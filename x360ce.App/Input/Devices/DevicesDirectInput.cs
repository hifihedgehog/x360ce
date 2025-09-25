using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// DirectInput device container with both device information and the actual device object.
    /// Contains comprehensive device metadata plus the live DirectInput device for input reading.
    /// </summary>
    public class DirectInputDeviceInfo : IDisposable
    {
        public Guid InstanceGuid { get; set; }
        public string InstanceName { get; set; }
        public Guid ProductGuid { get; set; }
        public string ProductName { get; set; }
        public DeviceType DeviceType { get; set; }
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
        
        /// <summary>
        /// The actual DirectInput device object for reading input.
        /// Can be Mouse, Keyboard, or Joystick depending on device type.
        /// </summary>
        public Device DirectInputDevice { get; set; }
        
        /// <summary>
        /// Display name combining instance ID and name for easy identification.
        /// </summary>
        public string DisplayName => $"{InstanceGuid.ToString().Substring(0, 8)} - {InstanceName}";
        
        /// <summary>
        /// VID/PID string in standard format for hardware identification.
        /// </summary>
        public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
        
        /// <summary>
        /// Dispose the DirectInput device when no longer needed.
        /// </summary>
        public void Dispose()
        {
            DirectInputDevice?.Dispose();
            DirectInputDevice = null;
        }
    }

    /// <summary>
    /// DirectInput device enumeration and management class.
    /// Self-contained implementation with minimal external dependencies.
    /// Provides functionality to discover and list DirectInput devices including gamepads, keyboards, and mice.
    /// Returns live DirectInput device objects that can be used for input reading.
    /// </summary>
    internal class DevicesDirectInput
    {
        /// <summary>
        /// Creates a public list of DirectInput devices (gamepads, keyboards, mice) with live device objects and logs their properties.
        /// This method enumerates all available DirectInput devices and outputs detailed information for debugging.
        /// </summary>
        /// <returns>List of DirectInputDeviceInfo objects containing both device information and live DirectInput device objects</returns>
        /// <remarks>
        /// This method performs comprehensive DirectInput device enumeration:
        /// • Discovers all DirectInput-compatible devices (gamepads, keyboards, mice)
        /// • Creates DirectInputDeviceInfo objects with device information AND live DirectInput device objects
        /// • Logs detailed device properties using Debug.WriteLine for diagnostics
        /// • Filters devices by type and availability
        /// • Provides device capability information where available
        /// • Keeps DirectInput devices alive for immediate input reading
        /// • Is self-contained with minimal external dependencies
        ///
        /// IMPORTANT: The returned DirectInputDeviceInfo objects contain live DirectInput devices.
        /// Call Dispose() on each DirectInputDeviceInfo when no longer needed to free resources.
        /// </remarks>
        public List<DirectInputDeviceInfo> GetDirectInputDeviceList()
        {
            var stopwatch = Stopwatch.StartNew();
            var deviceList = new List<DirectInputDeviceInfo>();
            var deviceListDebugLines = new List<string>();
            int deviceListIndex = 0;

            try
            {
                Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
                    "DeviceDirectInput: Starting DirectInput device enumeration...");
                
                using (var directInput = new DirectInput())
                {
                    // Enumerate all DirectInput devices and filter to only input devices (gamepads, keyboards, mice)
                    // This excludes non-input devices like sound cards, network adapters, etc.
                    var allDevices = directInput.GetDevices(DeviceClass.All, DeviceEnumerationFlags.AllDevices);
                    var inputDevices = allDevices.Where(d => IsInputDevice(d.Type)).ToList();
                    
                    Debug.WriteLine($"DeviceDirectInput: Found {inputDevices.Count} input devices");
                    
                    foreach (var deviceInstance in inputDevices)
                    {
                        try
                        {
                            // Create DirectInputDeviceInfo object with basic device instance information
                            var deviceInfo = new DirectInputDeviceInfo
                            {
                                InstanceGuid = deviceInstance.InstanceGuid,
                                InstanceName = deviceInstance.InstanceName,
                                ProductGuid = deviceInstance.ProductGuid,
                                ProductName = deviceInstance.ProductName,
                                DeviceType = deviceInstance.Type,
                                DeviceSubtype = deviceInstance.Subtype,
                                Usage = (int)deviceInstance.Usage,
                                UsagePage = (int)deviceInstance.UsagePage,
                                DeviceTypeName = GetDeviceTypeName(deviceInstance.Type)
                            };
                            
                            // Attempt to create the actual DirectInput device object to access capabilities and properties
                            // This is required to get detailed device information like button/axis counts
                            Device device = null;
                            try
                            {
                                // Create appropriate DirectInput device object based on device type
                                switch (deviceInstance.Type)
                                {
                                    case DeviceType.Mouse:
                                        device = new Mouse(directInput);
                                        break;
                                    case DeviceType.Keyboard:
                                        device = new Keyboard(directInput);
                                        break;
                                    case DeviceType.Joystick:
                                    case DeviceType.Gamepad:
                                    case DeviceType.FirstPerson:
                                    case DeviceType.Flight:
                                    case DeviceType.Driving:
                                        device = new Joystick(directInput, deviceInstance.InstanceGuid);
                                        break;
                                    default:
                                        // Handle unexpected device types that passed IsInputDevice() filter but aren't in our switch
                                        // Mark as offline since we can't create a DirectInput object for unknown types
                                        Debug.WriteLine($"DeviceDirectInput: Unexpected device type {deviceInstance.Type} - {deviceInstance.InstanceName} (this should not happen)");
                                        deviceInfo.IsOnline = false;
                                        deviceList.Add(deviceInfo);
                                        continue;
                                }
                                
                                if (device != null)
                                {
                                    // Extract device capabilities (button count, axis count, force feedback support, etc.)
                                    var capabilities = device.Capabilities;
                                    
                                    // Initialize variables for additional device identification information
                                    string interfacePath = "";
                                    int vendorId = 0;
                                    int productId = 0;
                                    
                                    try
                                    {
                                        // Extract extended properties only available for joystick-type devices
                                        // Mouse and Keyboard devices don't have these additional properties
                                        if (device is Joystick joystick)
                                        {
                                            // Extract hardware identification information using multiple methods
                                            try
                                            {
                                                // Get Windows device interface path (contains hardware IDs)
                                                interfacePath = joystick.Properties.InterfacePath ?? "";
                                                
                                                // Method 1: Get VID/PID directly from DirectInput properties (most reliable)
                                                vendorId = joystick.Properties.VendorId;
                                                productId = joystick.Properties.ProductId;
                                                
                                                // Method 2: Parse VID/PID from interface path if DirectInput properties are empty
                                                if (vendorId == 0 && productId == 0 && !string.IsNullOrEmpty(interfacePath))
                                                {
                                                    var vidPid = ExtractVidPidFromPath(interfacePath);
                                                    vendorId = vidPid.vid;
                                                    productId = vidPid.pid;
                                                }
                                                
                                                // Method 3: Extract VID/PID from ProductGuid as last resort
                                                // Some devices encode hardware IDs in the GUID format
                                                if (vendorId == 0 && productId == 0)
                                                {
                                                    var guidString = deviceInfo.ProductGuid.ToString("N");
                                                    if (guidString.Length >= 8)
                                                    {
                                                        // GUID format: first 4 hex chars = PID, next 4 hex chars = VID
                                                        if (int.TryParse(guidString.Substring(0, 4), System.Globalization.NumberStyles.HexNumber, null, out int guidPid) &&
                                                            int.TryParse(guidString.Substring(4, 4), System.Globalization.NumberStyles.HexNumber, null, out int guidVid))
                                                        {
                                                            vendorId = guidVid;
                                                            productId = guidPid;
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception vidPidEx)
                                            {
                                                Debug.WriteLine($"DeviceDirectInput: Could not extract VID/PID for {deviceInfo.InstanceName}: {vidPidEx.Message}");
                                            }
                                            
                                            // Extract Windows device class information and device path details
                                            try
                                            {
                                                // Attempt to get Windows device class GUID from DirectInput properties
                                                try
                                                {
                                                    deviceInfo.ClassGuid = joystick.Properties.ClassGuid;
                                                }
                                                catch
                                                {
                                                    // ClassGuid property may not be available on all systems/devices
                                                    deviceInfo.ClassGuid = Guid.Empty;
                                                }
                                                
                                                // Extract device hardware ID from the interface path for device identification
                                                if (!string.IsNullOrEmpty(interfacePath))
                                                {
                                                    deviceInfo.DeviceId = ExtractDeviceIdFromPath(interfacePath);
                                                }
                                            }
                                            catch (Exception extraEx)
                                            {
                                                Debug.WriteLine($"DeviceDirectInput: Could not get extra properties for {deviceInfo.InstanceName}: {extraEx.Message}");
                                            }
                                        }
                                    }
                                    catch (Exception propsEx)
                                    {
                                        Debug.WriteLine($"DeviceDirectInput: Could not get extended properties for {deviceInfo.InstanceName}: {propsEx.Message}");
                                    }

                                    // Populate device information with capabilities and extracted properties
                                    deviceInfo.DirectInputDevice = device; // Store live DirectInput device for input reading
                                    deviceInfo.AxeCount = capabilities.AxeCount; // Number of analog axes (X, Y, Z, etc.)
                                    deviceInfo.ButtonCount = capabilities.ButtonCount; // Number of buttons/triggers
                                    deviceInfo.PovCount = capabilities.PovCount; // Number of Point-of-View (D-pad) controls
                                    deviceInfo.HasForceFeedback = capabilities.Flags.HasFlag(DeviceFlags.ForceFeedback); // Rumble/vibration support
                                    deviceInfo.DriverVersion = capabilities.DriverVersion; // DirectInput driver version
                                    deviceInfo.HardwareRevision = capabilities.HardwareRevision; // Hardware revision from device
                                    deviceInfo.FirmwareRevision = capabilities.FirmwareRevision; // Firmware revision from device
                                    deviceInfo.InterfacePath = interfacePath ?? ""; // Windows device interface path
                                    deviceInfo.VendorId = vendorId; // USB Vendor ID (VID)
                                    deviceInfo.ProductId = productId; // USB Product ID (PID)
                                    
                                    // Set properties not available through DirectInput API to empty values
                                    deviceInfo.HardwareIds = ""; // DirectInput doesn't provide Windows hardware IDs
                                    deviceInfo.ParentDeviceId = ""; // DirectInput doesn't provide parent device information
                                    deviceInfo.IsOnline = true; // Mark device as successfully initialized and ready for input

                                    deviceListIndex++;

                                    // Log comprehensive device information for debugging and diagnostics
                                    deviceListDebugLines.Add($"\n{deviceListIndex}. DeviceDirectInputInfo: " +
                                    $"InstanceGuid: {deviceInfo.InstanceGuid}, " +
                                    $"ProductGuid: {deviceInfo.ProductGuid}, " +
                                    $"InstanceName: {deviceInfo.InstanceName}, " +
                                    $"ProductName: {deviceInfo.ProductName}, " +
                                    $"DeviceType: {deviceInfo.DeviceType}, " +
                                    $"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
                                    $"DeviceSubtype: {deviceInfo.DeviceSubtype}, " +
                                    $"Usage: {deviceInfo.Usage}, " +
                                    $"UsagePage: {deviceInfo.UsagePage}, " +
                                    $"DriverVersion: {deviceInfo.DriverVersion}, " +
                                    $"HardwareRevision: {deviceInfo.HardwareRevision}, " +
                                    $"FirmwareRevision: {deviceInfo.FirmwareRevision}, " +
                                    $"InterfacePath: {deviceInfo.InterfacePath}, " +
                                    $"VidPidString: {deviceInfo.VidPidString}, " +
                                    $"VendorId: {vendorId} (0x{vendorId:X4}), " +
                                    $"ProductId: {productId} (0x{productId:X4})");

                                    deviceListDebugLines.Add($"DeviceDirectInputInfo Capabilities: " +
                                    $"AxeCount: {deviceInfo.AxeCount}, " +
                                    $"ButtonCount: {deviceInfo.ButtonCount}, " +
                                    $"PovCount: {deviceInfo.PovCount}, " +
                                    $"HasForceFeedback: {deviceInfo.HasForceFeedback}");
                                    
                                    // Note: DirectInput device object is kept alive in deviceInfo.DirectInputDevice
                                    // It will be properly disposed when DirectInputDeviceInfo.Dispose() is called
                                }
                            }
                            catch (Exception deviceEx)
                            {
                                Debug.WriteLine($"DeviceDirectInput: Error creating device {deviceInstance.InstanceName}: {deviceEx.Message}");
                                
                                // Add device to list even when DirectInput object creation fails
                                // This provides visibility for troubleshooting and shows all detected devices
                                deviceInfo.DirectInputDevice = null; // No usable device object available
                                deviceInfo.IsOnline = false; // Mark as offline since device cannot be used for input
                                
                                // Note: Don't dispose here - any partially created device will be cleaned up
                                // when DirectInputDeviceInfo.Dispose() is called by the consumer
                            }
                            
                            // Add device information to the final list (both working and failed devices)
                            deviceList.Add(deviceInfo);
                        }
                        catch (Exception instanceEx)
                        {
                            Debug.WriteLine($"DeviceDirectInput: Error processing device instance: {instanceEx.Message}");
                        }
                    }
                }
                
                // Generate summary statistics for device enumeration results
                var gamepadCount = deviceList.Count(d => IsGamepadType(d.DeviceType));
                var keyboardCount = deviceList.Count(d => d.DeviceType == DeviceType.Keyboard);
                var mouseCount = deviceList.Count(d => d.DeviceType == DeviceType.Mouse);
                var offlineCount = deviceList.Count(d => !d.IsOnline);

                stopwatch.Stop();

                deviceListDebugLines.Add($"\nDeviceDirectInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
                $"Input Devices found: {deviceList.Count}, " +
                $"Gamepads/Joysticks: {gamepadCount}, " +
                $"Keyboards: {keyboardCount}, " +
                $"Mice: {mouseCount}, " +
                $"Offline/Failed: {offlineCount}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeviceDirectInput: Fatal error during device enumeration: {ex.Message}");
                Debug.WriteLine($"DeviceDirectInput: Stack trace: {ex.StackTrace}");
            }

            foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }

            return deviceList;
        }
        
        /// <summary>
        /// Disposes all DirectInput devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of DirectInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<DirectInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;
            
            Debug.WriteLine($"DeviceDirectInput: Disposing {deviceList.Count} DirectInput devices...");
            
            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo?.DirectInputDevice != null)
                    {
                        Debug.WriteLine($"DeviceDirectInput: Disposing device - {deviceInfo.InstanceName}");
                        deviceInfo.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DeviceDirectInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
                }
            }
            
            Debug.WriteLine("DeviceDirectInput: All devices disposed.");
        }
        
        /// <summary>
        /// Gets a human-readable device type name.
        /// </summary>
        /// <param name="deviceType">DirectInput device type</param>
        /// <returns>Human-readable device type name</returns>
        private string GetDeviceTypeName(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.Mouse:
                    return "Mouse";
                case DeviceType.Keyboard:
                    return "Keyboard";
                case DeviceType.Joystick:
                    return "Joystick";
                case DeviceType.Gamepad:
                    return "Gamepad";
                case DeviceType.FirstPerson:
                    return "First Person Controller";
                case DeviceType.Flight:
                    return "Flight Controller";
                case DeviceType.Driving:
                    return "Driving Controller";
                default:
                    return $"Unknown ({(int)deviceType})";
            }
        }
        
        /// <summary>
        /// Determines if a device type is a gamepad/joystick type.
        /// </summary>
        /// <param name="deviceType">DirectInput device type</param>
        /// <returns>True if device is a gamepad/joystick type</returns>
        private bool IsGamepadType(DeviceType deviceType)
        {
            return deviceType == DeviceType.Joystick ||
                   deviceType == DeviceType.Gamepad ||
                   deviceType == DeviceType.FirstPerson ||
                   deviceType == DeviceType.Flight ||
                   deviceType == DeviceType.Driving;
        }
        
        /// <summary>
        /// Extracts VID and PID from device interface path.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <returns>Tuple containing VID and PID values</returns>
        private (int vid, int pid) ExtractVidPidFromPath(string interfacePath)
        {
            if (string.IsNullOrEmpty(interfacePath))
                return (0, 0);
            
            try
            {
                // Common patterns in interface paths:
                // \\?\hid#vid_045e&pid_028e#...
                // \\?\usb#vid_045e&pid_028e#...
                var upperPath = interfacePath.ToUpperInvariant();
                
                var vidIndex = upperPath.IndexOf("VID_");
                var pidIndex = upperPath.IndexOf("PID_");
                
                if (vidIndex >= 0 && pidIndex >= 0)
                {
                    var vidStart = vidIndex + 4;
                    var pidStart = pidIndex + 4;
                    
                    // Extract 4-character hex values.
                    if (vidStart + 4 <= interfacePath.Length && pidStart + 4 <= interfacePath.Length)
                    {
                        var vidStr = interfacePath.Substring(vidStart, 4);
                        var pidStr = interfacePath.Substring(pidStart, 4);
                        
                        if (int.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out int vid) &&
                            int.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out int pid))
                        {
                            return (vid, pid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeviceDirectInput: Error extracting VID/PID from path '{interfacePath}': {ex.Message}");
            }
            
            return (0, 0);
        }
        
        /// <summary>
        /// Extracts device ID from interface path.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <returns>Device ID string</returns>
        private string ExtractDeviceIdFromPath(string interfacePath)
        {
            if (string.IsNullOrEmpty(interfacePath))
                return "";
            
            try
            {
                // Extract the device ID portion from paths like:
                // \\?\hid#vid_045e&pid_028e&mi_00#7&1234abcd&0&0000#{...}
                var parts = interfacePath.Split('#');
                if (parts.Length >= 2)
                {
                    // Return the hardware ID part (e.g., "vid_045e&pid_028e&mi_00")
                    return parts[1];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeviceDirectInput: Error extracting device ID from path '{interfacePath}': {ex.Message}");
            }
            
            return interfacePath; // Return full path as fallback
        }
        
        /// <summary>
        /// Determines if a device type represents an actual input device.
        /// Filters out non-input devices like sound cards, network adapters, etc.
        /// </summary>
        /// <param name="deviceType">DirectInput device type</param>
        /// <returns>True if device is an input device (gamepad, keyboard, mouse)</returns>
        private bool IsInputDevice(DeviceType deviceType)
        {
            switch (deviceType)
            {
                // Input devices to enumerate.
                case DeviceType.Mouse:
                case DeviceType.Keyboard:
                case DeviceType.Joystick:
                case DeviceType.Gamepad:
                case DeviceType.FirstPerson:
                case DeviceType.Flight:
                case DeviceType.Driving:
                    return true;

                // Non-input devices to ignore.
                default:
                    return false;
            }
        }
    }
}
