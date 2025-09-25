
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// RawInput device container with both device information and the actual device handle.
    /// Contains comprehensive device metadata plus the device handle for input reading.
    /// </summary>
    public class RawInputDeviceInfo : IDisposable
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
        
        // RawInput-specific properties
        public IntPtr DeviceHandle { get; set; }
        public RawInputDeviceType RawInputDeviceType { get; set; }
        public uint RawInputFlags { get; set; }
        public string RegistryPath { get; set; }
        
        /// <summary>
        /// Note: RawInput API does not provide native friendly names or manufacturer information.
        /// RawInput only provides device interface paths and basic HID information.
        /// Friendly names would require additional Windows Registry queries or device manager APIs
        /// which are not part of the core RawInput functionality.
        /// </summary>
        
        /// <summary>
        /// Display name combining device type and name for easy identification.
        /// </summary>
        public string DisplayName => $"{DeviceTypeName} - {InstanceName}";
        
        /// <summary>
        /// VID/PID string in standard format for hardware identification.
        /// </summary>
        public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
        
        /// <summary>
        /// Dispose the RawInput device when no longer needed.
        /// </summary>
        public void Dispose()
        {
            // RawInput devices don't need explicit disposal, but we clear the handle
            DeviceHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// RawInput device types enumeration.
    /// </summary>
    public enum RawInputDeviceType : uint
    {
        Mouse = 0,
        Keyboard = 1,
        HID = 2
    }

    /// <summary>
    /// RawInput device enumeration and management class.
    /// Self-contained implementation with minimal external dependencies.
    /// Provides functionality to discover and list RawInput devices including gamepads, keyboards, and mice.
    /// Returns device information that can be used for input reading through RawInput API.
    /// </summary>
    internal class DevicesRawInput
    {
        #region Win32 API Structures and Constants

        /// <summary>
        /// Contains information about a raw input device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public uint dwType;
        }

        /// <summary>
        /// Contains information about a raw input device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO
        {
            public uint cbSize;
            public uint dwType;
            public RID_DEVICE_INFO_UNION union;
        }

        /// <summary>
        /// Union for different device types in RID_DEVICE_INFO.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct RID_DEVICE_INFO_UNION
        {
            [FieldOffset(0)]
            public RID_DEVICE_INFO_MOUSE mouse;
            [FieldOffset(0)]
            public RID_DEVICE_INFO_KEYBOARD keyboard;
            [FieldOffset(0)]
            public RID_DEVICE_INFO_HID hid;
        }

        /// <summary>
        /// Mouse device information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO_MOUSE
        {
            public uint dwId;
            public uint dwNumberOfButtons;
            public uint dwSampleRate;
            public bool fHasHorizontalWheel;
        }

        /// <summary>
        /// Keyboard device information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO_KEYBOARD
        {
            public uint dwType;
            public uint dwSubType;
            public uint dwKeyboardMode;
            public uint dwNumberOfFunctionKeys;
            public uint dwNumberOfIndicators;
            public uint dwNumberOfKeysTotal;
        }

        /// <summary>
        /// HID device information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RID_DEVICE_INFO_HID
        {
            public uint dwVendorId;
            public uint dwProductId;
            public uint dwVersionNumber;
            public ushort usUsagePage;
            public ushort usUsage;
        }

        // RawInput API constants
        private const uint RIDI_DEVICENAME = 0x20000007;
        private const uint RIDI_DEVICEINFO = 0x2000000b;
        // Device types
        private const uint RIM_TYPEMOUSE = 0;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RIM_TYPEHID = 2;

        // HID Usage Pages
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_PAGE_GAME = 0x05;

        // HID Usages for Generic Desktop
        private const ushort HID_USAGE_GENERIC_JOYSTICK = 0x04;
        private const ushort HID_USAGE_GENERIC_GAMEPAD = 0x05;
        private const ushort HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER = 0x08;

        #endregion

        #region Win32 API Imports

        /// <summary>
        /// Enumerates the raw input devices attached to the system.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList(
            [Out] RAWINPUTDEVICELIST[] pRawInputDeviceList,
            ref uint puiNumDevices,
            uint cbSize);

        /// <summary>
        /// Gets information about the raw input device.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize);

        /// <summary>
        /// Gets the last Win32 error code.
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        #endregion

        /// <summary>
        /// Creates a public list of RawInput devices (gamepads, keyboards, mice) with device information and logs their properties.
        /// This method enumerates all available RawInput devices and outputs detailed information for debugging.
        /// </summary>
        /// <returns>List of RawInputDeviceInfo objects containing device information and handles</returns>
        /// <remarks>
        /// This method performs comprehensive RawInput device enumeration:
        /// • Discovers all RawInput-compatible devices (HID devices, keyboards, mice)
        /// • Creates RawInputDeviceInfo objects with device information AND device handles
        /// • Logs detailed device properties using Debug.WriteLine for diagnostics
        /// • Filters devices by type and availability
        /// • Provides device capability information where available
        /// • Keeps device handles for immediate input reading
        /// • Is self-contained with minimal external dependencies
        ///
        /// IMPORTANT: The returned RawInputDeviceInfo objects contain device handles.
        /// Call Dispose() on each RawInputDeviceInfo when no longer needed to free resources.
        /// </remarks>
        public List<RawInputDeviceInfo> GetRawInputDeviceList()
        {
            var stopwatch = Stopwatch.StartNew();
            var deviceList = new List<RawInputDeviceInfo>();
            var deviceListDebugLines = new List<string>();
            int deviceListIndex = 0;

            try
            {
                Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
                    "DevicesRawInput: Starting RawInput device enumeration...");

                // First, get the number of devices
                uint deviceCount = 0;
                uint result = GetRawInputDeviceList(null, ref deviceCount, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());
                
                if (result == uint.MaxValue)
                {
                    uint error = GetLastError();
                    Debug.WriteLine($"DevicesRawInput: Failed to get device count. Error: {error}");
                    deviceListDebugLines.Add($"DevicesRawInput: Failed to get device count. Error: {error}");
                    deviceListDebugLines.Add("\nDevicesRawInput: RawInput devices found: 0, HID: 0, Keyboards: 0, Mice: 0, Offline/Failed: 0\n");
                    foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }
                    return deviceList;
                }

                if (deviceCount == 0)
                {
                    Debug.WriteLine("DevicesRawInput: No RawInput devices found");
                    deviceListDebugLines.Add("DevicesRawInput: No RawInput devices found");
                    deviceListDebugLines.Add("\nDevicesRawInput: RawInput devices found: 0, HID: 0, Keyboards: 0, Mice: 0, Offline/Failed: 0\n");
                    foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }
                    return deviceList;
                }

                Debug.WriteLine($"DevicesRawInput: Found {deviceCount} RawInput devices");

                // Allocate array for device list
                var rawDevices = new RAWINPUTDEVICELIST[deviceCount];
                result = GetRawInputDeviceList(rawDevices, ref deviceCount, (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>());

                if (result == uint.MaxValue)
                {
                    uint error = GetLastError();
                    Debug.WriteLine($"DevicesRawInput: Failed to enumerate devices. Error: {error}");
                    deviceListDebugLines.Add($"DevicesRawInput: Failed to enumerate devices. Error: {error}");
                    deviceListDebugLines.Add("\nDevicesRawInput: RawInput devices found: 0, HID: 0, Keyboards: 0, Mice: 0, Offline/Failed: 0\n");
                    foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }
                    return deviceList;
                }

                // Process each device with early filtering
                foreach (var rawDevice in rawDevices)
                {
                    try
                    {
                        // Early filtering: Skip non-input device types immediately
                        var rawInputDeviceType = (RawInputDeviceType)rawDevice.dwType;
                        
                        // Always process Mouse and Keyboard devices
                        if (rawInputDeviceType == RawInputDeviceType.Mouse || rawInputDeviceType == RawInputDeviceType.Keyboard)
                        {
                            // Process as input device
                            ProcessInputDevice(rawDevice, ref deviceListIndex, deviceList, deviceListDebugLines);
                            continue;
                        }
                        
                        // For HID devices, do early filtering based on basic information
                        if (rawInputDeviceType == RawInputDeviceType.HID)
                        {
                            // Get minimal device information for early filtering
                            string deviceName = GetDeviceName(rawDevice.hDevice);
                            var deviceInfoStruct = GetDeviceInfo(rawDevice.hDevice);
                            
                            // Early filter based on device name patterns
                            if (IsKnownNonInputDeviceByName(deviceName))
                            {
                                // Skip this device entirely - don't process or log it
                                continue;
                            }
                            
                            // Early filter based on HID usage if available
                            if (deviceInfoStruct.HasValue && deviceInfoStruct.Value.dwType == RIM_TYPEHID)
                            {
                                var hid = deviceInfoStruct.Value.union.hid;
                                int usagePage = hid.usUsagePage;
                                int usage = hid.usUsage;
                                
                                // Check if it's a known input device by HID usage
                                if (IsKnownInputDeviceByUsage(usagePage, usage))
                                {
                                    // Process as input device
                                    ProcessInputDevice(rawDevice, ref deviceListIndex, deviceList, deviceListDebugLines);
                                    continue;
                                }
                                
                                // Check if device has input capabilities
                                if (HasInputCapabilitiesFromDeviceInfo(deviceInfoStruct.Value))
                                {
                                    // Process as input device
                                    ProcessInputDevice(rawDevice, ref deviceListIndex, deviceList, deviceListDebugLines);
                                    continue;
                                }
                                
                                // Check if device name suggests it's an input device
                                if (HasInputDeviceNamePattern(deviceName, ""))
                                {
                                    // Process as input device
                                    ProcessInputDevice(rawDevice, ref deviceListIndex, deviceList, deviceListDebugLines);
                                    continue;
                                }
                                
                                // Additional check: Process the device temporarily to check capabilities
                                // This is needed because some devices might pass usage checks but have no actual input capabilities
                                var tempDeviceInfo = CreateTempDeviceInfo(rawDevice, deviceName, deviceInfoStruct.Value);
                                if (tempDeviceInfo != null && HasActualInputCapabilities(tempDeviceInfo))
                                {
                                    // Process as input device
                                    ProcessInputDevice(rawDevice, ref deviceListIndex, deviceList, deviceListDebugLines);
                                    continue;
                                }
                            }
                            
                            // If we reach here, it's likely not an input device - skip it entirely
                            continue;
                        }
                        
                        // Unknown device types are skipped entirely
                    }
                    catch (Exception deviceEx)
                    {
                        Debug.WriteLine($"DevicesRawInput: Error processing device 0x{rawDevice.hDevice.ToInt64():X8}: {deviceEx.Message}");
                    }
                }

                // Generate summary statistics for device enumeration results
                var hidCount = deviceList.Count(d => d.RawInputDeviceType == RawInputDeviceType.HID);
                var keyboardCount = deviceList.Count(d => d.RawInputDeviceType == RawInputDeviceType.Keyboard);
                var mouseCount = deviceList.Count(d => d.RawInputDeviceType == RawInputDeviceType.Mouse);
                var gamepadCount = deviceList.Count(d => IsGamepadDevice(d.Usage, d.UsagePage));
                var offlineCount = deviceList.Count(d => !d.IsOnline);

                stopwatch.Stop();

                deviceListDebugLines.Add($"\nDevicesRawInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
                    $"Input Devices found: {deviceList.Count}, " +
                    $"HID: {hidCount}, " +
                    $"Gamepads: {gamepadCount}, " +
                    $"Keyboards: {keyboardCount}, " +
                    $"Mice: {mouseCount}, " +
                    $"Offline/Failed: {offlineCount}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Fatal error during RawInput device enumeration: {ex.Message}");
                Debug.WriteLine($"DevicesRawInput: Stack trace: {ex.StackTrace}");
            }

            foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }

            return deviceList;
        }

        /// <summary>
        /// Disposes all RawInput devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of RawInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<RawInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;

            Debug.WriteLine($"DevicesRawInput: Disposing {deviceList.Count} RawInput devices...");

            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo != null)
                    {
                        Debug.WriteLine($"DevicesRawInput: Disposing device - {deviceInfo.InstanceName}");
                        deviceInfo.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DevicesRawInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
                }
            }

            Debug.WriteLine("DevicesRawInput: All RawInput devices disposed.");
        }

        #region Private Helper Methods

        /// <summary>
        /// Gets the device name (interface path) for a RawInput device.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <returns>Device name/interface path</returns>
        private string GetDeviceName(IntPtr hDevice)
        {
            try
            {
                uint size = 0;
                GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);

                if (size == 0)
                    return "";

                IntPtr buffer = Marshal.AllocHGlobal((int)size * 2); // Unicode characters
                try
                {
                    uint result = GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, buffer, ref size);
                    if (result == uint.MaxValue)
                        return "";

                    return Marshal.PtrToStringUni(buffer) ?? "";
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error getting device name for handle 0x{hDevice.ToInt64():X8}: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Gets device information structure for a RawInput device.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <returns>Device information structure or null if failed</returns>
        private RID_DEVICE_INFO? GetDeviceInfo(IntPtr hDevice)
        {
            try
            {
                uint size = (uint)Marshal.SizeOf<RID_DEVICE_INFO>();
                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    uint result = GetRawInputDeviceInfo(hDevice, RIDI_DEVICEINFO, buffer, ref size);
                    if (result == uint.MaxValue)
                        return null;

                    var deviceInfo = Marshal.PtrToStructure<RID_DEVICE_INFO>(buffer);
                    deviceInfo.cbSize = size;
                    return deviceInfo;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error getting device info for handle 0x{hDevice.ToInt64():X8}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Populates device information from RawInput device info structure.
        /// </summary>
        /// <param name="deviceInfo">Device info object to populate</param>
        /// <param name="ridDeviceInfo">RawInput device info structure</param>
        private void PopulateDeviceInfo(RawInputDeviceInfo deviceInfo, RID_DEVICE_INFO ridDeviceInfo)
        {
            switch (ridDeviceInfo.dwType)
            {
                case RIM_TYPEMOUSE:
                    var mouse = ridDeviceInfo.union.mouse;
                    deviceInfo.ButtonCount = (int)mouse.dwNumberOfButtons;
                    deviceInfo.ProductName = "Mouse";
                    deviceInfo.AxeCount = mouse.fHasHorizontalWheel ? 4 : 3; // X, Y, Wheel, (Horizontal Wheel)
                    break;

                case RIM_TYPEKEYBOARD:
                    var keyboard = ridDeviceInfo.union.keyboard;
                    deviceInfo.ButtonCount = (int)keyboard.dwNumberOfKeysTotal;
                    deviceInfo.ProductName = "Keyboard";
                    deviceInfo.DeviceSubtype = (int)keyboard.dwSubType;
                    break;

                case RIM_TYPEHID:
                    var hid = ridDeviceInfo.union.hid;
                    deviceInfo.VendorId = (int)hid.dwVendorId;
                    deviceInfo.ProductId = (int)hid.dwProductId;
                    deviceInfo.Usage = hid.usUsage;
                    deviceInfo.UsagePage = hid.usUsagePage;
                    deviceInfo.HardwareRevision = (int)hid.dwVersionNumber;
                    
                    // Estimate capabilities for HID devices
                    if (IsGamepadDevice(hid.usUsage, hid.usUsagePage))
                    {
                        deviceInfo.ProductName = "HID Gamepad";
                        deviceInfo.AxeCount = 6; // Typical gamepad axes
                        deviceInfo.ButtonCount = 16; // Typical gamepad buttons
                        deviceInfo.PovCount = 1; // Typical D-pad
                        deviceInfo.HasForceFeedback = false; // Cannot determine from RawInput
                    }
                    else
                    {
                        deviceInfo.ProductName = "HID Device";
                        deviceInfo.AxeCount = 0;
                        deviceInfo.ButtonCount = 0;
                        deviceInfo.PovCount = 0;
                    }
                    break;
            }
        }

        /// <summary>
        /// Determines if a device is a gamepad based on usage and usage page.
        /// </summary>
        /// <param name="usage">HID usage</param>
        /// <param name="usagePage">HID usage page</param>
        /// <returns>True if device is a gamepad</returns>
        private bool IsGamepadDevice(int usage, int usagePage)
        {
            return (usagePage == HID_USAGE_PAGE_GENERIC && 
                   (usage == HID_USAGE_GENERIC_JOYSTICK || 
                    usage == HID_USAGE_GENERIC_GAMEPAD ||
                    usage == HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER)) ||
                   (usagePage == HID_USAGE_PAGE_GAME);
        }

        /// <summary>
        /// Gets a human-readable device type name.
        /// </summary>
        /// <param name="deviceType">RawInput device type</param>
        /// <param name="usage">HID usage</param>
        /// <param name="usagePage">HID usage page</param>
        /// <returns>Human-readable device type name</returns>
        private string GetDeviceTypeName(RawInputDeviceType deviceType, int usage, int usagePage)
        {
            switch (deviceType)
            {
                case RawInputDeviceType.Mouse:
                    return "RawInput Mouse";
                case RawInputDeviceType.Keyboard:
                    return "RawInput Keyboard";
                case RawInputDeviceType.HID:
                    if (IsGamepadDevice(usage, usagePage))
                    {
                        if (usage == HID_USAGE_GENERIC_JOYSTICK)
                            return "RawInput Joystick";
                        else if (usage == HID_USAGE_GENERIC_GAMEPAD)
                            return "RawInput Gamepad";
                        else
                            return "RawInput Game Controller";
                    }
                    return $"RawInput HID Device (Usage: 0x{usage:X4}, Page: 0x{usagePage:X4})";
                default:
                    return $"RawInput Unknown ({(int)deviceType})";
            }
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

                    // Extract 4-character hex values
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
                Debug.WriteLine($"DevicesRawInput: Error extracting VID/PID from path '{interfacePath}': {ex.Message}");
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
                Debug.WriteLine($"DevicesRawInput: Error extracting device ID from path '{interfacePath}': {ex.Message}");
            }

            return interfacePath; // Return full path as fallback
        }

        /// <summary>
        /// Extracts a friendly device name from the interface path.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <returns>Friendly device name</returns>
        private string ExtractDeviceNameFromPath(string interfacePath)
        {
            if (string.IsNullOrEmpty(interfacePath))
                return "Unknown Device";

            try
            {
                // Try to extract meaningful name from path
                var parts = interfacePath.Split('#');
                if (parts.Length >= 2)
                {
                    var devicePart = parts[1];
                    // Remove VID/PID and return a cleaner name
                    var cleanName = devicePart.Replace("vid_", "VID_").Replace("pid_", "PID_").Replace("&", " ");
                    return cleanName;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error extracting device name from path '{interfacePath}': {ex.Message}");
            }

            return "RawInput Device";
        }

        /// <summary>
        /// Generates a unique instance GUID for a device.
        /// </summary>
        /// <param name="hDevice">Device handle</param>
        /// <param name="deviceName">Device name</param>
        /// <returns>Unique instance GUID</returns>
        private Guid GenerateInstanceGuid(IntPtr hDevice, string deviceName)
        {
            try
            {
                // Create a deterministic GUID based on device handle and name
                var input = $"RawInput_{hDevice.ToInt64():X16}_{deviceName}";
                var hash = System.Security.Cryptography.MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
                return new Guid(hash);
            }
            catch
            {
                // Fallback to random GUID
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Generates a product GUID based on VID and PID.
        /// </summary>
        /// <param name="vendorId">Vendor ID</param>
        /// <param name="productId">Product ID</param>
        /// <returns>Product GUID</returns>
        private Guid GenerateProductGuid(int vendorId, int productId)
        {
            try
            {
                // Create a deterministic GUID based on VID/PID
                var guidBytes = new byte[16];
                var vidBytes = BitConverter.GetBytes(vendorId);
                var pidBytes = BitConverter.GetBytes(productId);
                
                // Fill GUID with VID/PID pattern
                Array.Copy(vidBytes, 0, guidBytes, 0, 4);
                Array.Copy(pidBytes, 0, guidBytes, 4, 4);
                
                // Add RawInput identifier
                guidBytes[8] = 0x52; // 'R'
                guidBytes[9] = 0x41; // 'A'
                guidBytes[10] = 0x57; // 'W'
                guidBytes[11] = 0x49; // 'I'
                
                return new Guid(guidBytes);
            }
            catch
            {
                // Fallback to random GUID
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Generates hardware IDs string for device identification.
        /// </summary>
        /// <param name="vendorId">Vendor ID</param>
        /// <param name="productId">Product ID</param>
        /// <param name="deviceType">RawInput device type</param>
        /// <returns>Hardware IDs string</returns>
        private string GenerateHardwareIds(int vendorId, int productId, RawInputDeviceType deviceType)
        {
            if (vendorId == 0 && productId == 0)
                return "";

            string prefix;
            switch (deviceType)
            {
                case RawInputDeviceType.HID:
                    prefix = "HID";
                    break;
                case RawInputDeviceType.Mouse:
                    prefix = "MOUSE";
                    break;
                case RawInputDeviceType.Keyboard:
                    prefix = "KEYBOARD";
                    break;
                default:
                    prefix = "RAWINPUT";
                    break;
            }

            return $"{prefix}\\VID_{vendorId:X4}&PID_{productId:X4}";
        }


        /// <summary>
        /// Checks if device is a known input device based on HID usage page and usage.
        /// </summary>
        /// <param name="usagePage">HID usage page</param>
        /// <param name="usage">HID usage</param>
        /// <returns>True if device is a known input device type</returns>
        private bool IsKnownInputDeviceByUsage(int usagePage, int usage)
        {
            // Generic Desktop Controls (0x01)
            if (usagePage == HID_USAGE_PAGE_GENERIC)
            {
                switch (usage)
                {
                    case HID_USAGE_GENERIC_JOYSTICK:        // 0x04 - Joystick
                    case HID_USAGE_GENERIC_GAMEPAD:         // 0x05 - Gamepad
                    case HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER: // 0x08 - Multi-axis Controller
                    case 0x02: // Mouse (HID_USAGE_GENERIC_MOUSE)
                    case 0x06: // Keyboard (HID_USAGE_GENERIC_KEYBOARD)
                    case 0x07: // Keypad (HID_USAGE_GENERIC_KEYPAD)
                        return true;
                }
            }

            // Game Controls (0x05)
            if (usagePage == HID_USAGE_PAGE_GAME)
            {
                // Any device in Game Controls usage page is likely an input device
                return true;
            }

            // Consumer Controls (0x0C) - These are typically media keys, volume controls, etc.
            // Most are not gaming input devices, so we need to be more selective
            if (usagePage == 0x0C)
            {
                // Only include specific consumer control usages that are actual gaming inputs
                // Most consumer controls (like volume, media keys) should be excluded
                switch (usage)
                {
                    case 0x01: // Consumer Control (generic) - usually not a gaming device
                    case 0x02: // Numeric Key Pad - usually not a gaming device
                    case 0x03: // Programmable Buttons - usually not a gaming device
                    case 0x04: // Microphone - not a gaming input device
                    case 0x05: // Headphone - not a gaming input device
                    case 0x06: // Graphic Equalizer - not a gaming input device
                        return false; // Exclude these consumer controls
                    
                    // We could add specific gaming-related consumer controls here if needed
                    // For now, exclude all consumer controls as they're typically not gaming devices
                    default:
                        return false;
                }
            }

            // Telephony (0x0B) - Phone controls are typically not gaming input devices
            if (usagePage == 0x0B)
            {
                return false; // Exclude telephony devices
            }

            return false;
        }

        /// <summary>
        /// Checks if device name/path contains patterns typical of input devices.
        /// </summary>
        /// <param name="interfacePath">Device interface path</param>
        /// <param name="instanceName">Device instance name</param>
        /// <returns>True if device has input device name patterns</returns>
        private bool HasInputDeviceNamePattern(string interfacePath, string instanceName)
        {
            if (string.IsNullOrEmpty(interfacePath) && string.IsNullOrEmpty(instanceName))
                return false;

            var combinedText = $"{interfacePath} {instanceName}".ToLowerInvariant();

            // Known input device patterns
            string[] inputPatterns = {
                "gamepad", "joystick", "controller", "wheel", "pedal", "throttle",
                "mouse", "keyboard", "trackpad", "touchpad", "trackball",
                "tablet", "stylus", "pen", "digitizer", "touch",
                "remote", "media", "volume", "button", "switch"
            };

            foreach (var pattern in inputPatterns)
            {
                if (combinedText.Contains(pattern))
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Processes a device that has been identified as an input device.
        /// Extracts full device information and adds it to the device list.
        /// </summary>
        /// <param name="rawDevice">Raw device information</param>
        /// <param name="deviceListIndex">Current device index (will be incremented)</param>
        /// <param name="deviceList">List to add the device to</param>
        /// <param name="deviceListDebugLines">Debug lines list</param>
        private void ProcessInputDevice(RAWINPUTDEVICELIST rawDevice, ref int deviceListIndex,
            List<RawInputDeviceInfo> deviceList, List<string> deviceListDebugLines)
        {
            try
            {
                // Create RawInputDeviceInfo object with basic information
                var deviceInfo = new RawInputDeviceInfo
                {
                    DeviceHandle = rawDevice.hDevice,
                    RawInputDeviceType = (RawInputDeviceType)rawDevice.dwType,
                    DeviceType = (int)rawDevice.dwType,
                    RawInputFlags = rawDevice.dwType,
                    IsOnline = true
                };

                // Get device name (interface path)
                string deviceName = GetDeviceName(rawDevice.hDevice);
                deviceInfo.InterfacePath = deviceName;
                
                // Extract VID/PID from device name (RawInput provides this in interface path)
                var vidPid = ExtractVidPidFromPath(deviceName);
                deviceInfo.VendorId = vidPid.vid;
                deviceInfo.ProductId = vidPid.pid;
                
                // Use only what RawInput provides - extracted name from interface path
                deviceInfo.InstanceName = ExtractDeviceNameFromPath(deviceName);

                // Get device information
                var deviceInfoStruct = GetDeviceInfo(rawDevice.hDevice);
                if (deviceInfoStruct.HasValue)
                {
                    PopulateDeviceInfo(deviceInfo, deviceInfoStruct.Value);
                }

                // VID/PID already extracted above for friendly name lookup

                // Generate GUIDs
                deviceInfo.InstanceGuid = GenerateInstanceGuid(rawDevice.hDevice, deviceName);
                deviceInfo.ProductGuid = GenerateProductGuid(deviceInfo.VendorId, deviceInfo.ProductId);

                // Set device type name
                deviceInfo.DeviceTypeName = GetDeviceTypeName(deviceInfo.RawInputDeviceType, deviceInfo.Usage, deviceInfo.UsagePage);

                // Extract additional device identification
                deviceInfo.DeviceId = ExtractDeviceIdFromPath(deviceName);
                deviceInfo.HardwareIds = GenerateHardwareIds(deviceInfo.VendorId, deviceInfo.ProductId, deviceInfo.RawInputDeviceType);

                // Set default values for properties not available through RawInput
                deviceInfo.DriverVersion = 1;
                deviceInfo.HardwareRevision = 1;
                deviceInfo.FirmwareRevision = 1;
                deviceInfo.ParentDeviceId = "";

                deviceListIndex++;

                // Log comprehensive device information for debugging
                deviceListDebugLines.Add($"\n{deviceListIndex}. DevicesRawInputInfo: " +
                    $"DeviceHandle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}, " +
                    $"RawInputDeviceType: {deviceInfo.RawInputDeviceType}, " +
                    $"InstanceGuid: {deviceInfo.InstanceGuid}, " +
                    $"ProductGuid: {deviceInfo.ProductGuid}, " +
                    $"InstanceName: {deviceInfo.InstanceName}, " +
                    $"ProductName: {deviceInfo.ProductName}, " +
                    $"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
                    $"Usage: 0x{deviceInfo.Usage:X4}, " +
                    $"UsagePage: 0x{deviceInfo.UsagePage:X4}, " +
                    $"InterfacePath: {deviceInfo.InterfacePath}");

                deviceListDebugLines.Add($"DevicesRawInputInfo Identification: " +
                    $"VidPidString: {deviceInfo.VidPidString}, " +
                    $"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
                    $"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4}), " +
                    $"DeviceId: {deviceInfo.DeviceId}");

                deviceListDebugLines.Add($"DevicesRawInputInfo Capabilities: " +
                    $"AxeCount: {deviceInfo.AxeCount}, " +
                    $"ButtonCount: {deviceInfo.ButtonCount}, " +
                    $"PovCount: {deviceInfo.PovCount}, " +
                    $"HasForceFeedback: {deviceInfo.HasForceFeedback}");

                // Add device to the final list (already filtered as input device)
                deviceList.Add(deviceInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesRawInput: Error processing input device 0x{rawDevice.hDevice.ToInt64():X8}: {ex.Message}");
            }
        }

        /// <summary>
        /// Quick check if device name indicates it's a known non-input device.
        /// Used for early filtering to skip processing entirely.
        /// </summary>
        /// <param name="deviceName">Device name/interface path</param>
        /// <returns>True if device is definitely not an input device</returns>
        private bool IsKnownNonInputDeviceByName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return false;

            var lowerName = deviceName.ToLowerInvariant();

            // Known non-input device patterns that we can identify early
            string[] definiteNonInputPatterns = {
                "audio", "sound", "speaker", "microphone", "headphone",
                "storage", "disk", "drive", "mass", "flash",
                "network", "ethernet", "wifi", "bluetooth\\radio",
                "camera", "webcam", "video", "capture",
                "printer", "scanner", "fax",
                "modem", "serial", "parallel",
                "hub", "root", "composite\\interface",
                "system", "acpi", "pci", "processor"
            };

            foreach (var pattern in definiteNonInputPatterns)
            {
                if (lowerName.Contains(pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if device has input capabilities based on device info structure.
        /// Used for early filtering of HID devices.
        /// </summary>
        /// <param name="deviceInfo">RawInput device info structure</param>
        /// <returns>True if device appears to have input capabilities</returns>
        private bool HasInputCapabilitiesFromDeviceInfo(RID_DEVICE_INFO deviceInfo)
        {
            switch (deviceInfo.dwType)
            {
                case RIM_TYPEMOUSE:
                    var mouse = deviceInfo.union.mouse;
                    return mouse.dwNumberOfButtons > 0;

                case RIM_TYPEKEYBOARD:
                    var keyboard = deviceInfo.union.keyboard;
                    return keyboard.dwNumberOfKeysTotal > 0;

                case RIM_TYPEHID:
                    var hid = deviceInfo.union.hid;
                    // For HID devices, we can't easily determine button/axis count from RID_DEVICE_INFO
                    // So we rely on usage page/usage classification
                    return IsKnownInputDeviceByUsage(hid.usUsagePage, hid.usUsage);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Creates a temporary device info object to check capabilities without full processing.
        /// </summary>
        /// <param name="rawDevice">Raw device information</param>
        /// <param name="deviceName">Device name</param>
        /// <param name="ridDeviceInfo">RawInput device info structure</param>
        /// <returns>Temporary device info or null if creation fails</returns>
        private RawInputDeviceInfo CreateTempDeviceInfo(RAWINPUTDEVICELIST rawDevice, string deviceName, RID_DEVICE_INFO ridDeviceInfo)
        {
            try
            {
                var deviceInfo = new RawInputDeviceInfo
                {
                    DeviceHandle = rawDevice.hDevice,
                    RawInputDeviceType = (RawInputDeviceType)rawDevice.dwType,
                    DeviceType = (int)rawDevice.dwType,
                    InterfacePath = deviceName
                };

                // Populate basic device information
                PopulateDeviceInfo(deviceInfo, ridDeviceInfo);

                return deviceInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if a device has actual input capabilities (buttons, axes, or povs).
        /// Used to filter out devices that pass usage checks but have no real input capabilities.
        /// </summary>
        /// <param name="deviceInfo">Device info to check</param>
        /// <returns>True if device has actual input capabilities</returns>
        private bool HasActualInputCapabilities(RawInputDeviceInfo deviceInfo)
        {
            // Device must have at least one input capability to be considered an input device
            return deviceInfo.ButtonCount > 0 ||
                   deviceInfo.AxeCount > 0 ||
                   deviceInfo.PovCount > 0;
        }

        #endregion
    }
}
