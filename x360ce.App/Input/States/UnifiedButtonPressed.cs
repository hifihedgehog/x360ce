using System;
using System.Collections.Generic;
using System.Diagnostics;
using x360ce.App.Input.Devices;
using x360ce.App.Input.Triggers;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Unified button press detection for all input methods (DirectInput, XInput, GamingInput, RawInput).
	/// Consolidates common logic for checking button states across different input APIs.
	/// </summary>
	internal class UnifiedButtonPressed
	{
		/// <summary>
		/// Configuration for each input method's processing.
		/// </summary>
		private sealed class InputMethodConfig
		{
			public Dictionary<string, UnifiedInputDeviceInfo> Mapping;
			public int LastDeviceCount;
			public Func<UnifiedInputDeviceManager, IReadOnlyList<dynamic>> GetDeviceList;
			public Func<dynamic, string> GetIdentifier;
			public Func<dynamic, bool> IsDeviceValid;
			public string InputTypeName;
		}

		// Input method configurations
		private readonly InputMethodConfig _directInputConfig;
		private readonly InputMethodConfig _xinputConfig;
		private readonly InputMethodConfig _gamingInputConfig;
		private readonly InputMethodConfig _rawInputConfig;

		// Debug output throttling for RawInput
		private DateTime _lastDebugOutput = DateTime.MinValue;
		private const double DebugOutputIntervalSeconds = 1.0;

		// Reference to the device input handler for updating value labels
		private DevicesTab_DeviceSelectedInput _deviceSelectedInput;

		/// <summary>
		/// Initializes input method configurations.
		/// </summary>
		public UnifiedButtonPressed()
		{
			_directInputConfig = new InputMethodConfig
			{
				InputTypeName = "DirectInput",
				GetDeviceList = dm => dm.DirectInputDeviceInfoList,
				GetIdentifier = d => d.InterfacePath,
				IsDeviceValid = d => d?.DirectInputDevice != null
			};

			_xinputConfig = new InputMethodConfig
			{
				InputTypeName = "XInput",
				GetDeviceList = dm => dm.XInputDeviceInfoList,
				GetIdentifier = d => d.CommonIdentifier,
				IsDeviceValid = d => d != null
			};

			_gamingInputConfig = new InputMethodConfig
			{
				InputTypeName = "GamingInput",
				GetDeviceList = dm => dm.GamingInputDeviceInfoList,
				GetIdentifier = d => d.CommonIdentifier,
				IsDeviceValid = d => d?.GamingInputDevice != null
			};

			_rawInputConfig = new InputMethodConfig
			{
				InputTypeName = "RawInput",
				GetDeviceList = dm => dm.RawInputDeviceInfoList,
				GetIdentifier = d => d.InterfacePath,
				IsDeviceValid = d => d?.InterfacePath != null
			};
		}

		/// <summary>
		/// Sets the reference to the device input handler for updating value labels.
		/// </summary>
		/// <param name="deviceSelectedInput">The device input handler instance</param>
		public void SetDeviceSelectedInput(DevicesTab_DeviceSelectedInput deviceSelectedInput)
		{
			_deviceSelectedInput = deviceSelectedInput;
		}

		/// <summary>
		/// Checks all input methods for button presses and updates ButtonPressed properties.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing all device lists</param>
		public void CheckAllInputMethods(UnifiedInputDeviceManager devicesCombined)
		{
			if (devicesCombined == null)
				return;

			CheckInputMethod(devicesCombined, _directInputConfig);
			CheckInputMethod(devicesCombined, _xinputConfig);
			CheckInputMethod(devicesCombined, _gamingInputConfig);
			CheckInputMethodWithDebug(devicesCombined, _rawInputConfig);
		}

		/// <summary>
		/// Generic method to check any input method for button presses.
		/// </summary>
		private void CheckInputMethod(UnifiedInputDeviceManager devicesCombined, InputMethodConfig config)
		{
			var deviceList = config.GetDeviceList(devicesCombined);
			var allDevicesList = devicesCombined.UnifiedInputDeviceInfoList;

			if (deviceList == null || allDevicesList == null)
				return;

			// Build mapping cache if needed
			int currentCount = deviceList.Count;
			if (config.Mapping == null || config.LastDeviceCount != currentCount)
			{
				BuildDeviceMapping(allDevicesList, config.InputTypeName, config.GetIdentifier, ref config.Mapping);
				config.LastDeviceCount = currentCount;
			}

			// Check each device
			foreach (var deviceInfo in deviceList)
			{
				if (!config.IsDeviceValid(deviceInfo))
					continue;

				var listState = deviceInfo.StateList;
				if (listState == null)
					continue;

				bool buttonPressed = IsAnyButtonOrPovPressed(listState);

				// Update ButtonPressed property using cached mapping
				string identifier = config.GetIdentifier(deviceInfo);
				if (config.Mapping.TryGetValue(identifier, out var allDevice))
				{
					allDevice.ButtonPressed = buttonPressed;
				}

				// Update value labels if handler is set
				_deviceSelectedInput?.UpdateValueLabels(deviceInfo.InterfacePath, listState);
			}
		}

		/// <summary>
		/// Checks RawInput devices with additional debug output.
		/// NON-BLOCKING: Uses WM_INPUT message-based system, never opens handles.
		/// </summary>
		private void CheckInputMethodWithDebug(UnifiedInputDeviceManager devicesCombined, InputMethodConfig config)
		{
			var deviceList = config.GetDeviceList(devicesCombined);
			var allDevicesList = devicesCombined.UnifiedInputDeviceInfoList;

			if (deviceList == null || allDevicesList == null)
				return;

			// Build mapping cache if needed
			int currentCount = deviceList.Count;
			if (config.Mapping == null || config.LastDeviceCount != currentCount)
			{
				BuildDeviceMapping(allDevicesList, config.InputTypeName, config.GetIdentifier, ref config.Mapping);
				config.LastDeviceCount = currentCount;
			}

			// Debug output throttling (every second for gamepads only)
			bool shouldDebug = (DateTime.Now - _lastDebugOutput).TotalSeconds >= DebugOutputIntervalSeconds;
			if (shouldDebug)
				_lastDebugOutput = DateTime.Now;

			// Check each device
			foreach (var deviceInfo in deviceList)
			{
				if (!config.IsDeviceValid(deviceInfo))
					continue;

				var listState = deviceInfo.StateList;
				bool buttonPressed = listState != null && IsAnyButtonOrPovPressed(listState);

				// Update ButtonPressed property using cached mapping
				string identifier = config.GetIdentifier(deviceInfo);
				if (config.Mapping.TryGetValue(identifier, out var allDevice))
				{
					allDevice.ButtonPressed = buttonPressed;
				}

				// Update value labels if handler is set
				if (_deviceSelectedInput != null && listState != null)
				{
					_deviceSelectedInput.UpdateValueLabels(deviceInfo.InterfacePath, listState);
				}

				// Debug output for gamepads only (exclude keyboard and mouse)
				if (shouldDebug && IsGamepad(deviceInfo))
				{
					string deviceName = allDevice?.ProductName ?? "Unknown";
					string stateStr = listState?.ToString() ?? "null";
					Debug.WriteLine($"[RawInput Gamepad] {deviceName}: State={stateStr}, ButtonPressed={buttonPressed}");
				}
			}
		}

		/// <summary>
		/// Checks if any button or POV is pressed in the given state.
		/// Optimized for high-frequency execution (1000Hz).
		/// </summary>
		/// <param name="listState">The device state to check</param>
		/// <returns>True if any button is pressed (value 1) or any POV is pressed (value > -1)</returns>
		private static bool IsAnyButtonOrPovPressed(InputStateAsList listState)
		{
			// Check buttons - optimized loop instead of Contains()
			if (listState?.Buttons != null)
			{
				var buttons = listState.Buttons;
				for (int i = 0; i < buttons.Count; i++)
				{
					if (buttons[i] == 1)
						return true;
				}
			}

			// Check POVs - optimized loop instead of Exists()
			if (listState?.POVs != null)
			{
				var povs = listState.POVs;
				for (int i = 0; i < povs.Count; i++)
				{
					if (povs[i] > -1)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Builds a mapping dictionary for fast device lookups.
		/// </summary>
		/// <param name="allDevicesList">The unified device list</param>
		/// <param name="inputType">The input type to filter by (e.g., "DirectInput", "XInput")</param>
		/// <param name="getIdentifier">Function to extract the identifier from a device</param>
		/// <param name="mapping">Reference to the mapping dictionary to populate</param>
		private void BuildDeviceMapping(
			System.Collections.ObjectModel.ObservableCollection<UnifiedInputDeviceInfo> allDevicesList,
			string inputType,
			Func<dynamic, string> getIdentifier,
			ref Dictionary<string, UnifiedInputDeviceInfo> mapping)
		{
			mapping = new Dictionary<string, UnifiedInputDeviceInfo>(allDevicesList.Count);

			foreach (var device in allDevicesList)
			{
				if (device.InputType != inputType)
					continue;

				string identifier = getIdentifier(device);
				if (!string.IsNullOrEmpty(identifier))
				{
					mapping[identifier] = device;
				}
			}
		}

		/// <summary>
		/// Determines if a RawInput device is a gamepad (not keyboard or mouse).
		/// </summary>
		private static bool IsGamepad(RawInputDeviceInfo device)
		{
			// Exclude keyboard (type 1) and mouse (type 0)
			// Gamepads are typically type 2 (HID)
			if (device.DeviceType == 0 || device.DeviceType == 1)
				return false;

			// Additional check: exclude devices with "keyboard" or "mouse" in the name
			string productName = device.ProductName?.ToLowerInvariant() ?? "";
			if (productName.Contains("keyboard") || productName.Contains("mouse"))
				return false;

			return true;
		}

		/// <summary>
		/// Invalidates all device mapping caches, forcing a rebuild on next check.
		/// Call this when device lists change.
		/// </summary>
		public void InvalidateCache()
		{
			InvalidateConfigCache(_directInputConfig);
			InvalidateConfigCache(_xinputConfig);
			InvalidateConfigCache(_gamingInputConfig);
			InvalidateConfigCache(_rawInputConfig);
		}

		/// <summary>
		/// Invalidates a single input method configuration cache.
		/// </summary>
		private static void InvalidateConfigCache(InputMethodConfig config)
		{
			config.Mapping = null;
			config.LastDeviceCount = 0;
		}
	}
}