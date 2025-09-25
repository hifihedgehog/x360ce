using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// XInput device container with both device information and the actual XInput controller object.
    /// Contains comprehensive device metadata plus the live XInput controller for input reading.
    /// </summary>
    public class XInputDeviceInfo : IDisposable
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
        
        // XInput-specific properties
        public UserIndex XInputSlot { get; set; }
        public int SlotIndex { get; set; }
        public uint LastPacketNumber { get; set; }
        
        /// <summary>
        /// Note: XInput API does not provide native friendly names or manufacturer information.
        /// XInput abstracts hardware details and always reports generic "Microsoft" VID/PID values
        /// regardless of the actual controller manufacturer (Sony, Nintendo, etc.).
        /// Use DirectInput or RawInput for real manufacturer information.
        /// </summary>
        
        /// <summary>
        /// The actual XInput device object for reading input.
        /// </summary>
        public Controller XInputDevice { get; set; }
        
        /// <summary>
        /// Display name combining slot and name for easy identification.
        /// </summary>
        public string DisplayName => $"XInput Slot {SlotIndex + 1} - {InstanceName}";
        
        /// <summary>
        /// VID/PID string in standard format for hardware identification.
        /// </summary>
        public string VidPidString => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
        
        /// <summary>
        /// Dispose the XInput controller when no longer needed.
        /// </summary>
        public void Dispose()
        {
            // XInput devices don't need explicit disposal, but we clear the reference
            XInputDevice = null;
        }
    }

    /// <summary>
    /// XInput device enumeration and management class.
    /// Self-contained implementation with minimal external dependencies.
    /// Provides functionality to discover and list XInput devices including Xbox controllers.
    /// Returns live XInput controller objects that can be used for input reading.
    /// </summary>
    internal class DevicesXInput
    {
        /// <summary>
        /// Creates a public list of XInput devices (Xbox controllers) with live controller objects and logs their properties.
        /// This method enumerates all available XInput controllers and outputs detailed information for debugging.
        /// </summary>
        /// <returns>List of XInputDeviceInfo objects containing both device information and live XInput controller objects</returns>
        /// <remarks>
        /// This method performs comprehensive XInput device enumeration:
        /// • Discovers all XInput-compatible devices (Xbox 360/One controllers)
        /// • Creates XInputDeviceInfo objects with device information AND live XInput controller objects
        /// • Logs detailed device properties using Debug.WriteLine for diagnostics
        /// • Limited to 4 controllers maximum (XInput API limitation)
        /// • Provides device capability information for Xbox controllers
        /// • Keeps XInput controllers alive for immediate input reading
        /// • Is self-contained with minimal external dependencies
        ///
        /// IMPORTANT: The returned XInputDeviceInfo objects contain live XInput controllers.
        /// Call Dispose() on each XInputDeviceInfo when no longer needed to free resources.
        /// </remarks>
        public List<XInputDeviceInfo> GetXInputDeviceList()
        {
            var stopwatch = Stopwatch.StartNew();
            var deviceList = new List<XInputDeviceInfo>();
            var deviceListDebugLines = new List<string>();
            int deviceListIndex = 0;

            try
            {
                Debug.WriteLine("\n-----------------------------------------------------------------------------------------------------------------\n\n" +
                    "DevicesXInput: Starting XInput device enumeration...");
                
                // First ensure XInput library is loaded
                if (!EnsureXInputLibraryLoaded())
                {
                    Debug.WriteLine("DevicesXInput: XInput library could not be loaded");
                    deviceListDebugLines.Add("DevicesXInput: XInput library not found or could not be loaded");
                    deviceListDebugLines.Add("\nDevicesXInput: XInput controllers found: 0/4, Online: 0, Offline/Failed: 0\n");
                    foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }
                    return deviceList; // Return empty list
                }
                
                Debug.WriteLine("DevicesXInput: XInput library is loaded and available");
                
                // XInput supports maximum 4 controllers (slots 0-3)
                for (int slotIndex = 0; slotIndex < 4; slotIndex++)
                {
                    try
                    {
                        var userIndex = (UserIndex)slotIndex;
                        var controller = new Controller(userIndex);
                        
                        // Test if controller is connected by attempting to get state
                        State controllerState;
                        bool isConnected = SafeGetControllerState(controller, out controllerState);
                        
                        if (isConnected)
                        {
                            // Debug.WriteLine($"DevicesXInput: Found XInput controller in slot {slotIndex}");
                            
                            // Create XInputDeviceInfo object with XInput-only information
                            var deviceInfo = new XInputDeviceInfo
                            {
                                // Generate unique GUID for this XInput slot
                                InstanceGuid = GenerateXInputSlotGuid(slotIndex),
                                InstanceName = $"XInput Controller {slotIndex + 1}",
                                ProductGuid = GenerateXInputProductGuid(),
                                ProductName = "XInput Controller",
                                DeviceType = (int)SharpDX.DirectInput.DeviceType.Gamepad,
                                DeviceSubtype = 1, // Standard gamepad
                                Usage = 0x05, // Game Controls
                                UsagePage = 0x01, // Generic Desktop
                                DeviceTypeName = "XInput Controller",
                                XInputSlot = userIndex,
                                SlotIndex = slotIndex,
                                XInputDevice = controller,
                                IsOnline = true,
                                LastPacketNumber = (uint)controllerState.PacketNumber
                            };
                            
                            // XInput doesn't provide VID/PID information - use generic values
                            deviceInfo.VendorId = 0x045E; // Microsoft (XInput standard)
                            deviceInfo.ProductId = 0x028E; // Xbox 360 Controller (XInput standard)
                            
                            // Set standard XInput capabilities
                            deviceInfo.AxeCount = 6; // Left Stick X/Y, Right Stick X/Y, Left/Right Triggers
                            deviceInfo.ButtonCount = 15; // A, B, X, Y, LB, RB, Back, Start, LS, RS, DPad (4), Guide
                            deviceInfo.PovCount = 0; // XInput maps DPad to buttons, not POV
                            deviceInfo.HasForceFeedback = true; // XInput supports vibration
                            
                            // Try to get additional controller information
                            try
                            {
                                // XInput doesn't provide detailed capabilities like DirectInput
                                // Set standard Xbox controller information
                                deviceInfo.DeviceSubtype = 1; // Standard gamepad
                                deviceInfo.HasForceFeedback = true; // XInput supports vibration
                                
                                // Set known Xbox controller VID/PID (Microsoft)
                                deviceInfo.VendorId = 0x045E; // Microsoft
                                deviceInfo.ProductId = 0x028E; // Xbox 360 Controller (default)
                                
                                // deviceListDebugLines.Add($"DevicesXInput: Controller information - SubType: Standard Gamepad, HasForceFeedback: {deviceInfo.HasForceFeedback}");
                            }
                            catch (Exception capEx)
                            {
                                Debug.WriteLine($"DevicesXInput: Could not get information for slot {slotIndex}: {capEx.Message}");
                                
                                // Set default Xbox controller VID/PID (Microsoft)
                                deviceInfo.VendorId = 0x045E; // Microsoft
                                deviceInfo.ProductId = 0x028E; // Xbox 360 Controller (default)
                            }
                            
                            // Generate device identification strings
                            deviceInfo.DeviceId = $"XInput\\Controller_{slotIndex:D2}";
                            deviceInfo.InterfacePath = $"\\\\?\\XInput#{slotIndex}";
                            deviceInfo.HardwareIds = $"XInput\\VID_{deviceInfo.VendorId:X4}&PID_{deviceInfo.ProductId:X4}";
                            
                            // Set driver and firmware information (XInput doesn't provide real values)
                            deviceInfo.DriverVersion = GetXInputVersion();
                            deviceInfo.HardwareRevision = 1;
                            deviceInfo.FirmwareRevision = 1;

                            deviceListIndex++;

                            // Log comprehensive device information for debugging
                            deviceListDebugLines.Add($"\n{deviceListIndex}. DevicesXInputInfo: " +
                                $"SlotIndex: {deviceInfo.SlotIndex}, " +
                                $"XInputSlot { deviceInfo.XInputSlot}, " + 
                                $"InstanceGuid: {deviceInfo.InstanceGuid}, " +
                                $"ProductGuid: {deviceInfo.ProductGuid}, " +
                                $"InstanceName: {deviceInfo.InstanceName}, " +
                                $"ProductName: {deviceInfo.ProductName}, " +
                                $"DeviceType: {deviceInfo.DeviceType}, " +
                                $"DeviceTypeName: {deviceInfo.DeviceTypeName}, " +
                                $"PacketNumber: {deviceInfo.LastPacketNumber}, " +
                                $"VidPidString: {deviceInfo.VidPidString}, " +
                                $"VendorId: {deviceInfo.VendorId} (0x{deviceInfo.VendorId:X4}), " +
                                $"ProductId: {deviceInfo.ProductId} (0x{deviceInfo.ProductId:X4})");

                            deviceListDebugLines.Add($"DevicesXInputInfo Capabilities: " +
                                $"AxeCount: {deviceInfo.AxeCount}, " +
                                $"ButtonCount: {deviceInfo.ButtonCount}, " +
                                $"PovCount: {deviceInfo.PovCount}, " +
                                $"HasForceFeedback: {deviceInfo.HasForceFeedback}");
                            
                            // Add device to the final list
                            deviceList.Add(deviceInfo);
                        }
                        else
                        {
                            Debug.WriteLine($"DevicesXInput: No controller found in XInput slot {slotIndex}");
                        }
                    }
                    catch (Exception slotEx)
                    {
                        Debug.WriteLine($"DevicesXInput: Error checking XInput slot {slotIndex}: {slotEx.Message}");
                    }
                }
                
                // Generate summary statistics for device enumeration results
                var connectedCount = deviceList.Count;
                var offlineCount = deviceList.Count(d => !d.IsOnline);

                stopwatch.Stop();

                deviceListDebugLines.Add($"\nDevicesXInput: ({(int)Math.Round(stopwatch.Elapsed.TotalMilliseconds)} ms) " +
                    $"Input Devices found: {connectedCount}/4, " +
                    $"Online: {connectedCount - offlineCount}, " +
                    $"Offline/Failed: {offlineCount}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesXInput: Fatal error during XInput device enumeration: {ex.Message}");
                Debug.WriteLine($"DevicesXInput: Stack trace: {ex.StackTrace}");
            }

            foreach (var debugLine in deviceListDebugLines) { Debug.WriteLine(debugLine); }

            return deviceList;
        }
        
        /// <summary>
        /// Disposes all XInput devices in the provided list to free resources.
        /// Call this method when the device list is no longer needed.
        /// </summary>
        /// <param name="deviceList">List of XInputDeviceInfo objects to dispose</param>
        public static void DisposeDeviceList(List<XInputDeviceInfo> deviceList)
        {
            if (deviceList == null) return;
            
            Debug.WriteLine($"DevicesXInput: Disposing {deviceList.Count} XInput devices...");
            
            foreach (var deviceInfo in deviceList)
            {
                try
                {
                    if (deviceInfo != null)
                    {
                        Debug.WriteLine($"DevicesXInput: Disposing device - {deviceInfo.InstanceName}");
                        deviceInfo.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DevicesXInput: Error disposing device {deviceInfo?.InstanceName}: {ex.Message}");
                }
            }
            
            Debug.WriteLine("DevicesXInput: All XInput devices disposed.");
        }
        
        /// <summary>
        /// Generates a unique GUID for an XInput slot.
        /// </summary>
        /// <param name="slotIndex">The XInput slot index (0-3)</param>
        /// <returns>Unique GUID for the slot</returns>
        private Guid GenerateXInputSlotGuid(int slotIndex)
        {
            // Generate consistent GUID based on XInput slot
            // Using a base GUID and modifying the last bytes with slot index
            var baseBytes = new byte[] { 0x58, 0x49, 0x4E, 0x50, 0x55, 0x54, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            baseBytes[15] = (byte)slotIndex;
            return new Guid(baseBytes);
        }
        
        /// <summary>
        /// Generates a product GUID for XInput controllers.
        /// </summary>
        /// <returns>Product GUID for Xbox controllers</returns>
        private Guid GenerateXInputProductGuid()
        {
            // Standard Xbox controller product GUID
            return new Guid("58494E50-5554-5052-4F44-000000000000"); // "XINPUTPROD"
        }
        
        
        /// <summary>
        /// Ensures XInput library is loaded and available for use.
        /// Uses the x360ce custom XInput library loading system.
        /// </summary>
        /// <returns>True if XInput library is loaded and functional</returns>
        private bool EnsureXInputLibraryLoaded()
        {
            try
            {
                // Check if XInput library is already loaded
                if (SharpDX.XInput.Controller.IsLoaded)
                {
                    Debug.WriteLine("DevicesXInput: XInput library already loaded");
                    return true;
                }
                
                Debug.WriteLine("DevicesXInput: XInput library not loaded, attempting to load...");
                
                // Try to load XInput library using x360ce's method
                // First try XInput 1.4 (Windows 10+)
                Exception loadError;
                SharpDX.XInput.Controller.ReLoadLibrary("xinput1_4.dll", out loadError);
                
                if (SharpDX.XInput.Controller.IsLoaded)
                {
                    Debug.WriteLine("DevicesXInput: Successfully loaded xinput1_4.dll");
                    return true;
                }
                
                Debug.WriteLine($"DevicesXInput: Failed to load xinput1_4.dll: {loadError?.Message}");
                
                // Try XInput 1.3 (Windows 7/8)
                SharpDX.XInput.Controller.ReLoadLibrary("xinput1_3.dll", out loadError);
                
                if (SharpDX.XInput.Controller.IsLoaded)
                {
                    Debug.WriteLine("DevicesXInput: Successfully loaded xinput1_3.dll");
                    return true;
                }
                
                Debug.WriteLine($"DevicesXInput: Failed to load xinput1_3.dll: {loadError?.Message}");
                
                // Try XInput 9.1.0 (Windows Vista/7)
                SharpDX.XInput.Controller.ReLoadLibrary("xinput9_1_0.dll", out loadError);
                
                if (SharpDX.XInput.Controller.IsLoaded)
                {
                    Debug.WriteLine("DevicesXInput: Successfully loaded xinput9_1_0.dll");
                    return true;
                }
                
                Debug.WriteLine($"DevicesXInput: Failed to load xinput9_1_0.dll: {loadError?.Message}");
                Debug.WriteLine("DevicesXInput: All XInput library loading attempts failed");
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesXInput: Exception during XInput library loading: {ex.Message}");
                return false;
            }
        }
        
        
        /// <summary>
        /// Safely gets controller state with proper error handling.
        /// </summary>
        /// <param name="controller">The controller to get state from</param>
        /// <param name="state">Output state</param>
        /// <returns>True if controller is connected and state was retrieved</returns>
        private bool SafeGetControllerState(Controller controller, out State state)
        {
            state = new State();
            
            try
            {
                return controller.GetState(out state);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Debug.WriteLine($"DevicesXInput: Win32Exception getting controller state: {ex.Message}");
                return false;
            }
            catch (System.DllNotFoundException ex)
            {
                Debug.WriteLine($"DevicesXInput: XInput DLL not found when getting state: {ex.Message}");
                return false;
            }
            catch (System.EntryPointNotFoundException ex)
            {
                Debug.WriteLine($"DevicesXInput: XInput entry point not found when getting state: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevicesXInput: Unexpected error getting controller state: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Gets the XInput version information.
        /// </summary>
        /// <returns>XInput version as integer</returns>
        private int GetXInputVersion()
        {
            try
            {
                // XInput 1.4 is the current version on Windows 10+
                return 0x0104; // Version 1.4
            }
            catch
            {
                return 0x0103; // Fallback to XInput 1.3
            }
        }
    }
}
