using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	/// <summary>
	/// Interface for input processors that handle different input methods (DirectInput, XInput, Gaming Input, Raw Input).
	/// Each processor converts its specific input format to the standardized CustomDiState format.
	/// </summary>
	/// <remarks>
	/// DESIGN PRINCIPLES:
	/// • Each processor handles one specific input method
	/// • All processors must produce consistent CustomDiState output
	/// • No automatic fallbacks - processors must gracefully fail with clear error messages
	/// • Processors must document their specific limitations
	/// • Force feedback handling is optional and method-specific
	/// </remarks>
	public interface IInputProcessor
	{
		/// <summary>
		/// Gets the input method supported by this processor.
		/// </summary>
		InputMethod SupportedMethod { get; }

		/// <summary>
		/// Determines if this processor can handle the specified device.
		/// </summary>
		/// <param name="device">The user device to check</param>
		/// <returns>True if the processor can handle this device, false otherwise</returns>
		/// <remarks>
		/// This method should check:
		/// • Device compatibility with the input method
		/// • System requirements (e.g., Windows version for Gaming Input)
		/// • Hardware limitations (e.g., XInput 4-controller limit)
		/// • Current processor state (e.g., available slots)
		/// 
		/// This method should NOT automatically change the device's InputMethod property.
		/// </remarks>
		bool CanProcess(UserDevice device);

		/// <summary>
		/// Reads the current state from the device using this input method.
		/// </summary>
		/// <param name="device">The device to read from</param>
		/// <returns>CustomDiState representing the current controller state, or null if reading failed</returns>
		/// <remarks>
		/// IMPLEMENTATION REQUIREMENTS:
		/// • Must map physical controller inputs to consistent CustomDiState properties
		/// • Must handle method-specific limitations gracefully
		/// • Must throw InputMethodException with clear error message on failure
		/// • Must maintain consistent coordinate systems across all processors
		/// • Must preserve existing CustomDiState mapping conventions
		/// 
		/// COORDINATE SYSTEM CONSISTENCY:
		/// • Buttons: CustomDiState.Buttons[0-255] (true/false)
		/// • Axes: CustomDiState.Axis[0-23] (int values, -32768 to 32767 range)
		/// • Sliders: CustomDiState.Sliders[0-7] (int values, -32768 to 32767 range)
		/// • POVs: CustomDiState.POVs[0-3] (int degrees, -1 for centered)
		/// 
		/// MAPPING CONSISTENCY:
		/// All processors should map the same physical buttons/axes to the same CustomDiState indices:
		/// • Primary fire button → Buttons[0]
		/// • Left thumbstick X/Y → Axis[0]/Axis[1] 
		/// • Right thumbstick X/Y → Axis[2]/Axis[3]
		/// • Left/Right triggers → Axis[4]/Axis[5] (when available as separate axes)
		/// </remarks>
		/// <exception cref="InputMethodException">Thrown when the input method encounters an error specific to its limitations</exception>
		CustomDiState ReadState(UserDevice device);

		/// <summary>
		/// Handles force feedback for the device using this input method.
		/// </summary>
		/// <param name="device">The device to send force feedback to</param>
		/// <param name="ffState">The force feedback state to apply</param>
		/// <remarks>
		/// FORCE FEEDBACK SUPPORT:
		/// • DirectInput: Limited support, Xbox controllers may not work
		/// • XInput: Full vibration support for Xbox controllers
		/// • Gaming Input: Full support including trigger rumble for Xbox One
		/// • Raw Input: Probably no support (method-specific limitation)
		/// 
		/// Implementations should:
		/// • Check if force feedback is supported for this method
		/// • Handle unsupported devices gracefully (no exceptions)
		/// • Log warnings when force feedback is not available
		/// • Maintain existing ForceFeedbackState integration
		/// </remarks>
		void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState);

		/// <summary>
		/// Validates if the device can be used with this input method and returns detailed validation results.
		/// </summary>
		/// <param name="device">The device to validate</param>
		/// <returns>ValidationResult indicating success, warning, or error with detailed message</returns>
		/// <remarks>
		/// This method provides detailed validation beyond the simple CanProcess check.
		/// It should return:
		/// • ValidationResult.Success() - Device fully compatible
		/// • ValidationResult.Warning(message) - Device works but with limitations
		/// • ValidationResult.Error(message) - Device cannot be used with this method
		/// 
		/// VALIDATION EXAMPLES:
		/// • XInput with 5th controller: Error("XInput supports maximum 4 controllers")
		/// • DirectInput with Xbox on Win10: Warning("Xbox controllers may not work in background")
		/// • Gaming Input on Win7: Error("Gaming Input requires Windows 10 or later")
		/// </remarks>
		ValidationResult ValidateDevice(UserDevice device);
	}

	/// <summary>
	/// Exception thrown when an input method encounters an error specific to its limitations.
	/// </summary>
	public class InputMethodException : System.Exception
	{
		/// <summary>
		/// Gets the input method that caused the exception.
		/// </summary>
		public InputMethod InputMethod { get; }

		/// <summary>
		/// Gets the device that was being processed when the error occurred.
		/// </summary>
		public UserDevice Device { get; }

		/// <summary>
		/// Initializes a new instance of the InputMethodException class.
		/// </summary>
		/// <param name="inputMethod">The input method that caused the exception</param>
		/// <param name="device">The device being processed</param>
		/// <param name="message">The error message</param>
		public InputMethodException(InputMethod inputMethod, UserDevice device, string message)
			: base(message)
		{
			InputMethod = inputMethod;
			Device = device;
		}

		/// <summary>
		/// Initializes a new instance of the InputMethodException class with an inner exception.
		/// </summary>
		/// <param name="inputMethod">The input method that caused the exception</param>
		/// <param name="device">The device being processed</param>
		/// <param name="message">The error message</param>
		/// <param name="innerException">The exception that caused this exception</param>
		public InputMethodException(InputMethod inputMethod, UserDevice device, string message, System.Exception innerException)
			: base(message, innerException)
		{
			InputMethod = inputMethod;
			Device = device;
		}
	}

	/// <summary>
	/// Represents the result of validating a device with an input method.
	/// </summary>
	public class ValidationResult
	{
		/// <summary>
		/// Gets the validation status.
		/// </summary>
		public ValidationStatus Status { get; private set; }

		/// <summary>
		/// Gets the validation message providing details about the result.
		/// </summary>
		public string Message { get; private set; }

		/// <summary>
		/// Gets whether the validation passed (Success or Warning).
		/// </summary>
		public bool IsValid => Status == ValidationStatus.Success || Status == ValidationStatus.Warning;

		private ValidationResult(ValidationStatus status, string message)
		{
			Status = status;
			Message = message ?? string.Empty;
		}

		/// <summary>
		/// Creates a successful validation result.
		/// </summary>
		/// <param name="message">Optional success message</param>
		/// <returns>Success validation result</returns>
		public static ValidationResult Success(string message = null)
		{
			return new ValidationResult(ValidationStatus.Success, message);
		}

		/// <summary>
		/// Creates a warning validation result.
		/// </summary>
		/// <param name="message">Warning message explaining the limitation</param>
		/// <returns>Warning validation result</returns>
		public static ValidationResult Warning(string message)
		{
			return new ValidationResult(ValidationStatus.Warning, message);
		}

		/// <summary>
		/// Creates an error validation result.
		/// </summary>
		/// <param name="message">Error message explaining why the device cannot be used</param>
		/// <returns>Error validation result</returns>
		public static ValidationResult Error(string message)
		{
			return new ValidationResult(ValidationStatus.Error, message);
		}
	}

	/// <summary>
	/// Validation status levels.
	/// </summary>
	public enum ValidationStatus
	{
		/// <summary>
		/// Device is fully compatible with the input method.
		/// </summary>
		Success,

		/// <summary>
		/// Device works but has limitations with this input method.
		/// </summary>
		Warning,

		/// <summary>
		/// Device cannot be used with this input method.
		/// </summary>
		Error
	}
}
