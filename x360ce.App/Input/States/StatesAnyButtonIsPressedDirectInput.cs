using System.Collections.Generic;
using System.Linq;
using SharpDX.DirectInput;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to check if any button is pressed on DirectInput devices.
	/// Supports DirectInput devices (joysticks, keyboards, mice).
	/// </summary>
	internal class StatesAnyButtonIsPressedDirectInput
	{
		private readonly StatesDirectInput _statesDirectInput = new StatesDirectInput();
		
		// Cache for DirectInput device to AllInputDeviceInfo mapping
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;

		/// <summary>
		/// Checks each DirectInput device for button presses and updates the ButtonPressed property
		/// in AllInputDevicesList.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckDirectInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			if (devicesCombined.DirectInputDevicesList == null || devicesCombined.AllInputDevicesList == null)
				return;
			
			// Build mapping cache on first run or when device list changes
			if (_deviceMapping == null || _deviceMapping.Count != devicesCombined.DirectInputDevicesList.Count)
				BuildDeviceMapping(devicesCombined);
			
			// Check each DirectInput device
			foreach (var diDevice in devicesCombined.DirectInputDevicesList)
			{
				if (diDevice?.DirectInputDevice == null)
					continue;
			
				// Get the current state and check for button presses
				var state = _statesDirectInput.GetDirectInputDeviceState(diDevice);
				if (state == null)
					continue;
			
				// Determine if any button is pressed based on device type
				bool anyButtonPressed = IsAnyButtonPressed(state);
			
				// Use cached mapping for faster lookup
				if (_deviceMapping.TryGetValue(diDevice.InterfacePath, out var allDevice))
				{
					allDevice.ButtonPressed = anyButtonPressed;
				}
			}
		}

		/// <summary>
		/// Builds a mapping dictionary from InterfacePath to AllInputDeviceInfo for fast lookups.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		private void BuildDeviceMapping(DevicesCombined devicesCombined)
		{
			_deviceMapping = new Dictionary<string, DevicesCombined.AllInputDeviceInfo>();
			
			foreach (var device in devicesCombined.AllInputDevicesList)
			{
				if (device.InputType == "DirectInput" && !string.IsNullOrEmpty(device.InterfacePath))
				{
					_deviceMapping[device.InterfacePath] = device;
				}
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the given device state.
		/// </summary>
		/// <param name="state">The device state (JoystickState, KeyboardState, or MouseState)</param>
		/// <returns>True if any button is pressed, false otherwise</returns>
		private static bool IsAnyButtonPressed(object state)
		{
			if (state is JoystickState joystickState)
				return joystickState.Buttons.Any(b => b);
			
			if (state is KeyboardState keyboardState)
				return keyboardState.PressedKeys.Count > 0;
			
			if (state is MouseState mouseState)
				return mouseState.Buttons.Any(b => b);
			
			return false;
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
