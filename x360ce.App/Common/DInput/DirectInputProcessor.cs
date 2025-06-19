using JocysCom.ClassLibrary.IO;
using SharpDX;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	/// <summary>
	/// DirectInput processor - Handles legacy Microsoft DirectInput API via SharpDX wrapper.
	/// </summary>
	/// <remarks>
	/// CRITICAL LIMITATIONS (users must be aware):
	/// • Xbox One controllers: CANNOT read input when window loses focus on Windows 10+
	/// • Xbox 360/One controllers: Triggers combined on same axis (no separate LT/RT)
	/// • Xbox controllers: No Guide button access, no rumble support via DirectInput
	/// • Microsoft deprecated: Not recommended for new development
	/// • Windows Store: Cannot be used in Windows Store applications
	/// 
	/// CAPABILITIES:
	/// • All controller types: Works with any DirectInput-compatible device
	/// • Unlimited devices: No controller count limits
	/// • Generic controllers: Full support for non-Xbox devices
	/// • Mature API: Stable and well-documented (though deprecated)
	/// 
	/// BACKGROUND ACCESS ISSUE:
	/// On Windows 10+, Xbox controllers lose input when the application window loses focus,
	/// even when using CooperativeLevel.Background. This is a known limitation of the 
	/// DirectInput emulation layer for Xbox controllers on modern Windows versions.
	/// 
	/// WORKAROUND: Use XInput for Xbox controllers that need background access.
	/// </remarks>
	public class DirectInputProcessor : IInputProcessor
	{
		#region IInputProcessor Implementation

		/// <summary>
		/// Gets the input method supported by this processor.
		/// </summary>
		public InputMethod SupportedMethod => InputMethod.DirectInput;

		/// <summary>
		/// Determines if this processor can handle the specified device.
		/// DirectInput can handle any device that has a valid DirectInput Device object.
		/// </summary>
		/// <param name="device">The user device to check</param>
		/// <returns>True if the device has a valid DirectInput Device, false otherwise</returns>
		public bool CanProcess(UserDevice device)
		{
			if (device == null)
				return false;

			// DirectInput can process any device that has a Device object
			// This includes joysticks, gamepads, mice, and keyboards
			return device.Device != null || device.IsOnline;
		}

		/// <summary>
		/// Reads the current state from the device using DirectInput.
		/// </summary>
		/// <param name="device">The device to read from</param>
		/// <returns>CustomDiState representing the current controller state, or null if reading failed</returns>
		/// <exception cref="InputMethodException">Thrown when DirectInput encounters device access errors</exception>
		public CustomDiState ReadState(UserDevice device)
		{
			if (device?.Device == null)
				throw new InputMethodException(InputMethod.DirectInput, device, "Device not initialized or offline");

			var exceptionData = new StringBuilder();
			try
			{
				// Handle device acquisition and cooperative level
				HandleDeviceAcquisition(device, exceptionData);

				exceptionData.AppendFormat($"device.GetCurrentState() // device.IsExclusiveMode = {device.IsExclusiveMode}").AppendLine();

				// Polling - Retrieves data from polled objects on a DirectInput device.
				// Some devices require polling (For example original "XBOX Controller S" with XBCD drivers).
				// If the device does not require polling, calling this method has no effect.
				device.Device.Poll();

				// Read device state based on device type
				CustomDiState newState = null;
				switch (device.Device)
				{
					case Mouse mDevice:
						var mouseState = mDevice.GetCurrentState();
						newState = new CustomDiState(mouseState);
						device.DeviceState = mouseState;
						break;

					case Keyboard kDevice:
						var keyboardState = kDevice.GetCurrentState();
						newState = new CustomDiState(keyboardState);
						device.DeviceState = keyboardState;
						break;

					case Joystick jDevice:
						var joystickState = jDevice.GetCurrentState();
						newState = new CustomDiState(joystickState);
						device.DeviceState = joystickState;
						break;

					default:
						throw new InputMethodException(InputMethod.DirectInput, device, $"Unsupported device type: {device.Device.GetType()}");
				}

				// Handle mouse-specific coordinate processing
				if (device.Device.Information.Type == SharpDX.DirectInput.DeviceType.Mouse)
				{
					newState = ProcessMouseState(device, newState);
				}

				return newState;
			}
			catch (SharpDXException dex) when (
				dex.ResultCode == SharpDX.DirectInput.ResultCode.InputLost ||
				dex.ResultCode == SharpDX.DirectInput.ResultCode.NotAcquired ||
				dex.ResultCode == SharpDX.DirectInput.ResultCode.Unplugged)
			{
				// Device disconnected or lost - this is not an error, just mark device as needing update
				Debug.WriteLine($"DirectInput device lost: {DateTime.Now:HH:mm:ss.fff}");
				Debug.WriteLine($"Device {dex.Descriptor.ApiCode}. DisplayName {device.DisplayName}. ProductId {device.DevProductId}.");
				
				// Signal that devices need updating
				var helper = DInputHelper.Current;
				if (helper != null)
					helper.DevicesNeedUpdating = true;

				return null; // Return null to indicate device is offline
			}
			catch (Exception ex)
			{
				var message = $"DirectInput read error: {ex.Message}\nDevice: {device.DisplayName}\nDetails: {exceptionData}";
				throw new InputMethodException(InputMethod.DirectInput, device, message, ex);
			}
		}

		/// <summary>
		/// Handles force feedback for the device using DirectInput.
		/// </summary>
		/// <param name="device">The device to send force feedback to</param>
		/// <param name="ffState">The force feedback state to apply</param>
		/// <remarks>
		/// DirectInput force feedback limitations:
		/// • Xbox controllers: May not work properly (use XInput instead)
		/// • Requires exclusive mode: May conflict with other applications
		/// • Limited effect support: Compared to XInput's vibration
		/// </remarks>
		public void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState)
		{
			if (device?.Device == null || ffState == null)
				return;

			try
			{
				// DirectInput force feedback requires device capabilities check
				var deviceObject = device.Device;
				var hasForceFeedback = deviceObject.Capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback);
				
				if (!hasForceFeedback)
				{
					// No force feedback capability - this is not an error, just log and return
					Debug.WriteLine($"DirectInput: Device {device.DisplayName} does not support force feedback");
					return;
				}

				// Force feedback processing would go here
				// This involves the existing ForceFeedbackState.SetDeviceForces logic
				// For now, we'll maintain the existing implementation in the main DInputHelper
				
				Debug.WriteLine($"DirectInput: Force feedback processing for {device.DisplayName}");
			}
			catch (Exception ex)
			{
				// Log force feedback errors but don't throw - force feedback is optional
				Debug.WriteLine($"DirectInput force feedback error for {device.DisplayName}: {ex.Message}");
			}
		}

		/// <summary>
		/// Validates if the device can be used with DirectInput.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating compatibility and any limitations</returns>
		public ValidationResult ValidateDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			// Check for Xbox controller limitations on Windows 10+
			if (device.IsXboxCompatible)
			{
				var osVersion = Environment.OSVersion.Version;
				var isWindows10Plus = osVersion.Major >= 10;
				
				if (isWindows10Plus)
				{
					return ValidationResult.Warning(
						"Xbox controller with DirectInput on Windows 10+: " +
						"Input will be lost when window loses focus. " +
						"Consider using XInput for background access.");
				}
				else
				{
					return ValidationResult.Warning(
						"Xbox controller with DirectInput: " +
						"Triggers on same axis, no Guide button, no rumble support. " +
						"Consider using XInput for full Xbox controller features.");
				}
			}

			// Check for deprecated API warning
			if (device.Device != null)
			{
				return ValidationResult.Success("DirectInput compatible (API deprecated by Microsoft)");
			}

			return ValidationResult.Success("DirectInput compatible");
		}

		#endregion

		#region DirectInput-Specific Implementation

		/// <summary>
		/// Handles device acquisition and cooperative level management.
		/// Note: This method currently doesn't handle device acquisition since it requires 
		/// access to the DeviceDetector which is not available in the processor architecture.
		/// The existing DirectInput path in DInputHelper still handles this.
		/// </summary>
		private void HandleDeviceAcquisition(UserDevice device, StringBuilder exceptionData)
		{
			// TODO: This method needs to be redesigned to work with the processor architecture
			// For now, we'll skip device acquisition since it requires DeviceDetector access
			// The existing DirectInput implementation in DInputHelper.Step2.UpdateDiStates handles this
			
			exceptionData.AppendLine("DirectInput device acquisition handled by existing DInputHelper logic");
		}

		/// <summary>
		/// Sets the cooperative level for a DirectInput device.
		/// </summary>
		private void SetDeviceCooperativeLevel(UserDevice ud, DeviceDetector detector, Device device, StringBuilder exceptionData, CooperativeLevel cooperationLevel)
		{
			string mode = cooperationLevel == CooperativeLevel.Exclusive ? "Exclusive" : "NonExclusive";
			exceptionData.AppendLine($"UnAcquire ({mode})...");
			device.Unacquire();
			exceptionData.AppendLine($"SetCooperativeLevel ({mode})...");
			device.SetCooperativeLevel(detector.DetectorForm.Handle, CooperativeLevel.Background | cooperationLevel);
			exceptionData.AppendLine("Acquire...");
			device.Acquire();
			ud.IsExclusiveMode = cooperationLevel == CooperativeLevel.Exclusive;
		}

		/// <summary>
		/// Processes mouse state for special coordinate handling.
		/// </summary>
		private CustomDiState ProcessMouseState(UserDevice device, CustomDiState newState)
		{
			var newTime = Stopwatch.GetTimestamp();

			// If original state is missing then...
			if (device.OrgDiState == null)
			{
				// Store current values as original
				device.OrgDiState = newState;
				device.OrgDiStateTime = newTime;
				
				// Make sure new states have zero values for mouse movement
				for (int a = 0; a < newState.Axis.Length; a++)
					newState.Axis[a] = -short.MinValue;
				for (int s = 0; s < newState.Sliders.Length; s++)
					newState.Sliders[s] = -short.MinValue;
			}

			// Create new mouse state for position mapping
			var mouseState = new CustomDiState(new JoystickState());
			
			// Clone button values
			Array.Copy(newState.Buttons, mouseState.Buttons, mouseState.Buttons.Length);

			// Map mouse position to axis position (good for car wheel controls)
			ProcessMouseAxisMapping(device.OrgDiState.Axis, newState.Axis, mouseState.Axis);
			ProcessMouseAxisMapping(device.OrgDiState.Sliders, newState.Sliders, mouseState.Sliders);
			
			return mouseState;
		}

		/// <summary>
		/// Processes mouse axis mapping with sensitivity adjustment.
		/// </summary>
		private void ProcessMouseAxisMapping(int[] orgRange, int[] newState, int[] mouseState)
		{
			// Sensitivity factor for mouse movement conversion.
			var sensitivity = 16;
			for (int a = 0; a < newState.Length; a++)
			{
				// Get delta from original state and apply sensitivity.
				var value = (newState[a] - orgRange[a]) * sensitivity;
				if (value < ushort.MinValue)
				{
					value = ushort.MinValue;
					orgRange[a] = newState[a];
				}
				if (value > ushort.MaxValue)
				{
					value = ushort.MaxValue;
					orgRange[a] = newState[a] - (ushort.MaxValue / sensitivity);
				}
				mouseState[a] = value;
			}
		}

		#endregion
	}
}
