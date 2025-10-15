using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace x360ce.App.Input.Devices
{
	/// <summary>
	/// Combined device management class that orchestrates different input device types.
	/// Provides unified access to DirectInput, XInput, and other input methods.
	/// </summary>
	internal class DevicesCombined
	{
		private readonly DevicesPnPInput pnPInputDevices = new DevicesPnPInput();
		private readonly DevicesRawInput rawInputDevices = new DevicesRawInput();
		private readonly DevicesDirectInput directInputDevices = new DevicesDirectInput();
		private readonly DevicesXInput xInputDevices = new DevicesXInput();
		private readonly DevicesGamingInput gamingInputDevices = new DevicesGamingInput();

		// Cache for DirectInput product names to avoid repeated lookups
		private Dictionary<string, string> _directInputNameCache;

		public List<PnPInputDeviceInfo> PnPInputDevicesList;
		public List<RawInputDeviceInfo> RawInputDevicesList;
		public List<DirectInputDeviceInfo> DirectInputDevicesList;
		public List<XInputDeviceInfo> XInputDevicesList;
		public List<GamingInputDeviceInfo> GamingInputDevicesList;
		public ObservableCollection<AllInputDeviceInfo> AllInputDevicesList = new ObservableCollection<AllInputDeviceInfo>();

		/// <summary>
		/// Creates and populates all input device lists from various input sources.
		/// </summary>
		public void CreateInputDevicesLists()
		{
			// Retrieve device lists from all input sources
			PnPInputDevicesList = pnPInputDevices.GetPnPInputDeviceList();
			RawInputDevicesList = rawInputDevices.GetRawInputDeviceList();
			DirectInputDevicesList = directInputDevices.GetDirectInputDeviceList();
			XInputDevicesList = xInputDevices.GetXInputDeviceList();
			GamingInputDevicesList = gamingInputDevices.GetGamingInputDeviceList();

			// Build DirectInput name cache for efficient lookups
			BuildDirectInputNameCache();

			// Combine all device lists into the unified list
			AllInputDevicesList.Clear();

			// Process DirectInput devices first to establish friendly names
			AddDevicesToList(DirectInputDevicesList, (item, _) => item.ProductName, item => item.InterfacePath);

			// Process remaining device types with DirectInput name prefix
			AddDevicesToList(PnPInputDevicesList, GetPrefixedProductName, item => item.HardwareIds);
			AddDevicesToList(RawInputDevicesList, GetPrefixedProductName, item => item.InterfacePath);
			AddDevicesToList(XInputDevicesList, GetPrefixedProductName, item => item.InterfacePath);
			AddDevicesToList(GamingInputDevicesList, GetPrefixedProductName, item => item.InterfacePath);
		}

		/// <summary>
		/// Builds a cache of DirectInput product names indexed by truncated common identifier.
		/// This eliminates repeated LINQ queries during device processing.
		/// </summary>
		private void BuildDirectInputNameCache()
		{
			_directInputNameCache = new Dictionary<string, string>();
			
			if (DirectInputDevicesList == null)
				return;

			foreach (var device in DirectInputDevicesList)
			{
				if (string.IsNullOrEmpty(device.CommonIdentifier))
					continue;

				var key = device.CommonIdentifier.Length > 17
					? device.CommonIdentifier.Substring(0, 17)
					: device.CommonIdentifier;

				// Store first match only (consistent with original behavior)
				if (!_directInputNameCache.ContainsKey(key))
					_directInputNameCache[key] = device.ProductName;
			}
		}

		/// <summary>
		/// Generic method to add devices from any input source to the unified list.
		/// Eliminates code duplication across different device types.
		/// </summary>
		/// <typeparam name="T">The device info type (must have common properties)</typeparam>
		/// <param name="sourceList">Source device list to process</param>
		/// <param name="getProductName">Function to retrieve the product name</param>
		/// <param name="getInterfacePath">Function to retrieve the interface path</param>
		private void AddDevicesToList<T>(
			List<T> sourceList,
			Func<T, string, string> getProductName,
			Func<T, string> getInterfacePath) where T : class
		{
			if (sourceList == null)
				return;

			foreach (var item in sourceList)
			{
				// Extract common properties using reflection-free approach with dynamic
				dynamic device = item;
				string commonId = device.CommonIdentifier;
				
				var allItem = new AllInputDeviceInfo
				{
					InputType = device.InputType,
					CommonIdentifier = commonId,
					AxeCount = device.AxeCount,
					SliderCount = device.SliderCount,
					ButtonCount = device.ButtonCount,
					KeyCount = device.KeyCount,
					PovCount = device.PovCount,
					AxePressed = false,
					SliderPressed = false,
					ButtonPressed = false,
					KeyPressed = false,
					PovPressed = false,
					ProductName = getProductName(item, commonId),
					InterfacePath = getInterfacePath(item)
				};
				
				AllInputDevicesList.Add(allItem);
			}
		}

		/// <summary>
		/// Gets product name with DirectInput prefix for non-DirectInput devices.
		/// </summary>
		private string GetPrefixedProductName<T>(T item, string commonIdentifier) where T : class
		{
			var prefix = GetDirectInputProductNameFromCache(commonIdentifier);
			dynamic device = item;
			return prefix + device.ProductName;
		}

		/// <summary>
		/// Retrieves DirectInput product name from cache using truncated common identifier.
		/// Returns empty string if not found or cache is unavailable.
		/// </summary>
		/// <param name="commonIdentifier">The device's common identifier</param>
		/// <returns>DirectInput product name with separator, or empty string</returns>
		private string GetDirectInputProductNameFromCache(string commonIdentifier)
		{
			if (_directInputNameCache == null || string.IsNullOrEmpty(commonIdentifier))
				return string.Empty;

			var key = commonIdentifier.Length > 17
				? commonIdentifier.Substring(0, 17)
				: commonIdentifier;

			return _directInputNameCache.TryGetValue(key, out var productName)
				? productName + " • "
				: string.Empty;
		}

		/// <summary>
		/// Unified device information structure containing properties common to all input types.
		/// Implements INotifyPropertyChanged to enable DataGrid reactive updates.
		/// </summary>
		public class AllInputDeviceInfo : INotifyPropertyChanged
		{
			public string InputType { get; set; }
			public string CommonIdentifier { get; set; }
			public int AxeCount { get; set; }
			public int SliderCount { get; set; }
			public int ButtonCount { get; set; }
			public int KeyCount { get; set; }
			public int PovCount { get; set; }
			public string ProductName { get; set; }
			public string InterfacePath { get; set; }

			private bool _axePressed;
			private bool _sliderPressed;
			private bool _buttonPressed;
			private bool _keyPressed;
			private bool _povPressed;

			/// <summary>
			/// Gets or sets whether any axis is currently pressed/moved.
			/// </summary>
			public bool AxePressed
			{
				get => _axePressed;
				set
				{
					if (_axePressed != value)
					{
						_axePressed = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Gets or sets whether any slider is currently pressed/moved.
			/// </summary>
			public bool SliderPressed
			{
				get => _sliderPressed;
				set
				{
					if (_sliderPressed != value)
					{
						_sliderPressed = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Gets or sets whether any button is currently pressed.
			/// </summary>
			public bool ButtonPressed
			{
				get => _buttonPressed;
				set
				{
					if (_buttonPressed != value)
					{
						_buttonPressed = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Gets or sets whether any key is currently pressed.
			/// </summary>
			public bool KeyPressed
			{
				get => _keyPressed;
				set
				{
					if (_keyPressed != value)
					{
						_keyPressed = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Gets or sets whether any POV (point of view) control is currently pressed.
			/// </summary>
			public bool PovPressed
			{
				get => _povPressed;
				set
				{
					if (_povPressed != value)
					{
						_povPressed = value;
						OnPropertyChanged();
					}
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			/// <summary>
			/// Raises the PropertyChanged event for data binding updates.
			/// </summary>
			protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}
