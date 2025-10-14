using System.Collections.Generic;
using Windows.Gaming.Input;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to check if any button is pressed on Gaming Input devices.
	/// Supports Gaming Input devices (modern gamepads using Windows.Gaming.Input API).
	/// </summary>
	internal class StatesAnyButtonIsPressedGamingInput
	{
		private readonly StatesGamingInput _statesGamingInput = new StatesGamingInput();

		// Cache for Gaming Input device to AllInputDeviceInfo mapping
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;

		/// <summary>
		/// Checks each Gaming Input device for button presses and updates the ButtonPressed property
		/// in AllInputDevicesList.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckGamingInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			if (devicesCombined.GamingInputDevicesList == null || devicesCombined.AllInputDevicesList == null)
				return;

			// Build mapping cache on first run or when device list changes
			if (_deviceMapping == null || _deviceMapping.Count != devicesCombined.GamingInputDevicesList.Count)
				BuildDeviceMapping(devicesCombined);

			// Check each Gaming Input device
			foreach (var giDevice in devicesCombined.GamingInputDevicesList)
			{
				if (giDevice?.GamingInputDevice == null)
					continue;

				// Get the current state and check for button presses
				var state = _statesGamingInput.GetGamingInputDeviceState(giDevice);
				if (state == null)
					continue;

				// Determine if any button is pressed
				bool anyButtonPressed = IsAnyButtonPressed(state.Value);
	
				// Use cached mapping for faster lookup using CommonIdentifier
				if (_deviceMapping.TryGetValue(giDevice.CommonIdentifier, out var allDevice))
				{
					allDevice.ButtonPressed = anyButtonPressed;
				}
			}
		}

		/// <summary>
		/// Builds a mapping dictionary from CommonIdentifier to AllInputDeviceInfo for fast lookups.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		private void BuildDeviceMapping(DevicesCombined devicesCombined)
		{
			_deviceMapping = new Dictionary<string, DevicesCombined.AllInputDeviceInfo>();

			foreach (var device in devicesCombined.AllInputDevicesList)
			{
				if (device.InputType == "GamingInput" && !string.IsNullOrEmpty(device.CommonIdentifier))
				{
					_deviceMapping[device.CommonIdentifier] = device;
				}
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the given Gaming Input state.
		/// </summary>
		/// <param name="reading">The Gaming Input GamepadReading structure</param>
		/// <returns>True if any button is pressed, false otherwise</returns>
		/// <remarks>
		/// Checks all Gaming Input buttons including:
		/// • Face buttons (A, B, X, Y)
		/// • Shoulder buttons (LeftShoulder, RightShoulder)
		/// • Thumbstick buttons (LeftThumbstick, RightThumbstick)
		/// • System buttons (Menu, View)
		/// • D-Pad directions (DPadUp, DPadDown, DPadLeft, DPadRight)
		/// </remarks>
		private static bool IsAnyButtonPressed(GamepadReading reading)
		{
			// Check if any button flag is set
			return reading.Buttons != GamepadButtons.None;
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// Call this when device lists change.
		/// </summary>
		public void InvalidateCache()
		{
			_deviceMapping = null;
		}
	}
}
