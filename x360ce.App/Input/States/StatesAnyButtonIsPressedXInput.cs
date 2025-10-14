using System.Collections.Generic;
using SharpDX.XInput;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to check if any button is pressed on XInput devices.
	/// </summary>
	internal class StatesAnyButtonIsPressedXInput
	{
		private readonly StatesXinput _statesXinput = new StatesXinput();

		// Cache for XInput device to AllInputDeviceInfo mapping
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;

		/// <summary>
		/// Checks each XInput device for button presses and updates the ButtonPressed property
		/// in AllInputDevicesList.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckXInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			if (devicesCombined.XInputDevicesList == null || devicesCombined.AllInputDevicesList == null)
				return;

			// Build mapping cache on first run or when device list changes
			if (_deviceMapping == null || _deviceMapping.Count != devicesCombined.XInputDevicesList.Count)
				BuildDeviceMapping(devicesCombined);

			// Check each XInput device
			foreach (var xiDevice in devicesCombined.XInputDevicesList)
			{
				if (xiDevice == null)
					continue;

				// Get the current state and check for button presses
				var state = _statesXinput.GetXInputDeviceState(xiDevice);
				if (state == null)
					continue;

				// Determine if any button is pressed
				bool anyButtonPressed = IsAnyButtonPressed(state.Value);
	
				// Use cached mapping for faster lookup using CommonIdentifier
				if (_deviceMapping.TryGetValue(xiDevice.CommonIdentifier, out var allDevice))
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
				if (device.InputType == "XInput" && !string.IsNullOrEmpty(device.CommonIdentifier))
				{
					_deviceMapping[device.CommonIdentifier] = device;
				}
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the given XInput state.
		/// </summary>
		/// <param name="state">The XInput State structure</param>
		/// <returns>True if any button is pressed, false otherwise</returns>
		/// <remarks>
		/// Checks all XInput buttons including:
		/// • Face buttons (A, B, X, Y)
		/// • Shoulder buttons (LB, RB)
		/// • Thumbstick buttons (LS, RS)
		/// • System buttons (Start, Back)
		/// • D-Pad directions (Up, Down, Left, Right)
		/// </remarks>
		private static bool IsAnyButtonPressed(State state)
		{
			var buttons = state.Gamepad.Buttons;

			// Check if any button flag is set
			return buttons != GamepadButtonFlags.None;
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
