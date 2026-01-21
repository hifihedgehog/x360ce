using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Processors
{
    /// <summary>
    /// Interface for input processors that handle different input methods (DirectInput, XInput, RawInput, GamingInput).
    /// </summary>
    public interface IInputProcessor
    {
        /// <summary>
        /// Gets the input method supported by this processor.
        /// </summary>
        InputSourceType SupportedMethod { get; }

        /// <summary>
        /// Determines if this processor can handle the specified device.
        /// </summary>
        /// <param name="device">The user device to check</param>
        /// <returns>True if the processor can handle this device, false otherwise</returns>
        bool CanProcess(UserDevice device);

        /// <summary>
        /// Reads the current state from the device using the processor's input method.
        /// </summary>
        /// <param name="device">The device to read from</param>
        /// <returns>CustomDeviceState representing the current controller state</returns>
        /// <exception cref="InputMethodException">Thrown when the input method encounters errors</exception>
        CustomDeviceState ReadState(UserDevice device);

        /// <summary>
        /// Handles force feedback for the specified device.
        /// </summary>
        /// <param name="device">The device to send force feedback to</param>
        /// <param name="ffState">The force feedback state to apply</param>
        void HandleForceFeedback(UserDevice device, Engine.ForceFeedbackState ffState);

        /// <summary>
        /// Checks if this input method is available on the current system.
        /// </summary>
        /// <returns>True if the input method is available and functional</returns>
        bool IsAvailable();

        /// <summary>
        /// Gets diagnostic information about this input method's system status.
        /// </summary>
        /// <returns>String containing diagnostic information for troubleshooting</returns>
        string GetDiagnosticInfo();

        /// <summary>
        /// Validates if the device can be used with this input method.
        /// </summary>
        /// <param name="device">The device to validate</param>
        /// <returns>ValidationResult indicating compatibility and any limitations</returns>
        ValidationResult ValidateDevice(UserDevice device);

        /// <summary>
        /// Loads device capabilities specific to this input method.
        /// Populates CapButtonCount, CapAxeCount, DeviceObjects, and related properties.
        /// </summary>
        /// <param name="device">The device to load capabilities for</param>
        /// <remarks>
        /// This method ensures the device has accurate capability information for UI display:
        /// • DirectInput: Uses actual hardware detection via DirectInput API
        /// • XInput: Uses standardized Xbox controller capabilities (15 buttons, 6 axes)
        /// • Gaming Input: Uses Gaming Input API capabilities (16 buttons, 6 axes)
        /// • Raw Input: Uses HID descriptor-based capabilities with reasonable defaults
        ///
        /// Called during device initialization and when input method changes.
        /// </remarks>
        void LoadCapabilities(UserDevice device);

        /// <summary>
        /// Gets human-readable capability information for diagnostics and troubleshooting.
        /// </summary>
        /// <param name="device">The device to get capability information for</param>
        /// <returns>String containing detailed capability information</returns>
        /// <remarks>
        /// Provides detailed capability information including:
        /// • Button, axis, and POV counts
        /// • Force feedback support details
        /// • Input method-specific limitations and features
        /// • Device object information
        ///
        /// Used for diagnostic logs and user information display.
        /// </remarks>
        string GetCapabilitiesInfo(UserDevice device);
    }
}
