using System;
using System.Text;
using Windows.Gaming.Input;
using x360ce.Engine.Input.Devices;

namespace x360ce.Engine.Input.States
{
	/// <summary>
	/// Provides methods to retrieve Gaming Input device states.
	/// Handles state reading for modern gamepads using Windows.Gaming.Input API (Windows 10+).
	/// </summary>
	/// <remarks>
	/// Gaming Input API Characteristics:
	/// • Windows 10+ exclusive API for modern gamepad support
	/// • Direct gamepad object access (no slot-based indexing like XInput)
	/// • Supports advanced features like trigger rumble on Xbox One controllers
	/// • Provides normalized analog values (-1.0 to 1.0 for sticks, 0.0 to 1.0 for triggers)
	/// • No device acquisition needed (unlike DirectInput)
	/// • UWP-based API with background access limitations
	/// 
	/// Gaming Input State Structure (GamepadReading):
	/// • Timestamp: High-precision timestamp for the reading
	/// • Buttons: GamepadButtons flags enum (A, B, X, Y, shoulders, D-Pad, etc.)
	/// • LeftThumbstickX/Y: Left stick position (-1.0 to 1.0)
	/// • RightThumbstickX/Y: Right stick position (-1.0 to 1.0)
	/// • LeftTrigger/RightTrigger: Trigger pressure (0.0 to 1.0)
	/// </remarks>
	internal class GamingInputState
	{
		#region State Retrieval Methods

		/// <summary>
		/// Returns the current state of a Gaming Input device.
		/// The device info must contain a valid GamingInputDevice (Gamepad object).
		/// </summary>
		/// <param name="giDeviceInfo">GamingInputDeviceInfo containing the gamepad to read</param>
		/// <returns>GamepadReading structure or null if read failed or device is null</returns>
		/// <remarks>
		/// This method reads the current input state from a Gaming Input gamepad.
		/// 
		/// Gaming Input Device Access:
		/// • Gamepad objects are obtained from DevicesGamingInput.GetGamingInputDeviceList()
		/// • No slot-based indexing - direct object reference required
		/// • Gamepad objects remain valid until device is disconnected
		/// 
		/// Unlike DirectInput and XInput:
		/// • No device acquisition needed (always ready to read)
		/// • No slot limitations (supports as many gamepads as system allows)
		/// • Normalized analog values (no raw integer ranges)
		/// 
		/// GamepadReading Structure Contents:
		/// • Timestamp: High-precision timestamp (useful for input prediction)
		/// • Buttons: All button states as flags (A, B, X, Y, LB, RB, View, Menu, sticks, D-Pad)
		/// • LeftThumbstickX/Y: Left stick axes (-1.0 to 1.0, center = 0.0)
		/// • RightThumbstickX/Y: Right stick axes (-1.0 to 1.0, center = 0.0)
		/// • LeftTrigger: Left trigger (0.0 to 1.0, unpressed = 0.0)
		/// • RightTrigger: Right trigger (0.0 to 1.0, unpressed = 0.0)
		/// </remarks>
		public GamepadReading? GetGamingInputState(GamingInputDeviceInfo giDeviceInfo)
		{
			// Optimization: Local reference to avoid multiple property accesses
			var device = giDeviceInfo?.GamingInputDevice;
			if (device == null)
				return null;
			try
			{
				// Read Gaming Input state - no acquisition or polling needed
				return device.GetCurrentReading();
			}
			catch
			{
				// Device may be disconnected or access lost.
				// Do not log in high-frequency loop.
				return null;
			}
		}

		/// <summary>
		/// Returns the current state of a Gaming Input device using the gamepad object directly.
		/// </summary>
		/// <param name="gamepad">The Windows.Gaming.Input.Gamepad object to read from</param>
		/// <returns>GamepadReading structure or null if read failed or gamepad is null</returns>
		/// <remarks>
		/// This is an alternative method that accepts a Gamepad object directly.
		/// Useful when you have a gamepad reference but not the full device info.
		/// </remarks>
		public GamepadReading? GetGamingInputState(Gamepad gamepad)
		{
			if (gamepad == null)
				return null;
			try
			{
				// Read Gaming Input state - no acquisition or polling needed
				return gamepad.GetCurrentReading();
			}
			catch
			{
				// Device may be disconnected or access lost.
				// Do not log in high-frequency loop.
				return null;
			}
		}

		/// <summary>
		/// Returns the current state of a Gaming Input device by gamepad index.
		/// </summary>
		/// <param name="gamepadIndex">Zero-based index into Gamepad.Gamepads collection</param>
		/// <returns>GamepadReading structure or null if read failed or index invalid</returns>
		/// <remarks>
		/// This is a convenience method that retrieves the gamepad from the global collection
		/// and reads its state. Useful when you only have an index reference.
		/// 
		/// Index Mapping:
		/// • Index 0 = First gamepad in Gamepad.Gamepads collection
		/// • Index 1 = Second gamepad, etc.
		/// • Unlike XInput, there's no fixed slot assignment
		/// • Index order may change when devices connect/disconnect
		/// 
		/// For stable device tracking, prefer using the GamingInputDeviceInfo object directly
		/// rather than relying on collection indices.
		/// </remarks>
		public GamepadReading? GetGamingInputState(int gamepadIndex)
		{
			try
			{
				var gamepads = Gamepad.Gamepads;
				if (gamepadIndex >= 0 && gamepadIndex < gamepads.Count)
					return gamepads[gamepadIndex].GetCurrentReading();
			}
			catch { }
			return null;
		}

		#endregion

		#region Connection Status Methods

		/// <summary>
		/// Checks if Gaming Input API is available on the current system.
		/// </summary>
		/// <returns>True if Gaming Input is available and functional</returns>
		/// <remarks>
		/// Gaming Input requires Windows 10 or higher.
		/// This method tests actual API accessibility, not just OS version.
		/// </remarks>
		public bool IsGamingInputAvailable()
		{
			try
			{
				// Test Gaming Input API accessibility
				var gamepads = Gamepad.Gamepads;
				return true;
			}
			catch { return false; }
		}

		/// <summary>
		/// Gets the number of currently connected Gaming Input gamepads.
		/// </summary>
		/// <returns>Number of connected gamepads, or 0 if Gaming Input unavailable</returns>
		public int GetConnectedGamepadCount()
		{
			try
			{
				return Gamepad.Gamepads.Count;
			}
			catch { return 0; }
		}

		/// <summary>
		/// Checks if a specific gamepad index is valid and connected.
		/// </summary>
		/// <param name="gamepadIndex">Zero-based gamepad index</param>
		/// <returns>True if gamepad exists at this index</returns>
		public bool IsGamepadConnected(int gamepadIndex)
		{
			try
			{
				var gamepads = Gamepad.Gamepads;
				return gamepadIndex >= 0 && gamepadIndex < gamepads.Count;
			}
			catch { return false; }
		}

		/// <summary>
		/// Checks if a specific device info represents a connected gamepad.
		/// </summary>
		/// <param name="deviceInfo">The device info to check</param>
		/// <returns>True if device is online and has a valid gamepad object</returns>
		public bool IsDeviceConnected(GamingInputDeviceInfo deviceInfo) =>
			deviceInfo != null && deviceInfo.IsOnline && deviceInfo.GamingInputDevice != null;

		#endregion

		#region Diagnostic Methods

		/// <summary>
		/// Gets diagnostic information about all Gaming Input gamepads.
		/// </summary>
		/// <returns>String containing detailed Gaming Input status</returns>
		/// <remarks>
		/// Provides comprehensive overview of Gaming Input system state including:
		/// • API availability and OS compatibility
		/// • Connected gamepad count and details
		/// • Current state readings for each gamepad
		/// • Button states, analog stick positions, and trigger values
		/// </remarks>
		public string GetDiagnosticInfo()
		{
			var sb = new StringBuilder();
			sb.AppendLine("=== Gaming Input Status ===");

			try
			{
				// Check OS version
				var osVersion = Environment.OSVersion.Version;
				var isWindows10Plus = osVersion.Major >= 10;
				sb.AppendLine($"Operating System: {osVersion}");
				sb.AppendLine($"Windows 10+ Required: {isWindows10Plus}");

				// Check API availability
				var isAvailable = IsGamingInputAvailable();
				sb.AppendLine($"Gaming Input Available: {isAvailable}");

				if (!isAvailable)
				{
					sb.AppendLine().AppendLine("⚠️ Gaming Input is not available on this system");
					sb.AppendLine("Requires: Windows 10 or higher");
					return sb.ToString();
				}

				// Get connected gamepads
				var gamepads = Gamepad.Gamepads;
				sb.AppendLine().AppendLine($"Connected Gamepads: {gamepads.Count}");

				// Detail each gamepad
				for (int i = 0; i < gamepads.Count; i++)
				{
					sb.AppendLine().AppendLine($"--- Gamepad {i + 1} ---");
					try
					{
						var reading = gamepads[i].GetCurrentReading();
						sb.AppendLine($"Timestamp: {reading.Timestamp}");
						sb.AppendLine($"Buttons: {reading.Buttons}");
						foreach (GamepadButtons button in Enum.GetValues(typeof(GamepadButtons)))
						{
							if (button == GamepadButtons.None) continue;
							sb.AppendLine($"  {button}: {reading.Buttons.HasFlag(button)}");
						}
						sb.AppendLine($"Left Stick: X={reading.LeftThumbstickX:F3}, Y={reading.LeftThumbstickY:F3}");
						sb.AppendLine($"Right Stick: X={reading.RightThumbstickX:F3}, Y={reading.RightThumbstickY:F3}");
						sb.AppendLine($"Triggers: L={reading.LeftTrigger:F3}, R={reading.RightTrigger:F3}");
					}
					catch (Exception ex)
					{
						sb.AppendLine($"Error reading gamepad {i + 1}: {ex.Message}");
					}
				}
				sb.AppendLine().AppendLine("=== Gaming Input Features ===");
				sb.AppendLine("✅ Normalized analog values (-1.0 to 1.0)");
				sb.AppendLine("✅ Separate trigger axes (0.0 to 1.0)");
				sb.AppendLine("✅ High-precision timestamps");
				sb.AppendLine("✅ Advanced vibration support (including trigger rumble)");
				sb.AppendLine("⚠️ No background access (UWP limitation)");
				sb.AppendLine("⚠️ No Guide button access");
			}
			catch (Exception ex)
			{
				sb.AppendLine().AppendLine($"Error getting Gaming Input diagnostic info: {ex.Message}");
			}
			return sb.ToString();
		}

		/// <summary>
		/// Gets detailed state information for a specific device.
		/// </summary>
		/// <param name="deviceInfo">The device to get state information for</param>
		/// <returns>String containing detailed state information</returns>
		public string GetDeviceStateInfo(GamingInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.GamingInputDevice == null)
				return "Device info is null or missing gamepad object";

			var sb = new StringBuilder();
			try
			{
				var reading = deviceInfo.GamingInputDevice.GetCurrentReading();
				sb.AppendLine($"=== {deviceInfo.DisplayName} State ===");
				sb.AppendLine($"Timestamp: {reading.Timestamp}");
				sb.AppendLine($"Buttons: {reading.Buttons}");
				sb.AppendLine($"Left Stick: ({reading.LeftThumbstickX:F3}, {reading.LeftThumbstickY:F3})");
				sb.AppendLine($"Right Stick: ({reading.RightThumbstickX:F3}, {reading.RightThumbstickY:F3})");
				sb.AppendLine($"Triggers: L={reading.LeftTrigger:F3}, R={reading.RightTrigger:F3}");
			}
			catch (Exception ex)
			{
				sb.AppendLine($"Error reading device state: {ex.Message}");
			}
			return sb.ToString();
		}

		/// <summary>
		/// Gets detailed state information for a gamepad by index.
		/// </summary>
		/// <param name="gamepadIndex">Zero-based gamepad index</param>
		/// <returns>String containing detailed state information</returns>
		public string GetGamepadStateInfo(int gamepadIndex)
		{
			try
			{
				var gamepads = Gamepad.Gamepads;
				if (gamepadIndex < 0 || gamepadIndex >= gamepads.Count)
					return $"Invalid gamepad index {gamepadIndex}. Available gamepads: {gamepads.Count}";

				var reading = gamepads[gamepadIndex].GetCurrentReading();
				var sb = new StringBuilder();
				sb.AppendLine($"=== Gamepad {gamepadIndex + 1} State ===");
				sb.AppendLine($"Timestamp: {reading.Timestamp}");
				sb.AppendLine($"Buttons: {reading.Buttons}");
				sb.AppendLine($"Left Stick: ({reading.LeftThumbstickX:F3}, {reading.LeftThumbstickY:F3})");
				sb.AppendLine($"Right Stick: ({reading.RightThumbstickX:F3}, {reading.RightThumbstickY:F3})");
				sb.AppendLine($"Triggers: L={reading.LeftTrigger:F3}, R={reading.RightTrigger:F3}");
				return sb.ToString();
			}
			catch (Exception ex)
			{
				return $"Error getting gamepad state info: {ex.Message}";
			}
		}

		#endregion
	}
}
