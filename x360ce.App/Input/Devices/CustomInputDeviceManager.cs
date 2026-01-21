using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using x360ce.App.Input.States;
using x360ce.Engine;
using x360ce.Engine.Data;

namespace x360ce.App.Input.Devices
{
    /// <summary>
    /// Combined device management class that orchestrates different input device types.
    /// Provides custom access to DirectInput, XInput, and other input methods.
    /// </summary>
    internal class CustomInputDeviceManager
    {
        private readonly PnPInputDevice pnPInputDevice = new PnPInputDevice();
        private readonly RawInputDevice rawInputDevice = new RawInputDevice();
        private readonly DirectInputDevice directInputDevice = new DirectInputDevice();
        private readonly XInputDevice xInputDevice = new XInputDevice();
        private readonly GamingInputDevice gamingInputDevice = new GamingInputDevice();

        public List<PnPInputDeviceInfo> PnPInputDeviceInfoList = new List<PnPInputDeviceInfo>();
        // RawInputDeviceInfoList is now a direct reference to the static authoritative list
        public List<RawInputDeviceInfo> RawInputDeviceInfoList => RawInputDevice.RawInputDeviceInfoList;
        public List<DirectInputDeviceInfo> DirectInputDeviceInfoList = new List<DirectInputDeviceInfo>();
        public List<XInputDeviceInfo> XInputDeviceInfoList = new List<XInputDeviceInfo>();
        public List<GamingInputDeviceInfo> GamingInputDeviceInfoList = new List<GamingInputDeviceInfo>();
        public ObservableCollection<CustomInputDeviceInfo> CustomInputDeviceInfoList = new ObservableCollection<CustomInputDeviceInfo>();

        // Cache for DirectInput product names to avoid repeated lookups
        private Dictionary<string, string> _directInputNameCache;

        // State collection manager for all device types
        private readonly CustomInputStateTimer _stateCollector = CustomInputStateTimer.Instance;

        // Dispatcher for UI thread synchronization
        private System.Windows.Threading.Dispatcher _dispatcher;

        /// <summary>
        /// Sets the dispatcher for UI thread synchronization.
        /// Must be called from the UI thread before device monitoring starts.
        /// </summary>
        public void SetDispatcher(System.Windows.Threading.Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }
        /// <summary>
        /// Creates and populates all input device lists from various input sources:
        /// A. On InputDevicesControl loaded.
        /// B. On Refresh button click.
        /// C. On Input device connection change.
        /// Updates lists incrementally: existing devices are updated in place, new devices are added, removed devices are deleted.
        /// </summary>
        public void GetCustomInputDeviceList()
        {
            // Retrieve current device lists from all input sources
            var currentPnPList = pnPInputDevice.GetPnPInputDeviceInfoList();
            // Populate the static RawInputDeviceInfoList (no return value, updates static list directly)
            rawInputDevice.GetRawInputDeviceInfoList();
            var currentDirectInputList = directInputDevice.GetDirectInputDeviceInfoList();
            var currentXInputList = xInputDevice.GetXInputDeviceInfoList();
            var currentGamingInputList = gamingInputDevice.GetGamingInputDeviceInfoList();

            // Update PnPInputDeviceInfoList incrementally
            UpdateDeviceList(PnPInputDeviceInfoList, currentPnPList, d => d.InstanceGuid);

            // RawInputDeviceInfoList is already updated by rawInputDevice.GetRawInputDeviceInfoList()
            // No need to call UpdateDeviceList - it's the authoritative static list that's directly modified

            // Update DirectInputDeviceInfoList incrementally
            UpdateDeviceList(DirectInputDeviceInfoList, currentDirectInputList, d => d.InstanceGuid);

            // Update XInputDeviceInfoList incrementally
            UpdateDeviceList(XInputDeviceInfoList, currentXInputList, d => d.InstanceGuid);

            // Update GamingInputDeviceInfoList incrementally
            UpdateDeviceList(GamingInputDeviceInfoList, currentGamingInputList, d => d.InstanceGuid);

            // Build DirectInput name cache for efficient lookups
            BuildDirectInputNameCache();
    
            // Update the custom list incrementally instead of clearing
            UpdateCustomInputDeviceList();

            // Add offline devices from SettingsManager
            AddOfflineDevicesToCustomList();

            // RawInputState now directly accesses RawInputDevice.RawInputDeviceInfoList
            // No SetDeviceList call needed - simplified architecture eliminates the method!
    
            // Collect and save states for all devices after enumeration
            // CollectDeviceStates();
        }

        /// <summary>
        /// Updates the custom device list incrementally by comparing with source lists.
        /// Removes devices no longer present, updates existing devices, and adds new devices.
        /// Ensures all ObservableCollection modifications happen on the UI thread.
        /// </summary>
        private void UpdateCustomInputDeviceList()
        {
            // Build a set of all current device identifiers from source lists
            var currentDeviceKeys = new HashSet<string>();

            // Collect all current device keys
            CollectDeviceKeys(DirectInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(PnPInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(RawInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(XInputDeviceInfoList, currentDeviceKeys);
            CollectDeviceKeys(GamingInputDeviceInfoList, currentDeviceKeys);

            // Execute ObservableCollection modifications on UI thread
            var action = new Action(() =>
            {
                // Remove devices that are no longer present in the live lists
                // NOTE: We only remove devices that are ONLINE but not in current keys.
                // Offline devices (IsOnline=false) are managed by AddOfflineDevicesToCustomList.
                for (int i = CustomInputDeviceInfoList.Count - 1; i >= 0; i--)
                {
                    var device = CustomInputDeviceInfoList[i];

                    // If device is marked as Online, but not found in current live keys, remove it.
                    // It might be re-added as Offline later if it exists in Settings.
                    if (device.IsOnline)
                    {
                        var key = GetDeviceKey(device.InputType, device.InputGroupId);
                        if (!currentDeviceKeys.Contains(key))
                        {
                            CustomInputDeviceInfoList.RemoveAt(i);
                        }
                    }
                }

                // Update existing devices and add new ones
                UpdateOrAddDevicesFromList(DirectInputDeviceInfoList, (item, _) => item.ProductName, item => item.InterfacePath);
                UpdateOrAddDevicesFromList(PnPInputDeviceInfoList, GetPrefixedProductName, item => item.HardwareIds);
                UpdateOrAddDevicesFromList(RawInputDeviceInfoList, GetPrefixedProductName, item => item.InterfacePath);
                UpdateOrAddDevicesFromList(XInputDeviceInfoList, GetPrefixedProductName, item => item.InterfacePath);
                UpdateOrAddDevicesFromList(GamingInputDeviceInfoList, GetPrefixedProductName, item => item.InterfacePath);
            });

            // If dispatcher is available and we're not on UI thread, invoke on UI thread
            if (_dispatcher != null && !_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(action);
            }
            else
            {
                // Already on UI thread or no dispatcher set, execute directly
                action();
            }
        }

        /// <summary>
        /// Adds offline devices from SettingsManager.UserDevices to CustomInputDeviceInfoList.
        /// Checks if a device is already present (Online or Offline) to avoid duplicates.
        /// </summary>
        private void AddOfflineDevicesToCustomList()
        {
            // Safely access SettingsManager.UserDevices
            List<UserDevice> offlineDevices;
            lock (SettingsManager.UserDevices.SyncRoot)
            {
                offlineDevices = SettingsManager.UserDevices.Items.ToList();
            }

            var action = new Action(() =>
            {
                // Get set of existing InstanceGuids in the custom list
                var existingGuids = new HashSet<Guid>(CustomInputDeviceInfoList.Select(x => x.InstanceGuid));

                foreach (var userDevice in offlineDevices)
                {
                    // If device is already in the list (Online or Offline), skip it
                    if (existingGuids.Contains(userDevice.InstanceGuid))
                        continue;

                    // Create Offline InputDeviceInfo
                    var offlineInfo = CreateOfflineInputDeviceInfo(userDevice);
                    if (offlineInfo != null)
                    {
                        CustomInputDeviceInfoList.Add(new CustomInputDeviceInfo(offlineInfo));
                    }
                }

                // Also check if any "Offline" devices in our list are no longer in SettingsManager
                // This handles the case where a user deletes a device from the database
                var settingGuids = new HashSet<Guid>(offlineDevices.Select(x => x.InstanceGuid));
                for (int i = CustomInputDeviceInfoList.Count - 1; i >= 0; i--)
                {
                    var device = CustomInputDeviceInfoList[i];
                    if (!device.IsOnline && !settingGuids.Contains(device.InstanceGuid))
                    {
                        CustomInputDeviceInfoList.RemoveAt(i);
                    }
                }
            });

             // If dispatcher is available and we're not on UI thread, invoke on UI thread
            if (_dispatcher != null && !_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(action);
            }
            else
            {
                // Already on UI thread or no dispatcher set, execute directly
                action();
            }
        }

        /// <summary>
        /// Creates an InputDeviceInfo object from a UserDevice, marked as Offline.
        /// </summary>
        private InputDeviceInfo CreateOfflineInputDeviceInfo(UserDevice userDevice)
        {
            InputDeviceInfo info = null;
            string inputType = "DirectInput";

            switch (userDevice.InputMethod)
            {
                case InputSourceType.DirectInput:
                    info = new DirectInputDeviceInfo();
                    inputType = "DirectInput";
                    break;
                case InputSourceType.XInput:
                    info = new XInputDeviceInfo();
                    inputType = "XInput";
                    break;
                case InputSourceType.RawInput:
                    info = new RawInputDeviceInfo();
                    inputType = "RawInput";
                    break;
                case InputSourceType.GamingInput:
                    info = new GamingInputDeviceInfo();
                    inputType = "GamingInput";
                    break;
                // Add other types as needed, defaulting to DirectInput or generic
                default:
                    info = new DirectInputDeviceInfo();
                    inputType = "DirectInput";
                    break;
            }

            if (info != null)
            {
                info.InstanceGuid = userDevice.InstanceGuid;
                info.ProductName = userDevice.ProductName ?? "";
                info.InstanceName = userDevice.InstanceName ?? "";
                info.ProductGuid = userDevice.ProductGuid;
                info.DeviceType = userDevice.CapType; // CapType maps to DeviceType
                
                info.InputType = inputType;
                
                // Construct InputGroupId similar to how it is done in live detection
                // Format: VID_XXXX&PID_XXXX...
                var vid = userDevice.HidVendorId > 0 ? userDevice.HidVendorId : userDevice.DevVendorId;
                var pid = userDevice.HidProductId > 0 ? userDevice.HidProductId : userDevice.DevProductId;
                info.InputGroupId = $"VID_{vid:X4}&PID_{pid:X4}";

                // InterfacePath
                info.InterfacePath = !string.IsNullOrEmpty(userDevice.HidDevicePath) ? userDevice.HidDevicePath : (userDevice.DevDevicePath ?? "");
                
                info.IsOnline = false;
                info.IsEnabled = userDevice.IsEnabled;
                
                // Map other properties if available and necessary
                info.VendorId = vid;
                info.ProductId = pid;

                // Initialize counts to 0 as they are not persisted in UserDevices.xml
                info.AxeCount = 0;
                info.SliderCount = 0;
                info.ButtonCount = 0;
                info.PovCount = 0;

                // Initialize empty state to avoid null reference exceptions in UI
                info.CustomInputState = new CustomInputState();

                // Initialize AssignedToPad list to avoid NullReferenceException in CustomInputDeviceInfo
                info.AssignedToPad = new List<bool> { false, false, false, false };
            }

            return info;
        }

        /// <summary>
        /// Collects device keys from a source list into the provided set.
        /// </summary>
        private void CollectDeviceKeys<T>(List<T> sourceList, HashSet<string> keySet) where T : class
        {
            if (sourceList == null)
                return;

            foreach (var item in sourceList)
            {
                dynamic device = item;
                var key = GetDeviceKey(device.InputType, device.InputGroupId);
                keySet.Add(key);
            }
        }

        /// <summary>
        /// Generates a unique key for a device based on input type and common identifier.
        /// </summary>
        private string GetDeviceKey(string inputType, string inputGroupId)
        {
            return $"{inputType}|{inputGroupId}";
        }

        /// <summary>
        /// Updates existing devices or adds new devices from a source list to the custom list.
        /// </summary>
        private void UpdateOrAddDevicesFromList<T>(
         List<T> sourceList,
         Func<T, string, string> getProductName,
         Func<T, string> getInterfacePath) where T : class
        {
            if (sourceList == null)
                return;

            foreach (var item in sourceList)
            {
                dynamic device = item;
                string inputGroupId = device.InputGroupId;
                string inputType = device.InputType;
                var key = GetDeviceKey(inputType, inputGroupId);

                // Find existing device in custom list
                var existingDevice = CustomInputDeviceInfoList.FirstOrDefault(d =>
                 GetDeviceKey(d.InputType, d.InputGroupId) == key);

                if (existingDevice != null)
                {
                	// Update existing device properties
                	   // Since CustomInputDeviceInfo wraps the source device by reference,
                	   // most properties are already up to date if the source object is the same.
                	   // However, computed properties like ProductName (with prefix) need explicit update.
                	   
                	   // CRITICAL: Update the underlying device reference.
                	   // Required for RawInput which creates new device instances on enumeration.
                	   existingDevice.SetDevice(item as InputDeviceInfo);

                	existingDevice.ProductName = getProductName(item, inputGroupId);
                	existingDevice.InterfacePath = getInterfacePath(item);
                }
                else
                {
                	// Add new device
                	var newDevice = new CustomInputDeviceInfo(item as InputDeviceInfo)
                	{
                		AxePressed = false,
                		SliderPressed = false,
                		ButtonPressed = false,
                		PovPressed = false,
                		ProductName = getProductName(item, inputGroupId),
                		InterfacePath = getInterfacePath(item),
                    };
                	CustomInputDeviceInfoList.Add(newDevice);
                }
            }
        }

        /// <summary>
        /// Updates a device list incrementally: existing devices are updated in place, new devices are added, removed devices are deleted.
        /// </summary>
        /// <typeparam name="T">Device info type</typeparam>
        /// <param name="existingList">Existing device list to update</param>
        /// <param name="currentList">Current device list from enumeration</param>
        /// <param name="keySelector">Function to extract unique identifier</param>
        private void UpdateDeviceList<T>(List<T> existingList, List<T> currentList, Func<T, Guid> keySelector) where T : class
        {
            if (currentList == null)
                return;

            // Create dictionaries for efficient lookup
            var existingDict = existingList.ToDictionary(keySelector);
            var currentDict = currentList.ToDictionary(keySelector);

            // CRITICAL FIX: Store ListInputState from devices that will be removed
            // This preserves state when devices are temporarily removed and re-added
            var preservedStates = new Dictionary<Guid, object>();
            for (int i = existingList.Count - 1; i >= 0; i--)
            {
                var device = existingList[i];
                var key = keySelector(device);
                if (!currentDict.ContainsKey(key))
                {
                    // Device is being removed - preserve its ListInputState if it has one
                    var listInputStateProp = device.GetType().GetProperty("CustomInputState");
                    if (listInputStateProp != null)
                    {
                        var state = listInputStateProp.GetValue(device);
                        if (state != null)
                        {
                            preservedStates[key] = state;
                        }
                    }
                    existingList.RemoveAt(i);
                }
            }

            // Add new devices and update existing ones
            foreach (var currentDevice in currentList)
            {
                var key = keySelector(currentDevice);
                if (existingDict.ContainsKey(key))
                {
                    // CRITICAL FIX: Update existing device properties IN PLACE instead of replacing the object
                    // This preserves the ListInputState property that is updated by event-driven RawInput processing
                    var existingDevice = existingDict[key];

                    // Copy all properties from currentDevice to existingDevice
                    // This is done using reflection to handle all device types generically
                    var properties = currentDevice.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    foreach (var prop in properties)
                    {
                        // Skip ListInputState property - it's updated by event-driven processing
                        if (prop.Name == "CustomInputState")
                            continue;

                        // Only copy writable properties
                        if (prop.CanWrite && prop.CanRead)
                        {
                            try
                            {
                                var value = prop.GetValue(currentDevice);
                                prop.SetValue(existingDevice, value);
                            }
                            catch
                            {
                                // Skip properties that can't be copied
                            }
                        }
                    }
                }
                else
                {
                    // Add new device
                    existingList.Add(currentDevice);
                    
                    // CRITICAL FIX: Restore preserved ListInputState if this device was temporarily removed
                    if (preservedStates.ContainsKey(key))
                    {
                        var listInputStateProp = currentDevice.GetType().GetProperty("CustomInputState");
                        if (listInputStateProp != null && listInputStateProp.CanWrite)
                        {
                            listInputStateProp.SetValue(currentDevice, preservedStates[key]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds a cache of DirectInput product names indexed by truncated common identifier.
        /// This eliminates repeated LINQ queries during device processing.
        /// </summary>
        private void BuildDirectInputNameCache()
        {
            _directInputNameCache = new Dictionary<string, string>();

            if (DirectInputDeviceInfoList == null)
                return;

            foreach (var device in DirectInputDeviceInfoList)
            {
                if (string.IsNullOrEmpty(device.InputGroupId))
                    continue;

                var key = device.InputGroupId.Length > 17
                    ? device.InputGroupId.Substring(0, 17)
                    : device.InputGroupId;

                // Store first match only (consistent with original behavior)
                if (!_directInputNameCache.ContainsKey(key))
                    _directInputNameCache[key] = device.ProductName;
            }
        }

        /// <summary>
        /// Gets product name with DirectInput prefix for non-DirectInput devices.
        /// </summary>
        private string GetPrefixedProductName<T>(T item, string inputGroupId) where T : class
        {
            var prefix = GetDirectInputProductNameFromCache(inputGroupId);
            dynamic device = item;
            return prefix + device.ProductName;
        }

        /// <summary>
        /// Retrieves DirectInput product name from cache using truncated common identifier.
        /// Returns empty string if not found or cache is unavailable.
        /// </summary>
        /// <param name="inputGroupId">The device's input group identifier</param>
        /// <returns>DirectInput product name with separator, or empty string</returns>
        private string GetDirectInputProductNameFromCache(string inputGroupId)
        {
            if (_directInputNameCache == null || string.IsNullOrEmpty(inputGroupId))
                return string.Empty;

            var key = inputGroupId.Length > 17
                ? inputGroupId.Substring(0, 17)
                : inputGroupId;

            return _directInputNameCache.TryGetValue(key, out var productName)
                ? productName + " • "
                : string.Empty;
        }

        /// <summary>
        /// Collects and saves current states for all enumerated devices.
        /// This populates the ListInputState property on each device info object.
        /// Note: RawInput states are updated event-driven (1000+ Hz) when WM_INPUT arrives,
        /// so we only need to collect initial states here for consistency.
        /// </summary>
        //private void CollectDeviceStates()
        //{
        //    // Collect initial states for RawInput (event-driven updates happen at 1000+ Hz via WM_INPUT)
        //    _stateCollector.GetAndSaveRawInputStates(RawInputDeviceInfoList);

        //    // Collect states for polling-based devices (updated at 20Hz by timer)
        //    _stateCollector.GetAndSaveDirectInputStates(DirectInputDeviceInfoList);
        //    _stateCollector.GetAndSaveXInputStates(XInputDeviceInfoList);
        //    _stateCollector.GetAndSaveGamingInputStates(GamingInputDeviceInfoList);
        //}

    }
}
