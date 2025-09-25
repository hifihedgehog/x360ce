using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Gaming.Input;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// Gaming Input device container with both device information and the actual Gaming Input gamepad object.
    /// Contains comprehensive device metadata plus the live Gaming Input gamepad for input reading.
    /// </summary>
    public class GamingInputDeviceInfo : IDisposable
    {
        public Guid InstanceGuid { get; set; }
        public string InstanceName { get; set; }
        public Guid ProductGuid { get; set; }
        public string ProductName { get; set; }
        public int DeviceType { get; set; }
        public int DeviceSubtype { get; set; }
        public int Usage { get; set; }
        public int UsagePage { get; set; }
        public int AxeCount { get; set; }
        public int ButtonCount { get; set; }
        public int PovCount { get; set; }
        public bool HasForceFeedback { get; set; }
        public int DriverVersion { get; set; }
        public int HardwareRevision { get; set; }
        public int FirmwareRevision { get; set; }
        public bool IsOnline { get; set; }
        public string DeviceTypeName { get; set; }
        public string InterfacePath { get; set; }
        
        // Additional identification properties
        public int VendorId { get; set; }
        public int ProductId { get; set; }
        public Guid ClassGuid { get; set; }
        public string HardwareIds { get; set; }
        public string DeviceId { get; set; }
        public string ParentDeviceId { get; set; }
        
        // Gaming Input-specific properties
        public int GamepadIndex { get; set; }
        public uint LastTimestamp { get; set; }
        public bool SupportsVibration { get; set; }
        public bool SupportsTriggerRumble { get; set; }
        
        /// <summary>
        /// Note: Gaming Input API does not provide native friendly names or manufacturer information.
        /// Gaming Input is the highest level of abstraction and focuses on functionality over hardware identity.
        /// It does not expose VID/PID, manufacturer names, or device-specific information.
        /// Use DirectInput or RawInput for hardware identification details.
        /// </summary>
        
        /// <summary>
        /// The actual Gaming Input gamepad object for reading input.
        /// </summary>
        public Gamepad GamingInputDevice { get; set; }
        
        /// <summary>
        /// Display name combining index and name for easy identification.
        /// </summary>
        public string DisplayName => $"Gaming Input {GamepadIndex + 1} - {InstanceName}";
        
        /// <summary>
        /// VID/PID string in standard format for hardware identification.
        /// </summary>
        public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
        
        /// <summary>
        /// Dispose the Gaming Input gamepad when no longer needed.
        /// </summary>
        public void Dispose()
        {
            // Gaming Input devices don't need explicit disposal, but we clear the reference
            GamingInputDevice = null;
        }
    }

    /// <summary>
    /// Gaming Input device enumeration and management class.
    /// Self-contained implementation with minimal external dependencies.
    /// Provides functionality to discover and list Gaming Input devices including modern gamepads.
    /// Returns live Gaming Input gamepad objects that can be used for input reading.
    /// </summary>
    internal class DevicesGamingInput
    {
        /// <summary>
        /// Creates a public list of Gaming Input devices (modern gamepads) with live gamepad objects and logs their properties.
        /// This method enumerates all available Gaming Input gamepads and outputs detailed information for debugging.
        /// </summary>
        /// <returns>List of GamingInputDeviceInfo objects containing both device information and live Gaming Input gamepad objects</returns>
        /// <remarks>
        /// This method performs comprehensive Gaming Input device enumeration:
        /// • Discovers all Gaming Input-compatible devices (modern gamepads)
        /// • Creates GamingInputDeviceInfo objects with device information AND live Gaming Input gamepad objects
        /// • Logs detailed device properties using Debug.WriteLine for diagnostics
        /// • Requires Windows 10+ for Gaming Input API availability
        /// • Provides device capability information for modern gamepads
        /// • Keeps Gaming Input gamepads alive for immediate input reading
        /// • Is self-contained with minimal external dependencies
        ///
        /// IMPORTANT: The returned GamingInputDeviceInfo objects contain live Gaming Input gamepads.
        /// Call Dispose() on each GamingInputDeviceInfo when no longer needed to free resources.
        /// </remarks>
        public List<GamingInputDeviceInfo> GetGamingInputDeviceList()
        {
            var stopwatch = Stopwatch.StartNew();
            var deviceList = new List<GamingInputDeviceInfo>();
            var deviceListDebugLines = new List<string>();
            int deviceListIndex = 0;

            try
            {
                Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
                    "DevicesGamingInput: Starting Gaming Input device enumeration...");
                
                // Check privilege level and adjust detection strategy accordingly
                bool isAdmin = IsRunningAsAdministrator();
                Debug.WriteLine($"DevicesGamingInput: Running as Administrator: {isAdmin}");
                
                // First check if Gaming Input is available on this system
                if (!IsGamingInputAvailable())
                {
                    Debug.WriteLine("DevicesGamingInput: Gaming Input is not available on this system");
                    deviceListDebugLines.Add("DevicesGamingInput: Gaming Input requires Windows 10+ and is not available on this system");
                    deviceListDebugLines.Add("\nDevicesGamingInput: Gaming Input gamepads found: 0, Online: 0, Offline/Failed: 0\n");
                    foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }
                    return deviceList; // Return empty list
                }
                
                Debug.WriteLine("DevicesGamingInput: Gaming Input API is available");
                
                // Use privilege-aware detection strategy
                List<GamingInputDeviceInfo> detectedDevices;
                if (isAdmin)
                {
                    Debug.WriteLine("DevicesGamingInput: Using Administrator-mode detection (full access)");
                    detectedDevices = GetGamepadsWithAdminPrivileges();
                }
                else
                {
                    Debug.WriteLine("DevicesGamingInput: Using normal-user detection (limited access)");
                    detectedDevices = GetGamepadsWithoutAdminPrivileges();
                }
                
                deviceList.AddRange(detectedDevices);
                Debug.WriteLine($"DevicesGamingInput: Privilege-aware detection found {detectedDevices.Count} gamepads");
                
                // Log detailed information for each detected device
                deviceListIndex = 0;
                foreach (var deviceInfo in detectedDevices)
                {
                    deviceListIndex++;
                    
                    // Log comprehensive device information for debugging
                    deviceListDebugLines.Add($"\n{deviceListIndex}. DevicesGamingInputInfo: " +
                        $"GamepadIndex: {deviceInfo.GamepadIndex}, " +
                        $"InstanceGuid: {deviceInfo.InstanceGuid}, " +
                        $"ProductGuid: {deviceInfo.ProductGuid}, " +
                        $"InstanceName: {deviceInfo.InstanceName}, " +
                        $"ProductName: {deviceInfo.ProductName}, " +
                        $"DeviceType: {deviceInfo.DeviceType}, " +
                        $"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
                        $"Timestamp: {deviceInfo.LastTimestamp}, " +
                        $"VidPidString: {deviceInfo.VidPidString}, " +
                        $"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
                        $"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

                    deviceListDebugLines.Add($"DevicesGamingInputInfo Capabilities: " +
                        $"AxeCount: {deviceInfo.AxeCount}, " +
                        $"ButtonCount: {deviceInfo.ButtonCount}, " +
                        $"PovCount: {deviceInfo.PovCount}, " +
                        $"HasForceFeedback: {deviceInfo.HasForceFeedback}, " +
                        $"SupportsVibration: {deviceInfo.SupportsVibration}, " +
                        $"SupportsTriggerRumble: {deviceInfo.SupportsTriggerRumble}");
                }
                
                // Provide user guidance based on results and privilege level
                if (detectedDevices.Count == 0 && !isAdmin)
                {
                    Debug.WriteLine("DevicesGamingInput: No devices found in normal user mode.");
                    Debug.WriteLine("DevicesGamingInput: RECOMMENDATION: Try running as Administrator for full GamingInput access.");
                    Debug.WriteLine("DevicesGamingInput: Right-click the application and select 'Run as administrator'.");
                    deviceListDebugLines.Add("DevicesGamingInput: No GamingInput devices detected in normal user mode.");
                    deviceListDebugLines.Add("DevicesGamingInput: For full GamingInput support, run as Administrator.");
                }
                else if (detectedDevices.Count == 0 && isAdmin)
                {
                    Debug.WriteLine("DevicesGamingInput: No devices found even with Administrator privileges.");
                    Debug.WriteLine("DevicesGamingInput: This indicates no GamingInput-compatible controllers are connected.");
                    deviceListDebugLines.Add("DevicesGamingInput: No GamingInput devices found (Administrator mode).");
                    deviceListDebugLines.Add("DevicesGamingInput: Controllers may only support DirectInput/XInput.");
                }
                
                // Only use legacy method if privilege-aware detection found nothing
                if (detectedDevices.Count == 0)
                {
                    Debug.WriteLine("DevicesGamingInput: Trying legacy detection as fallback...");
                    var gamepads = Gamepad.Gamepads;
                    Debug.WriteLine($"DevicesGamingInput: Legacy check found {gamepads.Count} Gaming Input gamepads");
                    
                    for (int gamepadIndex = 0; gamepadIndex < gamepads.Count; gamepadIndex++)
                {
                    try
                    {
                        var gamepad = gamepads[gamepadIndex];
                        
                        // Test if gamepad is functional by attempting to get a reading
                        GamepadReading reading;
                        bool isWorking = SafeGetGamepadReading(gamepad, out reading);
                        
                        if (isWorking)
                        {
                            // Create GamingInputDeviceInfo object with Gaming Input information
                            var deviceInfo = new GamingInputDeviceInfo
                            {
                                // Generate unique GUID for this Gaming Input gamepad
                                InstanceGuid = GenerateGamingInputGuid(gamepadIndex),
                                InstanceName = $"Gaming Input Gamepad {gamepadIndex + 1}",
                                ProductGuid = GenerateGamingInputProductGuid(),
                                ProductName = "Gaming Input Gamepad",
                                DeviceType = (int)SharpDX.DirectInput.DeviceType.Gamepad,
                                DeviceSubtype = 1, // Standard gamepad
                                Usage = 0x05, // Game Controls
                                UsagePage = 0x01, // Generic Desktop
                                DeviceTypeName = "Gaming Input Gamepad",
                                GamepadIndex = gamepadIndex,
                                GamingInputDevice = gamepad,
                                IsOnline = true,
                                LastTimestamp = (uint)reading.Timestamp
                            };
                            
                            // Gaming Input doesn't provide direct VID/PID access - use generic values
                            deviceInfo.VendorId = 0x045E; // Microsoft (Gaming Input standard)
                            deviceInfo.ProductId = 0x02FF; // Gaming Input Gamepad (generic)
                            
                            // Set standard Gaming Input capabilities
                            deviceInfo.AxeCount = 6; // Left Stick X/Y, Right Stick X/Y, Left/Right Triggers
                            deviceInfo.ButtonCount = 16; // Gaming Input supports up to 16 buttons
                            deviceInfo.PovCount = 1; // D-Pad as POV
                            deviceInfo.HasForceFeedback = true; // Gaming Input supports advanced vibration
                            
                            // Try to determine vibration capabilities
                            try
                            {
                                // Gaming Input supports advanced vibration features
                                deviceInfo.SupportsVibration = true;
                                deviceInfo.SupportsTriggerRumble = true; // Gaming Input exclusive feature
                                deviceInfo.HasForceFeedback = true;
                                
                                // Set known Gaming Input gamepad information
                                deviceInfo.DeviceSubtype = 1; // Standard gamepad
                                
                                // Use Gaming Input standard VID/PID
                                deviceInfo.VendorId = 0x045E; // Microsoft
                                deviceInfo.ProductId = 0x02FF; // Gaming Input Gamepad
                            }
                            catch (Exception capEx)
                            {
                                Debug.WriteLine($"DevicesGamingInput: Could not get vibration info for gamepad {gamepadIndex}: {capEx.Message}");
                                
                                // Set default Gaming Input gamepad VID/PID
                                deviceInfo.VendorId = 0x045E; // Microsoft
                                deviceInfo.ProductId = 0x02FF; // Gaming Input Gamepad
                                deviceInfo.SupportsVibration = true; // Assume supported
                                deviceInfo.SupportsTriggerRumble = true;
                            }
                            
                            // Generate device identification strings
                            deviceInfo.DeviceId = $"GamingInput\\Gamepad_{gamepadIndex:D2}";
                            deviceInfo.InterfacePath = $"\\\\?\\GamingInput#{gamepadIndex}";
                            deviceInfo.HardwareIds = $"GamingInput\\VID_{deviceInfo.VendorId:X4}&PID_{deviceInfo.ProductId:X4}";
                            
                            // Set driver and firmware information (Gaming Input doesn't provide real values)
                            deviceInfo.DriverVersion = GetGamingInputVersion();
                            deviceInfo.HardwareRevision = 1;
                            deviceInfo.FirmwareRevision = 1;
                            
                            // Set Windows device class information
                            deviceInfo.ClassGuid = GenerateGamingInputClassGuid();
                            deviceInfo.ParentDeviceId = "GamingInput\\Root";

                            deviceListIndex++;

                            // Log comprehensive device information for debugging
                            deviceListDebugLines.Add($"\n{deviceListIndex}. DevicesGamingInputInfo: " +
                                $"GamepadIndex: {deviceInfo.GamepadIndex}, " +
                                $"InstanceGuid: {deviceInfo.InstanceGuid}, " +
                                $"ProductGuid: {deviceInfo.ProductGuid}, " +
                                $"InstanceName: {deviceInfo.InstanceName}, " +
                                $"ProductName: {deviceInfo.ProductName}, " +
                                $"DeviceType: {deviceInfo.DeviceType}, " +
                                $"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
                                $"Timestamp: {deviceInfo.LastTimestamp}, " +
                                $"VidPidString: {deviceInfo.VidPidString}, " +
                                $"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
                                $"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

                            deviceListDebugLines.Add($"DevicesGamingInputInfo Capabilities: " +
                                $"AxeCount: {deviceInfo.AxeCount}, " +
                                $"ButtonCount: {deviceInfo.ButtonCount}, " +
                                $"PovCount: {deviceInfo.PovCount}, " +
                                $"HasForceFeedback: {deviceInfo.HasForceFeedback}, " +
                                $"SupportsVibration: {deviceInfo.SupportsVibration}, " +
                                $"SupportsTriggerRumble: {deviceInfo.SupportsTriggerRumble}");
                            
                            // Add device to the final list
                            deviceList.Add(deviceInfo);
                        }
                        else
                        {
                            Debug.WriteLine($"DevicesGamingInput: Gamepad {gamepadIndex} is not responding");
                        }
                    }
                    catch (Exception gamepadEx)
                    {
                        Debug.WriteLine($"DevicesGamingInput: Error processing Gaming Input gamepad {gamepadIndex}: {gamepadEx.Message}");
                    }
                    }
                }
                
                // Generate summary statistics for device enumeration results
                var connectedCount = deviceList.Count;
                var offlineCount = deviceList.Count(d => !d.IsOnline);
                var vibrationCount = deviceList.Count(d => d.SupportsVibration);
                var triggerRumbleCount = deviceList.Count(d => d.SupportsTriggerRumble);

                stopwatch.Stop();

                deviceListDebugLines.Add($"\nDevicesGamingInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
                    $"Gaming Input gamepads found: {connectedCount}, " +
                    $"Online: {connectedCount - offlineCount}, " +
                    $"Offline/Failed: {offlineCount}, " +
                    $"With Vibration: {vibrationCount}, " +
                    $"With Trigger Rumble: {triggerRumbleCount}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Fatal error during Gaming Input device enumeration: {ex.Message}");
                Debug.WriteLine($"DevicesGamingInput: Stack trace: {ex.StackTrace}");
            }

            foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }

            return deviceList;
        }
        
        /// <summary>
        /// Disposes all Gaming Input devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of GamingInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<GamingInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;
            
            Debug.WriteLine($"DevicesGamingInput: Disposing {deviceList.Count} Gaming Input devices...");
            
            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo != null)
                    {
                        Debug.WriteLine($"DevicesGamingInput: Disposing device - {deviceInfo.InstanceName}");
                        deviceInfo.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DevicesGamingInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
                }
            }
            
            Debug.WriteLine("DevicesGamingInput: All Gaming Input devices disposed.");
        }
        
        /// <summary>
        /// Enhanced GamingInput availability check with detailed diagnostics.
        /// </summary>
        /// <returns>True if Gaming Input is available and functional</returns>
        private bool IsGamingInputAvailable()
        {
            try
            {
                // IMPORTANT: Don't rely on Environment.OSVersion for Windows 10+ detection
                // Windows often reports incorrect versions due to compatibility manifests
                // The real test is whether the Gaming Input API is accessible
                
                Debug.WriteLine($"DevicesGamingInput: Detected OS version: {Environment.OSVersion.Version}");
                Debug.WriteLine($"DevicesGamingInput: Platform: {Environment.OSVersion.Platform}");
                Debug.WriteLine($"DevicesGamingInput: Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
                Debug.WriteLine($"DevicesGamingInput: Is64BitProcess: {Environment.Is64BitProcess}");
                Debug.WriteLine($"DevicesGamingInput: CLR Version: {Environment.Version}");
                Debug.WriteLine($"DevicesGamingInput: Running as Administrator: {IsRunningAsAdministrator()}");
                Debug.WriteLine("DevicesGamingInput: Testing Gaming Input API accessibility (this is the definitive test)...");
                
                // Test 1: Check if Windows.Gaming.Input namespace is available
                var gamingInputType = typeof(Windows.Gaming.Input.Gamepad);
                Debug.WriteLine($"DevicesGamingInput: Gaming Input type loaded: {gamingInputType.FullName}");
                
                // Test 2: Try to access the Gamepads collection
                var gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                Debug.WriteLine($"DevicesGamingInput: Gaming Input API is accessible! Found {gamepads.Count} gamepads");
                
                // Test 2.5: Check if no gamepads found and provide guidance
                if (gamepads.Count == 0)
                {
                    Debug.WriteLine("DevicesGamingInput: No gamepads found in initial check.");
                    if (!IsRunningAsAdministrator())
                    {
                        Debug.WriteLine("DevicesGamingInput: WARNING - Running without Administrator privileges.");
                        Debug.WriteLine("DevicesGamingInput: GamingInput may require elevated privileges for device access.");
                        Debug.WriteLine("DevicesGamingInput: Consider running as Administrator if devices are not detected.");
                    }
                    else
                    {
                        Debug.WriteLine("DevicesGamingInput: Running with Administrator privileges - privilege level is not the issue.");
                    }
                }
                
                // Test 3: Try to register for gamepad events (this often fails if GamingInput isn't properly available)
                try
                {
                    Windows.Gaming.Input.Gamepad.GamepadAdded += OnGamepadAdded;
                    Windows.Gaming.Input.Gamepad.GamepadRemoved += OnGamepadRemoved;
                    Debug.WriteLine("DevicesGamingInput: Successfully registered for gamepad events");
                    
                    // Immediately unregister to avoid memory leaks
                    Windows.Gaming.Input.Gamepad.GamepadAdded -= OnGamepadAdded;
                    Windows.Gaming.Input.Gamepad.GamepadRemoved -= OnGamepadRemoved;
                }
                catch (Exception eventEx)
                {
                    Debug.WriteLine($"DevicesGamingInput: Could not register for events (may indicate limited access): {eventEx.Message}");
                }
                
                // Skip Windows Runtime activation test as it's not needed for GamingInput
                // and causes exceptions in non-UWP applications
                
                return true;
            }
            catch (System.TypeLoadException ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Gaming Input types not available: {ex.Message}");
                Debug.WriteLine("DevicesGamingInput: This indicates Windows.Gaming.Input is not accessible");
                return false;
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Gaming Input assemblies not found: {ex.Message}");
                Debug.WriteLine("DevicesGamingInput: Required Windows Runtime components are missing");
                return false;
            }
            catch (System.PlatformNotSupportedException ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Platform not supported: {ex.Message}");
                Debug.WriteLine("DevicesGamingInput: Gaming Input requires Windows 10 1607+ or Windows 11");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Gaming Input API is not accessible: {ex.Message}");
                Debug.WriteLine($"DevicesGamingInput: Exception type: {ex.GetType().Name}");
                Debug.WriteLine("DevicesGamingInput: This could mean:");
                Debug.WriteLine("  • Gaming Input is not available on this system");
                Debug.WriteLine("  • Windows version is too old (requires Windows 10 1607+)");
                Debug.WriteLine("  • UWP runtime components are missing");
                Debug.WriteLine("  • Application manifest doesn't declare Windows 10+ compatibility");
                Debug.WriteLine("  • Application is running in compatibility mode");
                Debug.WriteLine("  • Windows Gaming Input service is disabled");
                return false;
            }
        }
        
        /// <summary>
        /// Event handler for gamepad added events (used for testing event registration).
        /// </summary>
        private void OnGamepadAdded(object sender, Windows.Gaming.Input.Gamepad gamepad)
        {
            // This is just for testing event registration - actual event handling would go here
        }
        
        /// <summary>
        /// Event handler for gamepad removed events (used for testing event registration).
        /// </summary>
        private void OnGamepadRemoved(object sender, Windows.Gaming.Input.Gamepad gamepad)
        {
            // This is just for testing event registration - actual event handling would go here
        }
        
        /// <summary>
        /// Safely gets gamepad reading with proper error handling.
        /// </summary>
        /// <param name="gamepad">The gamepad to get reading from</param>
        /// <param name="reading">Output reading</param>
        /// <returns>True if gamepad is functional and reading was retrieved</returns>
        private bool SafeGetGamepadReading(Gamepad gamepad, out GamepadReading reading)
        {
            reading = new GamepadReading();
            
            try
            {
                reading = gamepad.GetCurrentReading();
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Win32Exception getting gamepad reading: {ex.Message}");
                return false;
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"DevicesGamingInput: UnauthorizedAccessException getting gamepad reading (UWP restriction): {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Unexpected error getting gamepad reading: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Generates a unique GUID for a Gaming Input gamepad.
        /// </summary>
        /// <param name="gamepadIndex">The Gaming Input gamepad index</param>
        /// <returns>Unique GUID for the gamepad</returns>
        private Guid GenerateGamingInputGuid(int gamepadIndex)
        {
            // Generate consistent GUID based on Gaming Input gamepad index
            // Using a base GUID and modifying the last bytes with gamepad index
            var baseBytes = new byte[] { 0x47, 0x41, 0x4D, 0x49, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            baseBytes[15] = (byte)gamepadIndex;
            return new Guid(baseBytes);
        }
        
        /// <summary>
        /// Generates a product GUID for Gaming Input gamepads.
        /// </summary>
        /// <returns>Product GUID for Gaming Input gamepads</returns>
        private Guid GenerateGamingInputProductGuid()
        {
            // Standard Gaming Input gamepad product GUID
            return new Guid("47414D49-4E47-5052-4F44-000000000000"); // "GAMINGPROD"
        }
        
        /// <summary>
        /// Generates a class GUID for Gaming Input devices.
        /// </summary>
        /// <returns>Class GUID for Gaming Input devices</returns>
        private Guid GenerateGamingInputClassGuid()
        {
            // Gaming Input device class GUID
            return new Guid("47414D49-4E47-434C-4153-000000000000"); // "GAMINGCLAS"
        }
        
        /// <summary>
        /// Gets the Gaming Input API version information.
        /// </summary>
        /// <returns>Gaming Input version as integer</returns>
        private int GetGamingInputVersion()
        {
            try
            {
                // Gaming Input is part of Windows 10+ UWP APIs
                // Don't rely on OS version reporting - use API availability instead
                if (IsGamingInputAvailable())
                {
                    // Gaming Input is available, return current API version
                    return 0x0100; // Version 1.0
                }
                
                return 0x0000; // Not available
            }
            catch
            {
                return 0x0100; // Default to version 1.0 if available
            }
        }
        
        /// <summary>
        /// Gets comprehensive Gaming Input system information for diagnostics.
        /// </summary>
        /// <returns>String containing detailed Gaming Input diagnostic information</returns>
        public string GetGamingInputDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            
            try
            {
                var osVersion = Environment.OSVersion.Version;
                var isAvailable = IsGamingInputAvailable();
                
                info.AppendLine("=== Gaming Input Diagnostic Information ===");
                info.AppendLine($"Gaming Input Available: {isAvailable}");
                info.AppendLine($"Reported OS Version: {Environment.OSVersion}");
                info.AppendLine($"Note: OS version may be incorrect due to compatibility manifests");
                info.AppendLine($"Gaming Input API Test: {(isAvailable ? "PASSED" : "FAILED")}");
                info.AppendLine($"Gaming Input Version: {GetGamingInputVersion():X4}");
                info.AppendLine();
                
                if (isAvailable)
                {
                    try
                    {
                        var gamepads = Gamepad.Gamepads;
                        info.AppendLine($"Connected Gamepads: {gamepads.Count}");
                        info.AppendLine();
                        
                        for (int i = 0; i < gamepads.Count; i++)
                        {
                            var gamepad = gamepads[i];
                            info.AppendLine($"Gamepad {i + 1}:");
                            
                            try
                            {
                                // Try to get a reading to verify functionality
                                var reading = gamepad.GetCurrentReading();
                                info.AppendLine($"  Status: Functional");
                                info.AppendLine($"  Timestamp: {reading.Timestamp}");
                                info.AppendLine($"  Buttons: {reading.Buttons}");
                                info.AppendLine($"  Left Stick: ({reading.LeftThumbstickX:F2}, {reading.LeftThumbstickY:F2})");
                                info.AppendLine($"  Right Stick: ({reading.RightThumbstickX:F2}, {reading.RightThumbstickY:F2})");
                                info.AppendLine($"  Triggers: L={reading.LeftTrigger:F2}, R={reading.RightTrigger:F2}");
                                
                                // Check vibration support
                                try
                                {
                                    var vibration = gamepad.Vibration;
                                    info.AppendLine($"  Vibration Support: Available");
                                    info.AppendLine($"  Current Vibration: L={vibration.LeftMotor:F2}, R={vibration.RightMotor:F2}, LT={vibration.LeftTrigger:F2}, RT={vibration.RightTrigger:F2}");
                                }
                                catch
                                {
                                    info.AppendLine($"  Vibration Support: Unknown");
                                }
                            }
                            catch (Exception ex)
                            {
                                info.AppendLine($"  Status: Error - {ex.Message}");
                            }
                            
                            info.AppendLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        info.AppendLine($"Error enumerating gamepads: {ex.Message}");
                    }
                }
                else
                {
                    info.AppendLine("Gaming Input is not available on this system.");
                    info.AppendLine("Possible causes:");
                    info.AppendLine("  • Windows version too old (requires Windows 10 1607+ or Windows 11)");
                    info.AppendLine("  • Gaming Input API not accessible");
                    info.AppendLine("  • UWP runtime components missing");
                    info.AppendLine("  • Application manifest missing Gaming Input capability");
                    info.AppendLine("  • System compatibility issues");
                }
                
                info.AppendLine("Gaming Input Features:");
                info.AppendLine("  • Modern Windows 10+ API");
                info.AppendLine("  • Advanced vibration (including trigger rumble)");
                info.AppendLine("  • Enhanced controller support");
                info.AppendLine("  • UWP and Win32 compatibility");
                info.AppendLine("  • Separate trigger axes");
                info.AppendLine();
                
                info.AppendLine("Gaming Input Limitations:");
                info.AppendLine("  • Windows 10+ required");
                info.AppendLine("  • No background access (UWP restriction)");
                info.AppendLine("  • No Guide button access");
                info.AppendLine("  • Limited to modern controllers");
            }
            catch (Exception ex)
            {
                info.AppendLine($"Error getting Gaming Input diagnostic info: {ex.Message}");
            }
            
            return info.ToString();
        }
        
        /// <summary>
        /// Checks if the current process is running with Administrator privileges.
        /// </summary>
        /// <returns>True if running as Administrator</returns>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Could not determine Administrator status: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// GamingInput detection with Administrator privileges - full access and extended timeouts.
        /// </summary>
        /// <returns>List of detected gamepads using Administrator-level detection</returns>
        private List<GamingInputDeviceInfo> GetGamepadsWithAdminPrivileges()
        {
            var deviceList = new List<GamingInputDeviceInfo>();
            
            try
            {
                Debug.WriteLine("DevicesGamingInput: Starting Administrator-mode detection...");
                
                // Try immediate access first
                var gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                Debug.WriteLine($"DevicesGamingInput: [Admin] Immediate access found {gamepads.Count} gamepads");
                
                // With Administrator privileges, use more aggressive detection with longer timeouts
                if (gamepads.Count == 0)
                {
                    Debug.WriteLine("DevicesGamingInput: [Admin] No devices found immediately, trying with 50ms delay...");
                    System.Threading.Thread.Sleep(50);
                    gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                    Debug.WriteLine($"DevicesGamingInput: [Admin] After 50ms delay found {gamepads.Count} gamepads");
                    
                    if (gamepads.Count == 0)
                    {
                        Debug.WriteLine("DevicesGamingInput: [Admin] Still no devices, trying with additional 100ms delay...");
                        System.Threading.Thread.Sleep(100);
                        gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                        Debug.WriteLine($"DevicesGamingInput: [Admin] After total 150ms delay found {gamepads.Count} gamepads");
                        
                        // Final attempt with extended delay for Administrator mode
                        if (gamepads.Count == 0)
                        {
                            Debug.WriteLine("DevicesGamingInput: [Admin] Final attempt with additional 100ms delay...");
                            System.Threading.Thread.Sleep(100);
                            gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                            Debug.WriteLine($"DevicesGamingInput: [Admin] After total 250ms delay found {gamepads.Count} gamepads");
                        }
                    }
                }
                
                // Process found gamepads
                for (int i = 0; i < gamepads.Count; i++)
                {
                    var gamepad = gamepads[i];
                    if (ProcessGamepadForDetectionOptimized(gamepad, i, deviceList))
                    {
                        Debug.WriteLine($"DevicesGamingInput: [Admin] Successfully processed gamepad {i}");
                    }
                }
                
                Debug.WriteLine($"DevicesGamingInput: Administrator-mode detection completed with {deviceList.Count} gamepads");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Administrator-mode detection failed: {ex.Message}");
            }
            
            return deviceList;
        }
        
        /// <summary>
        /// GamingInput detection without Administrator privileges - limited access with quick timeouts.
        /// </summary>
        /// <returns>List of detected gamepads using normal user detection</returns>
        private List<GamingInputDeviceInfo> GetGamepadsWithoutAdminPrivileges()
        {
            var deviceList = new List<GamingInputDeviceInfo>();
            
            try
            {
                Debug.WriteLine("DevicesGamingInput: Starting normal-user detection...");
                
                // Try immediate access first
                var gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                Debug.WriteLine($"DevicesGamingInput: [User] Immediate access found {gamepads.Count} gamepads");
                
                // Without Administrator privileges, use minimal delays to avoid hanging
                if (gamepads.Count == 0)
                {
                    Debug.WriteLine("DevicesGamingInput: [User] No devices found immediately, trying with 25ms delay...");
                    System.Threading.Thread.Sleep(25);
                    gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                    Debug.WriteLine($"DevicesGamingInput: [User] After 25ms delay found {gamepads.Count} gamepads");
                    
                    // Only one retry in normal user mode to avoid delays
                    if (gamepads.Count == 0)
                    {
                        Debug.WriteLine("DevicesGamingInput: [User] Still no devices, final attempt with 25ms delay...");
                        System.Threading.Thread.Sleep(25);
                        gamepads = Windows.Gaming.Input.Gamepad.Gamepads;
                        Debug.WriteLine($"DevicesGamingInput: [User] After total 50ms delay found {gamepads.Count} gamepads");
                    }
                }
                
                // If no devices found in normal user mode, provide guidance
                if (gamepads.Count == 0)
                {
                    Debug.WriteLine("DevicesGamingInput: [User] No gamepads detected in normal user mode.");
                    Debug.WriteLine("DevicesGamingInput: [User] GamingInput may require Administrator privileges for device access.");
                    Debug.WriteLine("DevicesGamingInput: [User] DirectInput and XInput detection will still work normally.");
                }
                
                // Process found gamepads
                for (int i = 0; i < gamepads.Count; i++)
                {
                    var gamepad = gamepads[i];
                    if (ProcessGamepadForDetectionOptimized(gamepad, i, deviceList))
                    {
                        Debug.WriteLine($"DevicesGamingInput: [User] Successfully processed gamepad {i}");
                    }
                }
                
                Debug.WriteLine($"DevicesGamingInput: Normal-user detection completed with {deviceList.Count} gamepads");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Normal-user detection failed: {ex.Message}");
                Debug.WriteLine("DevicesGamingInput: [User] This is expected behavior - GamingInput often requires elevation.");
            }
            
            return deviceList;
        }
        
        /// <summary>
        /// Optimized method to process a gamepad for detection with minimal overhead.
        /// </summary>
        /// <param name="gamepad">The gamepad to process</param>
        /// <param name="index">The gamepad index</param>
        /// <param name="deviceList">The device list to add to</param>
        /// <returns>True if gamepad was successfully processed</returns>
        private bool ProcessGamepadForDetectionOptimized(Windows.Gaming.Input.Gamepad gamepad, int index, List<GamingInputDeviceInfo> deviceList)
        {
            try
            {
                // Test if gamepad is functional by attempting to get a reading
                GamepadReading reading;
                bool isWorking = SafeGetGamepadReading(gamepad, out reading);
                
                if (isWorking)
                {
                    // Create GamingInputDeviceInfo object with optimized settings
                    var deviceInfo = new GamingInputDeviceInfo
                    {
                        InstanceGuid = GenerateGamingInputGuid(index),
                        InstanceName = $"Gaming Input Gamepad {index + 1}",
                        ProductGuid = GenerateGamingInputProductGuid(),
                        ProductName = "Gaming Input Gamepad",
                        DeviceType = (int)SharpDX.DirectInput.DeviceType.Gamepad,
                        DeviceSubtype = 1,
                        Usage = 0x05,
                        UsagePage = 0x01,
                        DeviceTypeName = "Gaming Input Gamepad",
                        GamepadIndex = index,
                        GamingInputDevice = gamepad,
                        IsOnline = true,
                        LastTimestamp = (uint)reading.Timestamp,
                        VendorId = 0x045E,
                        ProductId = 0x02FF,
                        AxeCount = 6,
                        ButtonCount = 16,
                        PovCount = 1,
                        HasForceFeedback = true,
                        SupportsVibration = true,
                        SupportsTriggerRumble = true
                    };
                    
                    // Generate device identification strings
                    deviceInfo.DeviceId = $"GamingInput\\Gamepad_{index:D2}";
                    deviceInfo.InterfacePath = $"\\\\?\\GamingInput#{index}";
                    deviceInfo.HardwareIds = $"GamingInput\\VID_{deviceInfo.VendorId:X4}&PID_{deviceInfo.ProductId:X4}";
                    deviceInfo.DriverVersion = GetGamingInputVersion();
                    deviceInfo.HardwareRevision = 1;
                    deviceInfo.FirmwareRevision = 1;
                    deviceInfo.ClassGuid = GenerateGamingInputClassGuid();
                    deviceInfo.ParentDeviceId = "GamingInput\\Root";
                    
                    deviceList.Add(deviceInfo);
                    return true;
                }
                else
                {
                    Debug.WriteLine($"DevicesGamingInput: Gamepad {index} is not responding");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesGamingInput: Error processing gamepad {index}: {ex.Message}");
                return false;
            }
        }
    }
}
