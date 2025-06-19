using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	/// <summary>
	/// XInput processor - Handles Microsoft XInput API for Xbox controllers.
	/// </summary>
	/// <remarks>
	/// CRITICAL LIMITATIONS (users must be aware):
	/// • Maximum 4 controllers ONLY (hard XInput API limit)
	/// • Only XInput capable devices (Xbox 360/One controllers)
	/// • Cannot activate extra 2 rumble motors in Xbox One controller triggers
	/// • No support for generic gamepads or specialized controllers
	/// 
	/// CAPABILITIES:
	/// • XInput controllers CAN be accessed in background (major advantage over DirectInput)
	/// • Proper trigger separation (LT/RT as separate axes, not combined like DirectInput)
	/// • Guide button access available
	/// • Full rumble support available
	/// • Best performance for Xbox controllers
	/// • No cooperative level conflicts
	/// 
	/// CONTROLLER MAPPING:
	/// This processor maps XInput Gamepad state to CustomDiState as follows:
	/// • Buttons[0-15]: A, B, X, Y, LB, RB, Back, Start, LS, RS, DPad (4 directions), Guide, unused
	/// • Axis[0]: Left Thumbstick X (-32768 to 32767)
	/// • Axis[1]: Left Thumbstick Y (-32768 to 32767) 
	/// • Axis[2]: Right Thumbstick X (-32768 to 32767)
	/// • Axis[3]: Right Thumbstick Y (-32768 to 32767)
	/// • Axis[4]: Left Trigger (0 to 32767, converted from 0-255 byte range)
	/// • Axis[5]: Right Trigger (0 to 32767, converted from 0-255 byte range)
	/// </remarks>
	public class XInputProcessor : IInputProcessor
	{
		#region Static Controller Management

		/// <summary>
		/// Maximum number of XInput controllers supported.
		/// </summary>
		public const int MaxControllers = 4;

		/// <summary>
		/// Static array of XInput controllers for the 4 possible slots.
		/// </summary>
		private static Controller[] _xinputControllers;

		/// <summary>
		/// Tracks which XInput slots are currently assigned to which devices.
		/// </summary>
		private static Dictionary<Guid, int> _deviceToSlotMapping = new Dictionary<Guid, int>();

		/// <summary>
		/// Initialize static XInput controllers.
		/// </summary>
		static XInputProcessor()
		{
			_xinputControllers = new Controller[MaxControllers];
			for (int i = 0; i < MaxControllers; i++)
			{
				_xinputControllers[i] = new Controller((UserIndex)i);
			}
		}

		#endregion

		#region IInputProcessor Implementation

		/// <summary>
		/// Gets the input method supported by this processor.
		/// </summary>
		public InputMethod SupportedMethod => InputMethod.XInput;

		/// <summary>
		/// Determines if this processor can handle the specified device.
		/// </summary>
		/// <param name="device">The user device to check</param>
		/// <returns>True if device is Xbox-compatible and XInput slot is available</returns>
		public bool CanProcess(UserDevice device)
		{
			if (device == null || !device.IsOnline)
				return false;

			// Check if device is Xbox-compatible
			if (!device.IsXboxCompatible)
				return false;

			// Check if we have available XInput slots
			var assignedSlots = _deviceToSlotMapping.Values.ToHashSet();
			var availableSlots = Enumerable.Range(0, MaxControllers).Where(i => !assignedSlots.Contains(i));
			
			// Device can be processed if it already has a slot or if there's an available slot
			return _deviceToSlotMapping.ContainsKey(device.InstanceGuid) || availableSlots.Any();
		}

		/// <summary>
		/// Reads the current state from the device using XInput.
		/// </summary>
		/// <param name="device">The device to read from</param>
		/// <returns>CustomDiState representing the current controller state</returns>
		/// <exception cref="InputMethodException">Thrown when XInput encounters errors</exception>
		public CustomDiState ReadState(UserDevice device)
		{
			if (device == null)
				throw new InputMethodException(InputMethod.XInput, device, "Device is null");

			if (!device.IsXboxCompatible)
				throw new InputMethodException(InputMethod.XInput, device, "Device is not Xbox-compatible");

			try
			{
				// Get or assign XInput slot for this device
				int slotIndex = GetOrAssignSlot(device);
				var controller = _xinputControllers[slotIndex];

				// Read XInput state
				State xinputState;
				bool isConnected = controller.GetState(out xinputState);

				if (!isConnected)
				{
					// Controller disconnected - remove from slot mapping
					_deviceToSlotMapping.Remove(device.InstanceGuid);
					return null;
				}

				// Convert XInput Gamepad to CustomDiState
				var customState = ConvertGamepadToCustomDiState(xinputState.Gamepad);
				
				// Store the XInput state for potential use by other parts of the system
				device.DeviceState = xinputState.Gamepad;

				return customState;
			}
			catch (Exception ex)
			{
				var message = $"XInput read error: {ex.Message}\nDevice: {device.DisplayName}\nSlot: {GetAssignedSlot(device)}";
				throw new InputMethodException(InputMethod.XInput, device, message, ex);
			}
		}

		/// <summary>
		/// Handles force feedback for the device using XInput vibration.
		/// </summary>
		/// <param name="device">The device to send force feedback to</param>
		/// <param name="ffState">The force feedback state to apply</param>
		/// <remarks>
		/// XInput provides excellent vibration support for Xbox controllers.
		/// This is much more reliable than DirectInput force feedback for Xbox controllers.
		/// </remarks>
		public void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState)
		{
			if (device == null || ffState == null)
				return;

			try
			{
				// Get the assigned XInput slot
				if (!_deviceToSlotMapping.TryGetValue(device.InstanceGuid, out int slotIndex))
				{
					Debug.WriteLine($"XInput: No slot assigned for force feedback on device {device.DisplayName}");
					return;
				}

				var controller = _xinputControllers[slotIndex];
				
				// XInput vibration uses Vibration structure with left/right motor speeds
				// This should integrate with the existing force feedback system
				// For now, we'll log that force feedback is available
				Debug.WriteLine($"XInput: Force feedback available for {device.DisplayName} in slot {slotIndex}");
				
				// TODO: Integrate with existing ForceFeedbackState.SetDeviceForces logic
				// This would involve converting the ffState to XInput Vibration structure
			}
			catch (Exception ex)
			{
				// Log force feedback errors but don't throw - force feedback is optional
				Debug.WriteLine($"XInput force feedback error for {device.DisplayName}: {ex.Message}");
			}
		}

		/// <summary>
		/// Validates if the device can be used with XInput.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating compatibility and any limitations</returns>
		public ValidationResult ValidateDevice(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");

			if (!device.IsOnline)
				return ValidationResult.Error("Device is offline");

			if (!device.IsXboxCompatible)
				return ValidationResult.Error("XInput only supports Xbox-compatible controllers (Xbox 360/One)");

			// Check XInput controller limit
			var assignedSlots = _deviceToSlotMapping.Values.ToHashSet();
			var availableSlots = Enumerable.Range(0, MaxControllers).Where(i => !assignedSlots.Contains(i));
			
			// If device already has a slot, it's valid
			if (_deviceToSlotMapping.ContainsKey(device.InstanceGuid))
			{
				var slotIndex = _deviceToSlotMapping[device.InstanceGuid];
				return ValidationResult.Success($"XInput compatible - assigned to slot {slotIndex + 1}/4");
			}
			
			// Check if slots are available
			if (!availableSlots.Any())
			{
				return ValidationResult.Error($"XInput maximum {MaxControllers} controllers already in use. Free a slot or use different input method.");
			}

			var nextSlot = availableSlots.First();
			return ValidationResult.Success($"XInput compatible - will use slot {nextSlot + 1}/4");
		}

		#endregion

		#region XInput-Specific Implementation

		/// <summary>
		/// Gets or assigns an XInput slot for the specified device.
		/// </summary>
		/// <param name="device">The device to get/assign a slot for</param>
		/// <returns>The slot index (0-3)</returns>
		/// <exception cref="InputMethodException">Thrown when no slots are available</exception>
		private int GetOrAssignSlot(UserDevice device)
		{
			// If device already has a slot, return it
			if (_deviceToSlotMapping.TryGetValue(device.InstanceGuid, out int existingSlot))
				return existingSlot;

			// Find an available slot
			var assignedSlots = _deviceToSlotMapping.Values.ToHashSet();
			var availableSlots = Enumerable.Range(0, MaxControllers).Where(i => !assignedSlots.Contains(i));
			
			if (!availableSlots.Any())
				throw new InputMethodException(InputMethod.XInput, device, $"XInput maximum {MaxControllers} controllers already in use");

			// Assign the first available slot
			int newSlot = availableSlots.First();
			_deviceToSlotMapping[device.InstanceGuid] = newSlot;
			
			Debug.WriteLine($"XInput: Assigned device {device.DisplayName} to slot {newSlot + 1}");
			return newSlot;
		}

		/// <summary>
		/// Gets the currently assigned slot for a device.
		/// </summary>
		/// <param name="device">The device to check</param>
		/// <returns>The slot index if assigned, -1 if not assigned</returns>
		private int GetAssignedSlot(UserDevice device)
		{
			return _deviceToSlotMapping.TryGetValue(device.InstanceGuid, out int slot) ? slot : -1;
		}

		/// <summary>
		/// Converts XInput Gamepad state to CustomDiState format.
		/// </summary>
		/// <param name="gamepad">The XInput Gamepad state</param>
		/// <returns>CustomDiState with mapped values</returns>
		private CustomDiState ConvertGamepadToCustomDiState(Gamepad gamepad)
		{
			var customState = new CustomDiState();

			// Map buttons to CustomDiState.Buttons array
			// Using consistent button mapping that matches expected controller layout
			customState.Buttons[0] = gamepad.Buttons.HasFlag(GamepadButtonFlags.A);           // A button
			customState.Buttons[1] = gamepad.Buttons.HasFlag(GamepadButtonFlags.B);           // B button  
			customState.Buttons[2] = gamepad.Buttons.HasFlag(GamepadButtonFlags.X);           // X button
			customState.Buttons[3] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);           // Y button
			customState.Buttons[4] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);  // Left Bumper
			customState.Buttons[5] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder); // Right Bumper
			customState.Buttons[6] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);        // Back/Select
			customState.Buttons[7] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);       // Start/Menu
			customState.Buttons[8] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);   // Left Stick Click
			customState.Buttons[9] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);  // Right Stick Click
			
			// Map D-Pad to buttons 10-13
			customState.Buttons[10] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);     // D-Pad Up
			customState.Buttons[11] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);  // D-Pad Right
			customState.Buttons[12] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);   // D-Pad Down
			customState.Buttons[13] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);   // D-Pad Left
			
			// Guide button (if available) - button 14
			customState.Buttons[14] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Guide);      // Guide/Xbox button
			
			// Button 15 reserved for future use

			// Map thumbsticks to axes (XInput range is already -32768 to 32767)
			customState.Axis[0] = gamepad.LeftThumbX;   // Left Thumbstick X
			customState.Axis[1] = gamepad.LeftThumbY;   // Left Thumbstick Y  
			customState.Axis[2] = gamepad.RightThumbX;  // Right Thumbstick X
			customState.Axis[3] = gamepad.RightThumbY;  // Right Thumbstick Y

			// Map triggers to axes (convert from byte 0-255 to int -32768 to 32767)
			// Using 0 to 32767 range for triggers (positive values only)
			customState.Axis[4] = ConvertTriggerToAxis(gamepad.LeftTrigger);   // Left Trigger
			customState.Axis[5] = ConvertTriggerToAxis(gamepad.RightTrigger);  // Right Trigger

			// Note: XInput doesn't have POV/Hat controls like DirectInput, so POVs remain at default (-1)
			// Note: XInput doesn't have sliders, so they remain at default values

			return customState;
		}

		/// <summary>
		/// Converts XInput trigger value (0-255) to axis value (0-32767).
		/// </summary>
		/// <param name="triggerValue">Trigger value from XInput (0-255)</param>
		/// <returns>Axis value (0-32767)</returns>
		private int ConvertTriggerToAxis(byte triggerValue)
		{
			// Convert 0-255 range to 0-32767 range
			// Using positive range only since triggers don't have negative values
			return (int)((triggerValue / 255.0) * 32767.0);
		}

		/// <summary>
		/// Gets the current number of assigned XInput controllers.
		/// </summary>
		/// <returns>Number of currently assigned controllers (0-4)</returns>
		public static int GetAssignedControllerCount()
		{
			return _deviceToSlotMapping.Count;
		}

		/// <summary>
		/// Gets information about XInput slot assignments.
		/// </summary>
		/// <returns>Dictionary mapping device GUIDs to slot indices</returns>
		public static Dictionary<Guid, int> GetSlotAssignments()
		{
			return new Dictionary<Guid, int>(_deviceToSlotMapping);
		}

		/// <summary>
		/// Releases the XInput slot for a specific device.
		/// </summary>
		/// <param name="deviceGuid">The device GUID to release</param>
		/// <returns>True if a slot was released, false if device wasn't assigned</returns>
		public static bool ReleaseSlot(Guid deviceGuid)
		{
			if (_deviceToSlotMapping.Remove(deviceGuid))
			{
				Debug.WriteLine($"XInput: Released slot for device {deviceGuid}");
				return true;
			}
			return false;
		}

		/// <summary>
		/// Clears all XInput slot assignments.
		/// </summary>
		public static void ClearAllSlots()
		{
			_deviceToSlotMapping.Clear();
			Debug.WriteLine("XInput: Cleared all slot assignments");
		}

		#endregion
	}
}
