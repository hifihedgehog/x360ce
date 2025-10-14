using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides methods to check if any button is pressed on RawInput devices.
	/// Supports RawInput HID devices (gamepads, joysticks, keyboards, mice).
	/// </summary>
	internal class StatesAnyButtonIsPressedRawInput
	{
		private readonly StatesRawInput _statesRawInput = new StatesRawInput();

		// Cache for RawInput device to AllInputDeviceInfo mapping
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;

		#region Win32 API Declarations for HID Parsing

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
		private static extern bool CloseHandle(IntPtr hObject);

		private const uint GENERIC_READ = 0x80000000;
		private const uint GENERIC_WRITE = 0x40000000;
		private const uint FILE_SHARE_READ = 0x00000001;
		private const uint FILE_SHARE_WRITE = 0x00000002;
		private const uint OPEN_EXISTING = 3;
		private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
		private const int HIDP_STATUS_SUCCESS = 0x00110000;

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

		/// <summary>
		/// Checks each RawInput device for button presses and updates the ButtonPressed property
		/// in AllInputDevicesList.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckRawInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			if (devicesCombined.RawInputDevicesList == null || devicesCombined.AllInputDevicesList == null)
				return;

			// Build mapping cache on first run or when device list changes
			if (_deviceMapping == null || _deviceMapping.Count != devicesCombined.RawInputDevicesList.Count)
				BuildDeviceMapping(devicesCombined);

			// Check each RawInput device
			foreach (var riDevice in devicesCombined.RawInputDevicesList)
			{
				if (riDevice == null || string.IsNullOrEmpty(riDevice.InterfacePath))
					continue;

				// Get the current state and check for button presses
				var report = _statesRawInput.GetRawInputDeviceState(riDevice);
				if (report == null)
					continue;

				// Determine if any button is pressed by parsing HID report
				bool anyButtonPressed = IsAnyButtonPressedInHidReport(riDevice.InterfacePath, report);

				// Use cached mapping for faster lookup
				if (_deviceMapping.TryGetValue(riDevice.InterfacePath, out var allDevice))
				{
					allDevice.ButtonPressed = anyButtonPressed;
				}
			}
		}

		/// <summary>
		/// Builds a mapping dictionary from InterfacePath to AllInputDeviceInfo for fast lookups.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		private void BuildDeviceMapping(DevicesCombined devicesCombined)
		{
			_deviceMapping = new Dictionary<string, DevicesCombined.AllInputDeviceInfo>();

			foreach (var device in devicesCombined.AllInputDevicesList)
			{
				if (device.InputType == "RawInput" && !string.IsNullOrEmpty(device.InterfacePath))
				{
					_deviceMapping[device.InterfacePath] = device;
				}
			}
		}

		/// <summary>
		/// Checks if any button is pressed in the given HID input report.
		/// </summary>
		/// <param name="interfacePath">Device interface path for opening HID device</param>
		/// <param name="report">The raw HID input report data</param>
		/// <returns>True if any button is pressed, false otherwise</returns>
		/// <remarks>
		/// HID button detection:
		/// • Opens HID device to get preparsed data
		/// • Uses HidP_GetUsages to extract button states from report
		/// • Buttons are represented as usage values in the HID report
		/// • Any non-zero usage count indicates button press
		/// </remarks>
		private bool IsAnyButtonPressedInHidReport(string interfacePath, byte[] report)
		{
			if (report == null || report.Length == 0)
				return false;

			IntPtr hidHandle = IntPtr.Zero;
			IntPtr preparsedData = IntPtr.Zero;

			try
			{
				// Open HID device to get preparsed data
				hidHandle = CreateFile(
					interfacePath,
					GENERIC_READ | GENERIC_WRITE,
					FILE_SHARE_READ | FILE_SHARE_WRITE,
					IntPtr.Zero,
					OPEN_EXISTING,
					0,
					IntPtr.Zero);

				if (hidHandle == INVALID_HANDLE_VALUE || hidHandle == IntPtr.Zero)
					return false;

				// Get preparsed data for HID parsing
				if (!HidD_GetPreparsedData(hidHandle, out preparsedData) || preparsedData == IntPtr.Zero)
					return false;

				// Get HID capabilities
				HIDP_CAPS caps;
				int status = HidP_GetCaps(preparsedData, out caps);
				if (status != HIDP_STATUS_SUCCESS)
					return false;

				// Check if device has any buttons
				if (caps.NumberInputButtonCaps == 0)
					return false;

				// Get button usages from the report
				// Usage page 9 (Button) is standard for gamepad/joystick buttons
				const ushort USAGE_PAGE_BUTTON = 0x09;
				uint usageLength = (uint)(caps.NumberInputButtonCaps * 10); // Allocate enough space
				ushort[] usageList = new ushort[usageLength];

				status = HidP_GetUsages(
					HIDP_REPORT_TYPE.HidP_Input,
					USAGE_PAGE_BUTTON,
					0, // linkCollection
					usageList,
					ref usageLength,
					preparsedData,
					report,
					(uint)report.Length);

				// If we got any button usages, at least one button is pressed
				return status == HIDP_STATUS_SUCCESS && usageLength > 0;
			}
			catch (Exception)
			{
				return false;
			}
			finally
			{
				// Clean up resources
				if (preparsedData != IntPtr.Zero)
					HidD_FreePreparsedData(preparsedData);

				if (hidHandle != IntPtr.Zero && hidHandle != INVALID_HANDLE_VALUE)
					CloseHandle(hidHandle);
			}
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// Call this when device lists change.
		/// </summary>
		public void InvalidateCache()
		{
			_deviceMapping = null;
		}
	}
}
