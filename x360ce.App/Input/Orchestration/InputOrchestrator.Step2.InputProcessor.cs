using System;
using x360ce.App.Input.Processors;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Orchestration
{
public partial class InputOrchestrator
	{
		#region Input Processor Registry


		public DirectInputProcessor directInputProcessor = new DirectInputProcessor();
		public XInputProcessor xInputProcessor = new XInputProcessor();
		public GamingInputProcessor gamingInputProcessor = new GamingInputProcessor();
		public RawInputProcessor rawInputProcessor = new RawInputProcessor();

		/// <summary>
		/// Loads device capabilities using the appropriate processor based on the device's input method.
		/// This is the centralized entry point for capability loading across all input methods.
		/// </summary>
		/// <param name="device">The device to load capabilities for</param>
		/// <remarks>
		/// This method ensures capabilities are loaded consistently across all input methods:
		/// • DirectInput: Real hardware detection via DirectInput API (default method)
		/// • XInput: Standard Xbox controller capabilities (15 buttons, 6 axes)
		/// • Gaming Input: Gaming Input API capabilities (16 buttons, 6 axes)
		/// • Raw Input: HID descriptor-based capabilities with reasonable defaults
		///
		/// Called during device initialization and when input method changes.
		/// Handles capability loading failures gracefully with appropriate fallbacks.
		/// </remarks>
		public void LoadDeviceCapabilities(UserDevice device)
		{
			if (device == null)
				return;

			try
			{
				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						directInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.XInput:
						xInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.GamingInput:
						gamingInputProcessor.LoadCapabilities(device);
						break;
					case InputMethod.RawInput:
						rawInputProcessor.LoadCapabilities(device);
						break;
					default:
						throw new ArgumentException($"Invalid InputMethod: {device.InputMethod}");
				}

				System.Diagnostics.Debug.WriteLine($"Loaded {device.InputMethod} capabilities for {device.DisplayName} - Buttons: {device.CapButtonCount}, Axes: {device.CapAxeCount}, POVs: {device.CapPovCount}");
			}
			catch (Exception ex)
			{
				// Log error and clear capability values
				System.Diagnostics.Debug.WriteLine($"Capability loading failed for {device.DisplayName} ({device.InputMethod}): {ex.Message}");

				// Clear capability values instead of setting fake ones
				device.CapButtonCount = 0;
				device.CapAxeCount = 0;
				device.CapPovCount = 0;
				device.DeviceObjects = new DeviceObjectItem[0];
				device.DeviceEffects = new DeviceEffectItem[0];
			}
		}

		/// <summary>
		/// Gets detailed capability information using the appropriate processor.
		/// </summary>
		/// <param name="device">The device to get capability information for</param>
		/// <returns>String containing detailed capability information</returns>
		public string GetDeviceCapabilitiesInfo(UserDevice device)
		{
			if (device == null)
				return "Device is null";

			try
			{
				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						return directInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.XInput:
						return xInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.GamingInput:
						return gamingInputProcessor.GetCapabilitiesInfo(device);
					case InputMethod.RawInput:
						return rawInputProcessor.GetCapabilitiesInfo(device);
					default:
						return $"Unknown InputMethod: {device.InputMethod}";
				}
			}
			catch (Exception ex)
			{
				return $"Error getting capability info: {ex.Message}";
			}
		}

		/// <summary>
		/// Reloads capabilities when the input method changes.
		/// Ensures the device has accurate capabilities for the new input method.
		/// </summary>
		/// <param name="device">The device whose input method changed</param>
		/// <param name="previousMethod">The previous input method (for logging)</param>
		public void OnInputMethodChanged(UserDevice device, InputMethod previousMethod)
		{
			if (device == null)
				return;

			try
			{
				System.Diagnostics.Debug.WriteLine($"Input method changed for {device.DisplayName}: {previousMethod} → {device.InputMethod}");
				
				// Reload capabilities for the new input method
				LoadDeviceCapabilities(device);
				
				System.Diagnostics.Debug.WriteLine($"Capabilities updated for {device.DisplayName} - New method: {device.InputMethod}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to update capabilities after input method change for {device.DisplayName}: {ex.Message}");
			}
		}

		/// <summary>
		/// Validates that a device can be processed with its selected input method.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating compatibility and any limitations</returns>
		/// <remarks>
		/// This method provides comprehensive validation beyond simple compatibility checking.
		/// It returns detailed information about:
		/// • Device compatibility with the selected input method
		/// • Method-specific limitations and warnings
		/// • Clear error messages for unsupported combinations
		///
		/// VALIDATION EXAMPLES:
		/// • XInput with 5th controller: Error("XInput supports maximum 4 controllers")
		/// • DirectInput with Xbox on Win10: Warning("Xbox controllers may not work in background")
		/// • Gaming Input on Win7: Error("Gaming Input requires Windows 10 or later")
		///
		/// The validation does NOT recommend alternative methods - users must choose manually.
		/// </remarks>
		public ValidationResult ValidateDeviceInputMethod(UserDevice device)
		{
			if (device == null)
				return ValidationResult.Error("Device is null");
			try
			{

				switch (device.InputMethod)
				{
					case InputMethod.DirectInput:
						return directInputProcessor.ValidateDevice(device);
					case InputMethod.XInput:
						return xInputProcessor.ValidateDevice(device);
					case InputMethod.GamingInput:
						return gamingInputProcessor.ValidateDevice(device);
					case InputMethod.RawInput:
						return rawInputProcessor.ValidateDevice(device);
					default:
						return ValidationResult.Error($"Unknown InputMethod: {device.InputMethod}");
				}
			}
			catch (NotSupportedException ex)
			{
				return ValidationResult.Error(ex.Message);
			}
		}

		#endregion
	}
}
