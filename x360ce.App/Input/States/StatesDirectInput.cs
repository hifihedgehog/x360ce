using SharpDX;
using SharpDX.DirectInput;
using System;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to retrieve DirectInput device states.
	/// Handles state reading for Joystick, Keyboard, and Mouse devices.
	/// </summary>
	internal class StatesDirectInput
	{
		/// <summary>
		/// Returns the current state of a DirectInput device.
		/// The device must be acquired before reading state.
		/// </summary>
		/// <param name="deviceInfo">DirectInputDeviceInfo containing the device to read</param>
		/// <returns>Device state object (JoystickState, KeyboardState, or MouseState) or null if failed</returns>
		/// <remarks>
		/// This method reads the current input state from a DirectInput device.
		/// The returned object type depends on the device type:
		/// - Joystick/Gamepad: JoystickState (axes, sliders, buttons, POVs)
		/// - Keyboard: KeyboardState (key states)
		/// - Mouse: MouseState (position, buttons, wheel)
		/// 
		/// The device must be acquired before reading. If acquisition fails,
		/// this method will attempt to acquire it automatically.
		/// </remarks>
		public object GetDirectInputDeviceState(DirectInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.DirectInputDevice == null)
				return null;

			var device = deviceInfo.DirectInputDevice;

			try
			{
				// Attempt to acquire the device if not already acquired
				try
				{
					device.Acquire();
				}
				catch (SharpDXException)
				{
					// Device may already be acquired or temporarily unavailable
					// Continue to attempt state reading
				}

				// Poll the device to update state (required for some devices)
				device.Poll();

				// Read state based on device type
				switch (device)
				{
					case Joystick joystick:
						return joystick.GetCurrentState();
					case Keyboard keyboard:
						return keyboard.GetCurrentState();
					case Mouse mouse:
						return mouse.GetCurrentState();
					default:
						return null;
				}
			}
			catch (SharpDXException ex)
			{
				// Device may be unplugged or access lost
				// Return null to indicate state unavailable
				System.Diagnostics.Debug.WriteLine($"StatesDirectInput: Error reading state for {deviceInfo.InstanceName}: {ex.Message}");
				return null;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"StatesDirectInput: Unexpected error reading state for {deviceInfo.InstanceName}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Returns the current joystick state with type safety.
		/// </summary>
		/// <param name="deviceInfo">DirectInputDeviceInfo containing a joystick device</param>
		/// <returns>JoystickState or null if device is not a joystick or read failed</returns>
		public JoystickState GetJoystickState(DirectInputDeviceInfo deviceInfo)
		{
			return deviceInfo?.DirectInputDevice is Joystick
				? GetDirectInputDeviceState(deviceInfo) as JoystickState
				: null;
		}

		/// <summary>
		/// Returns the current keyboard state with type safety.
		/// </summary>
		/// <param name="deviceInfo">DirectInputDeviceInfo containing a keyboard device</param>
		/// <returns>KeyboardState or null if device is not a keyboard or read failed</returns>
		public KeyboardState GetKeyboardState(DirectInputDeviceInfo deviceInfo)
		{
			return deviceInfo?.DirectInputDevice is Keyboard
				? GetDirectInputDeviceState(deviceInfo) as KeyboardState
				: null;
		}

		/// <summary>
		/// Returns the current mouse state with type safety.
		/// </summary>
		/// <param name="deviceInfo">DirectInputDeviceInfo containing a mouse device</param>
		/// <returns>MouseState or null if device is not a mouse or read failed</returns>
		public MouseState GetMouseState(DirectInputDeviceInfo deviceInfo)
		{
			return deviceInfo?.DirectInputDevice is Mouse
				? GetDirectInputDeviceState(deviceInfo) as MouseState
				: null;
		}
	}
}
