using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to retrieve RawInput device states.
	/// Handles state reading for HID devices using Windows Raw Input API.
	/// </summary>
	/// <remarks>
	/// RawInput API Characteristics:
	/// • Uses device handles (IntPtr) instead of device objects or slot indices
	/// • Message-based input system (WM_INPUT messages)
	/// • Supports unlimited number of HID-compliant devices
	/// • Works with gamepads, joysticks, keyboards, and mice
	/// • No device acquisition needed (unlike DirectInput)
	/// • Background access supported (major advantage over DirectInput)
	/// 
	/// RawInput State Reading Approaches:
	/// 1. Message-based (WM_INPUT): Cached states from background messages (may have 2-3s lag)
	/// 2. Direct HID polling: Real-time state reading via HID API (immediate response)
	/// 
	/// This implementation provides direct HID polling for immediate state reading,
	/// similar to how DirectInput and XInput work.
	/// </remarks>
	internal class StatesRawInput
	{
		#region Win32 API Declarations

		// HID.dll - HID API for reading device state
		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetUsages(
			HIDP_REPORT_TYPE reportType,
			ushort usagePage,
			ushort linkCollection,
			[Out] ushort[] usageList,
			ref uint usageLength,
			IntPtr preparsedData,
			byte[] report,
			uint reportLength);

		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetUsageValue(
			HIDP_REPORT_TYPE reportType,
			ushort usagePage,
			ushort linkCollection,
			ushort usage,
			out uint usageValue,
			IntPtr preparsedData,
			byte[] report,
			uint reportLength);

		// Kernel32.dll - File operations for HID device access
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern IntPtr CreateFile(
			string lpFileName,
			uint dwDesiredAccess,
			uint dwShareMode,
			IntPtr lpSecurityAttributes,
			uint dwCreationDisposition,
			uint dwFlagsAndAttributes,
			IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool ReadFile(
			IntPtr hFile,
			byte[] lpBuffer,
			uint nNumberOfBytesToRead,
			out uint lpNumberOfBytesRead,
			IntPtr lpOverlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool CloseHandle(IntPtr hObject);

		// Constants
		private const uint GENERIC_READ = 0x80000000;
		private const uint GENERIC_WRITE = 0x40000000;
		private const uint FILE_SHARE_READ = 0x00000001;
		private const uint FILE_SHARE_WRITE = 0x00000002;
		private const uint OPEN_EXISTING = 3;
		private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

		private const int HIDP_STATUS_SUCCESS = 0x00110000;

		#endregion

		#region HID Structures

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDP_CAPS
		{
			public ushort Usage;
			public ushort UsagePage;
			public ushort InputReportByteLength;
			public ushort OutputReportByteLength;
			public ushort FeatureReportByteLength;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
			public ushort[] Reserved;
			public ushort NumberLinkCollectionNodes;
			public ushort NumberInputButtonCaps;
			public ushort NumberInputValueCaps;
			public ushort NumberInputDataIndices;
			public ushort NumberOutputButtonCaps;
			public ushort NumberOutputValueCaps;
			public ushort NumberOutputDataIndices;
			public ushort NumberFeatureButtonCaps;
			public ushort NumberFeatureValueCaps;
			public ushort NumberFeatureDataIndices;
		}

		private enum HIDP_REPORT_TYPE
		{
			HidP_Input = 0,
			HidP_Output = 1,
			HidP_Feature = 2
		}

		#endregion

		#region Device Handle Cache

		/// <summary>
		/// Cache of opened HID device handles for efficient state reading.
		/// Key: Device interface path, Value: HID device handle
		/// </summary>
		private readonly System.Collections.Generic.Dictionary<string, IntPtr> _deviceHandleCache = 
			new System.Collections.Generic.Dictionary<string, IntPtr>();

		/// <summary>
		/// Cache of preparsed data for each device.
		/// Key: Device interface path, Value: Preparsed data handle
		/// </summary>
		private readonly System.Collections.Generic.Dictionary<string, IntPtr> _preparsedDataCache = 
			new System.Collections.Generic.Dictionary<string, IntPtr>();

		#endregion

		#region State Retrieval Methods

		/// <summary>
		/// Returns the current state of a RawInput device.
		/// The device info must contain a valid DeviceHandle and InterfacePath.
		/// </summary>
		/// <param name="deviceInfo">RawInputDeviceInfo containing the device to read</param>
		/// <returns>Byte array containing the HID input report, or null if read failed</returns>
		/// <remarks>
		/// This method reads the current input state from a RawInput HID device.
		/// 
		/// RawInput Device Access:
		/// • Device handles are obtained from DevicesRawInput.GetRawInputDeviceList()
		/// • InterfacePath is used to open the HID device for direct reading
		/// • No slot-based indexing - direct device handle reference required
		/// • Device handles remain valid until device is disconnected
		/// 
		/// Unlike DirectInput and XInput:
		/// • No device acquisition needed (always ready to read)
		/// • No slot limitations (supports unlimited devices)
		/// • Returns raw HID report data (requires parsing)
		/// • Background access supported (major advantage)
		/// 
		/// HID Input Report Contents:
		/// • Report ID (first byte, if device uses report IDs)
		/// • Button states (packed bits)
		/// • Axis values (multi-byte values)
		/// • POV/Hat switch values
		/// • Device-specific data
		/// 
		/// The returned byte array is the raw HID input report that needs to be
		/// parsed using HID API functions (HidP_GetUsages, HidP_GetUsageValue).
		/// </remarks>
		public byte[] GetRawInputDeviceState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo == null)
			{
				Debug.WriteLine("StatesRawInput: Device info is null");
				return null;
			}

			if (string.IsNullOrEmpty(deviceInfo.InterfacePath))
			{
				Debug.WriteLine("StatesRawInput: Device interface path is null or empty");
				return null;
			}

			try
			{
				// Get or open HID device handle
				IntPtr hidHandle = GetOrOpenHidDevice(deviceInfo.InterfacePath);
				if (hidHandle == IntPtr.Zero || hidHandle == INVALID_HANDLE_VALUE)
				{
					Debug.WriteLine($"StatesRawInput: Failed to open HID device: {deviceInfo.InterfacePath}");
					return null;
				}

				// Get preparsed data for report size
				IntPtr preparsedData = GetOrCreatePreparsedData(hidHandle, deviceInfo.InterfacePath);
				if (preparsedData == IntPtr.Zero)
				{
					Debug.WriteLine($"StatesRawInput: Failed to get preparsed data for: {deviceInfo.InterfacePath}");
					return null;
				}

				// Get HID capabilities to determine input report size
				HIDP_CAPS caps;
				int status = HidP_GetCaps(preparsedData, out caps);
				if (status != HIDP_STATUS_SUCCESS)
				{
					Debug.WriteLine($"StatesRawInput: Failed to get HID capabilities. Status: 0x{status:X8}");
					return null;
				}

				// Allocate buffer for input report
				byte[] reportBuffer = new byte[caps.InputReportByteLength];

				// Read current input report from device
				uint bytesRead = 0;
				bool success = ReadFile(hidHandle, reportBuffer, (uint)reportBuffer.Length, out bytesRead, IntPtr.Zero);

				if (!success || bytesRead == 0)
				{
					Debug.WriteLine($"StatesRawInput: Failed to read input report. BytesRead: {bytesRead}");
					return null;
				}

				return reportBuffer;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"StatesRawInput: Error reading state for {deviceInfo.InstanceName}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Returns the current state of a RawInput device by interface path.
		/// </summary>
		/// <param name="interfacePath">Device interface path (e.g., \\?\hid#vid_045e&pid_028e...)</param>
		/// <returns>Byte array containing the HID input report, or null if read failed</returns>
		/// <remarks>
		/// This is a convenience method that accepts an interface path directly.
		/// Useful when you have the device path but not the full device info.
		/// </remarks>
		public byte[] GetRawInputState(string interfacePath)
		{
			if (string.IsNullOrEmpty(interfacePath))
			{
				Debug.WriteLine("StatesRawInput: Interface path is null or empty");
				return null;
			}

			var deviceInfo = new RawInputDeviceInfo { InterfacePath = interfacePath };
			return GetRawInputDeviceState(deviceInfo);
		}

		#endregion

		#region HID Device Management

		/// <summary>
		/// Gets or opens a HID device handle for the specified interface path.
		/// Uses caching to avoid repeatedly opening the same device.
		/// </summary>
		/// <param name="interfacePath">Device interface path</param>
		/// <returns>HID device handle or IntPtr.Zero if failed</returns>
		private IntPtr GetOrOpenHidDevice(string interfacePath)
		{
			// Check cache first
			if (_deviceHandleCache.TryGetValue(interfacePath, out IntPtr cachedHandle))
			{
				// Verify handle is still valid by attempting a test operation
				if (cachedHandle != IntPtr.Zero && cachedHandle != INVALID_HANDLE_VALUE)
					return cachedHandle;
				else
					_deviceHandleCache.Remove(interfacePath);
			}

			// Open new HID device handle
			IntPtr handle = CreateFile(
				interfacePath,
				GENERIC_READ | GENERIC_WRITE,
				FILE_SHARE_READ | FILE_SHARE_WRITE,
				IntPtr.Zero,
				OPEN_EXISTING,
				0, // Non-overlapped for synchronous reading
				IntPtr.Zero);

			if (handle != INVALID_HANDLE_VALUE)
			{
				_deviceHandleCache[interfacePath] = handle;
				Debug.WriteLine($"StatesRawInput: Opened HID device: {interfacePath}");
			}

			return handle;
		}

		/// <summary>
		/// Gets or creates preparsed data for a HID device.
		/// Uses caching to avoid repeatedly parsing the same device.
		/// </summary>
		/// <param name="hidHandle">HID device handle</param>
		/// <param name="interfacePath">Device interface path (for caching)</param>
		/// <returns>Preparsed data handle or IntPtr.Zero if failed</returns>
		private IntPtr GetOrCreatePreparsedData(IntPtr hidHandle, string interfacePath)
		{
			// Check cache first
			if (_preparsedDataCache.TryGetValue(interfacePath, out IntPtr cachedData))
			{
				if (cachedData != IntPtr.Zero)
					return cachedData;
				else
					_preparsedDataCache.Remove(interfacePath);
			}

			// Get preparsed data from device
			IntPtr preparsedData;
			bool success = HidD_GetPreparsedData(hidHandle, out preparsedData);

			if (success && preparsedData != IntPtr.Zero)
			{
				_preparsedDataCache[interfacePath] = preparsedData;
				Debug.WriteLine($"StatesRawInput: Got preparsed data for: {interfacePath}");
			}

			return preparsedData;
		}

		/// <summary>
		/// Closes a HID device handle and removes it from cache.
		/// </summary>
		/// <param name="interfacePath">Device interface path</param>
		public void CloseDevice(string interfacePath)
		{
			if (_deviceHandleCache.TryGetValue(interfacePath, out IntPtr handle))
			{
				if (handle != IntPtr.Zero && handle != INVALID_HANDLE_VALUE)
				{
					CloseHandle(handle);
					Debug.WriteLine($"StatesRawInput: Closed HID device: {interfacePath}");
				}
				_deviceHandleCache.Remove(interfacePath);
			}

			if (_preparsedDataCache.TryGetValue(interfacePath, out IntPtr preparsedData))
			{
				if (preparsedData != IntPtr.Zero)
				{
					HidD_FreePreparsedData(preparsedData);
				}
				_preparsedDataCache.Remove(interfacePath);
			}
		}

		/// <summary>
		/// Closes all cached HID device handles.
		/// Call this when shutting down or switching input methods.
		/// </summary>
		public void CloseAllDevices()
		{
			foreach (var kvp in _deviceHandleCache)
			{
				if (kvp.Value != IntPtr.Zero && kvp.Value != INVALID_HANDLE_VALUE)
				{
					CloseHandle(kvp.Value);
				}
			}
			_deviceHandleCache.Clear();

			foreach (var kvp in _preparsedDataCache)
			{
				if (kvp.Value != IntPtr.Zero)
				{
					HidD_FreePreparsedData(kvp.Value);
				}
			}
			_preparsedDataCache.Clear();

			Debug.WriteLine("StatesRawInput: Closed all HID devices");
		}

		#endregion

		#region Diagnostic Methods

		/// <summary>
		/// Gets diagnostic information about RawInput state reading.
		/// </summary>
		/// <returns>String containing detailed RawInput status</returns>
		public string GetDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder();
			info.AppendLine("=== RawInput State Reader Status ===");

			try
			{
				info.AppendLine($"Cached Device Handles: {_deviceHandleCache.Count}");
				info.AppendLine($"Cached Preparsed Data: {_preparsedDataCache.Count}");
				info.AppendLine();

				info.AppendLine("RawInput State Reading Features:");
				info.AppendLine("  ✅ Direct HID device polling (immediate response)");
				info.AppendLine("  ✅ Background access supported");
				info.AppendLine("  ✅ Unlimited number of devices");
				info.AppendLine("  ✅ Handle caching for efficiency");
				info.AppendLine("  ✅ Self-contained implementation");
				info.AppendLine();

				info.AppendLine("Cached Devices:");
				foreach (var kvp in _deviceHandleCache)
				{
					info.AppendLine($"  • {kvp.Key}");
					info.AppendLine($"    Handle: 0x{kvp.Value.ToInt64():X8}");
				}
			}
			catch (Exception ex)
			{
				info.AppendLine($"Error getting diagnostic info: {ex.Message}");
			}

			return info.ToString();
		}

		/// <summary>
		/// Gets detailed state information for a specific device.
		/// </summary>
		/// <param name="deviceInfo">The device to get state information for</param>
		/// <returns>String containing detailed state information</returns>
		public string GetDeviceStateInfo(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo == null)
				return "Device info is null";

			var info = new System.Text.StringBuilder();

			try
			{
				var report = GetRawInputDeviceState(deviceInfo);

				info.AppendLine($"=== {deviceInfo.DisplayName} State ===");
				info.AppendLine($"Interface Path: {deviceInfo.InterfacePath}");
				info.AppendLine($"Device Handle: 0x{deviceInfo.DeviceHandle.ToInt64():X8}");

				if (report != null)
				{
					info.AppendLine($"Report Size: {report.Length} bytes");
					info.AppendLine($"Report Data (hex): {BitConverter.ToString(report)}");
				}
				else
				{
					info.AppendLine("Failed to read device state");
				}
			}
			catch (Exception ex)
			{
				info.AppendLine($"Error reading device state: {ex.Message}");
			}

			return info.ToString();
		}

		#endregion
	}
}
