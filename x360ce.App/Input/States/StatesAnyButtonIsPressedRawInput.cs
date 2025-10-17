using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides NON-BLOCKING button press detection for RawInput devices.
	/// Uses WM_INPUT message-based system that doesn't block other input methods.
	/// Now uses proper HidP_GetUsages() API for reliable button detection.
	/// </summary>
	/// <remarks>
	/// NON-BLOCKING IMPLEMENTATION:
	/// • Uses StatesRawInput which receives WM_INPUT messages
	/// • Never opens HID device handles
	/// • Safe for concurrent use with DirectInput, XInput, GamingInput
	/// • Uses Windows HID API (HidP_GetUsages) for proper button reading
	/// • Works with ALL device layouts (buttons before/after axes)
	/// </remarks>
	internal class StatesAnyButtonIsPressedRawInput
	{
		#region Windows HID API
		
		[DllImport("hid.dll", SetLastError = true)]
		private static extern int HidP_GetUsages(
			ushort ReportType,
			ushort UsagePage,
			ushort LinkCollection,
			[Out] ushort[] UsageList,
			ref int UsageLength,
			IntPtr PreparsedData,
			IntPtr Report,
			int ReportLength);
		
		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetRawInputDeviceInfo(
			IntPtr hDevice,
			uint uiCommand,
			IntPtr pData,
			ref uint pcbSize);
		
		private const ushort HidP_Input = 0;
		private const ushort HID_USAGE_PAGE_BUTTON = 0x09;
		private const int HIDP_STATUS_SUCCESS = 0x00110000;
		private const uint RIDI_PREPARSEDDATA = 0x20000005;
		
		#endregion
		private readonly StatesRawInput _statesRawInput = new StatesRawInput();
		private Dictionary<string, DevicesCombined.AllInputDeviceInfo> _deviceMapping;
		private Dictionary<string, RawInputDeviceInfo> _rawInputDeviceInfo; // Cache full device info for report layout
		private Dictionary<string, IntPtr> _preparsedDataCache; // Cache preparsed data for HidP_GetUsages
		private int _lastDeviceCount;

		/// <summary>
		/// Checks each RawInput device for button presses using cached WM_INPUT message data.
		/// CRITICAL: This method is NON-BLOCKING - uses message-based system, never opens handles.
		/// Safe for concurrent use with all other input methods.
		/// Button state persists until a new WM_INPUT message arrives with different state.
		/// </summary>
		/// <param name="devicesCombined">The combined devices instance containing device lists</param>
		public void CheckRawInputDevicesIfAnyButtonIsPressed(DevicesCombined devicesCombined)
		{
			var rawInputList = devicesCombined?.RawInputDevicesList;
			var allDevicesList = devicesCombined?.AllInputDevicesList;
			
			if (rawInputList == null || allDevicesList == null)
				return;

			// Build mapping cache on first run or when device count changes
			int currentCount = rawInputList.Count;
			if (_deviceMapping == null || _lastDeviceCount != currentCount)
			{
				BuildDeviceMapping(allDevicesList, rawInputList);
				_lastDeviceCount = currentCount;
			}

			// Check each RawInput device
			foreach (var riDevice in rawInputList)
			{
				// Skip invalid devices
				if (riDevice?.InterfacePath == null)
					continue;
	
				// Fast lookup - single dictionary access
				if (!_deviceMapping.TryGetValue(riDevice.InterfacePath, out var allDevice))
					continue;
	
				// Get cached state from WM_INPUT messages (non-blocking)
				var report = _statesRawInput.GetRawInputDeviceState(riDevice);
				
				// Get device info for this device (contains report layout information)
				RawInputDeviceInfo deviceInfo = null;
				_rawInputDeviceInfo?.TryGetValue(riDevice.InterfacePath, out deviceInfo);
				
				// Use proper HID API to detect button presses
				bool buttonPressed = report != null && deviceInfo != null &&
					HasButtonPressedUsingHidApi(riDevice.InterfacePath, riDevice.DeviceHandle, report, deviceInfo);
				
				// DEBUG: Log state changes every second - GAMEPADS ONLY (exclude keyboards and mice)
				if (deviceInfo != null &&
					deviceInfo.RawInputDeviceType == RawInputDeviceType.HID &&
					(!_lastLogTime.HasValue || (System.DateTime.Now - _lastLogTime.Value).TotalSeconds >= 1.0))
				{
					_lastLogTime = System.DateTime.Now;
					var reportHex = report != null ? System.BitConverter.ToString(report).Replace("-", " ") : "null";
					var pathPreview = riDevice.InterfacePath?.Length > 60
						? riDevice.InterfacePath.Substring(0, 60) + "..."
						: riDevice.InterfacePath ?? "null";
					
					// Calculate button byte range for debugging
					int buttonByteCount = (deviceInfo.ButtonCount + 7) >> 3;
					int buttonEndIndex = deviceInfo.ButtonDataOffset + buttonByteCount;
					string buttonBytesHex = "none";
					if (report != null && report.Length >= buttonEndIndex)
					{
						var buttonBytes = new byte[buttonByteCount];
						System.Array.Copy(report, deviceInfo.ButtonDataOffset, buttonBytes, 0, buttonByteCount);
						buttonBytesHex = System.BitConverter.ToString(buttonBytes).Replace("-", " ");
					}
					
					System.Diagnostics.Debug.WriteLine($"RawInput Gamepad State: " +
						$"Path={pathPreview}, " +
						$"ButtonPressed={buttonPressed}, ReportLength={report?.Length ?? 0}, " +
						$"ButtonCount={deviceInfo.ButtonCount}, ButtonOffset={deviceInfo.ButtonDataOffset}, " +
						$"ButtonByteRange=[{deviceInfo.ButtonDataOffset}..{buttonEndIndex-1}], " +
						$"ButtonBytes=[{buttonBytesHex}], " +
						$"FullReport=[{reportHex}]");
				}
				
				allDevice.ButtonPressed = buttonPressed;
			}
		}

		private System.DateTime? _lastLogTime;

		/// <summary>
		/// Builds a mapping dictionary from InterfacePath to AllInputDeviceInfo for fast lookups.
		/// Also builds button byte count cache from RawInputDeviceInfo.
		/// </summary>
		private void BuildDeviceMapping(
			System.Collections.ObjectModel.ObservableCollection<DevicesCombined.AllInputDeviceInfo> allDevicesList,
			System.Collections.Generic.List<RawInputDeviceInfo> rawInputList)
		{
			// Pre-allocate with estimated capacity to reduce resizing
			_deviceMapping = new Dictionary<string, DevicesCombined.AllInputDeviceInfo>(allDevicesList.Count);
			_rawInputDeviceInfo = new Dictionary<string, RawInputDeviceInfo>(rawInputList.Count);
			_preparsedDataCache = new Dictionary<string, IntPtr>(rawInputList.Count);
	
			foreach (var device in allDevicesList)
			{
				// Single condition check with null-coalescing
				if (device.InputType == "RawInput" && device.InterfacePath != null)
					_deviceMapping[device.InterfacePath] = device;
			}
			
			// Build device info cache from RawInputDeviceInfo (contains report layout information)
			foreach (var riDevice in rawInputList)
			{
				if (riDevice?.InterfacePath == null)
					continue;
				
				// Store full device info for report layout access
				_rawInputDeviceInfo[riDevice.InterfacePath] = riDevice;
			}
		}

		/// <summary>
		/// Checks if any button/key is pressed in the input report using actual device button count.
		/// Supports HID devices (gamepads), keyboards, and mice.
		/// Optimized for performance in high-frequency loops.
		/// </summary>
		/// <param name="report">The raw input report data from WM_INPUT messages</param>
		/// <param name="deviceInfo">Device information containing report layout (report IDs, button counts, offsets)</param>
		/// <returns>True if any button/key is pressed, false otherwise</returns>
		/// <remarks>
		/// HID Report Structure:
		/// • Byte 0: Report ID (if UsesReportIds is true), otherwise button data starts at byte 0
		/// • Bytes [ButtonDataOffset] to [ButtonDataOffset + buttonByteCount - 1]: Button states (bit-packed)
		/// • Remaining bytes: Axis data (X, Y, Z, Rz, etc.) - EXCLUDED from button detection
		///
		/// Keyboard/Mouse Reports:
		/// • Different structure - any non-zero byte indicates key/button press
		/// • Keyboards: Scan codes in report indicate pressed keys
		/// • Mice: Button flags in report indicate pressed buttons
		///
		/// ENHANCED FIX: Uses Report ID detection and actual button count from HID descriptor.
		/// This eliminates false positives from axis data and correctly handles devices with/without Report IDs.
		///
		/// Performance optimizations:
		/// • Removed try-catch (exception handling is expensive in hot paths)
		/// • Direct array access with bounds check
		/// • Early return on first button press
		/// • Simplified loop logic
		/// • Uses device-specific report layout from HID descriptor
		/// </remarks>
		private static bool HasButtonPressed(byte[] report, RawInputDeviceInfo deviceInfo)
		{
			// Validate inputs
			if (report == null || report.Length < 1 || deviceInfo == null)
				return false;
	
			// Handle keyboard devices
			if (deviceInfo.RawInputDeviceType == RawInputDeviceType.Keyboard)
			{
				// Keyboard: Check scan code bytes (2-7), ignore modifiers (byte 0) and reserved (byte 1)
				if (report.Length < 3)
					return false;
				
				// Optimized: Check up to 6 scan codes with single loop
				int maxIndex = report.Length < 8 ? report.Length : 8;
				for (int i = 2; i < maxIndex; i++)
				{
					if (report[i] != 0)
						return true;
				}
				return false;
			}
			
			// Handle mouse devices
			if (deviceInfo.RawInputDeviceType == RawInputDeviceType.Mouse)
			{
				// Mouse: Check button flags in first byte only
				return report[0] != 0;
			}
			
			// Handle HID devices (gamepads, joysticks)
			int buttonCount = deviceInfo.ButtonCount;
			if (buttonCount <= 0)
				return false;
			
			int buttonDataOffset = deviceInfo.ButtonDataOffset;
			int buttonByteCount = (buttonCount + 7) >> 3; // Optimized: bit shift instead of division
			int minReportSize = buttonDataOffset + buttonByteCount;
			
			if (report.Length < minReportSize)
				return false;
			
			// CRITICAL FIX: Calculate end index more carefully to avoid reading axis data
			// Button data is bit-packed, so we need exactly buttonByteCount bytes, no more
			int buttonEndIndex = buttonDataOffset + buttonByteCount;
			
			// SAFETY CHECK: Ensure we don't read beyond report length
			if (buttonEndIndex > report.Length)
			{
				// If calculated end exceeds report length, adjust to actual available button bytes
				buttonEndIndex = report.Length;
				// Recalculate actual button byte count based on available space
				buttonByteCount = buttonEndIndex - buttonDataOffset;
				if (buttonByteCount <= 0)
					return false;
			}
			
			// CRITICAL FIX: Check button bytes using bit-level analysis to avoid false positives
			// Only check the actual button bits, not the entire bytes (which may contain padding)
			int bitsToCheck = buttonCount; // Exact number of button bits to check
			
			for (int buttonBit = 0; buttonBit < bitsToCheck; buttonBit++)
			{
				// Calculate which byte and bit position this button occupies
				int currentByteIndex = buttonDataOffset + (buttonBit >> 3); // buttonBit / 8
				int currentBitIndex = buttonBit & 7; // buttonBit % 8
				
				// Safety check: ensure we're within report bounds
				if (currentByteIndex >= report.Length)
					break;
				
				// Check if this specific button bit is set
				byte buttonByte = report[currentByteIndex];
				bool isPressed = (buttonByte & (1 << currentBitIndex)) != 0;
				
				if (isPressed)
					return true;
			}
			
			return false;
		}
		
		/// <summary>
		/// Detects button presses using proper Windows HID API (HidP_GetUsages).
		/// This is the CORRECT way to read buttons - works with ALL device layouts.
		/// </summary>
		/// <param name="devicePath">Device interface path</param>
		/// <param name="deviceHandle">Device handle for getting preparsed data</param>
		/// <param name="report">HID report data</param>
		/// <param name="deviceInfo">Device information</param>
		/// <returns>True if any button is pressed</returns>
		private bool HasButtonPressedUsingHidApi(string devicePath, IntPtr deviceHandle, byte[] report, RawInputDeviceInfo deviceInfo)
		{
			try
			{
				// Get or cache preparsed data for this device
				if (!_preparsedDataCache.TryGetValue(devicePath, out IntPtr preparsedData))
				{
					preparsedData = GetPreparsedData(deviceHandle);
					if (preparsedData == IntPtr.Zero)
					{
						// Fallback to manual parsing if HID API fails
						return HasButtonPressed(report, deviceInfo);
					}
					_preparsedDataCache[devicePath] = preparsedData;
				}
				
				// Allocate buffer for button usages (max 128 buttons should be enough)
				ushort[] usageList = new ushort[128];
				int usageLength = usageList.Length;
				
				// Pin the report data for unmanaged access
				GCHandle reportHandle = GCHandle.Alloc(report, GCHandleType.Pinned);
				try
				{
					IntPtr reportPtr = reportHandle.AddrOfPinnedObject();
					
					// Call HidP_GetUsages to get all pressed buttons
					int status = HidP_GetUsages(
						HidP_Input,
						HID_USAGE_PAGE_BUTTON,
						0, // LinkCollection
						usageList,
						ref usageLength,
						preparsedData,
						reportPtr,
						report.Length);
					
					// If successful and we found pressed buttons, return true
					if (status == HIDP_STATUS_SUCCESS && usageLength > 0)
					{
						return true;
					}
				}
				finally
				{
					reportHandle.Free();
				}
				
				// No buttons pressed or API call failed
				return false;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"RawInput HidP_GetUsages error: {ex.Message}");
				// Fallback to manual parsing
				return HasButtonPressed(report, deviceInfo);
			}
		}
		
		/// <summary>
		/// Gets preparsed data for a device handle.
		/// </summary>
		/// <param name="deviceHandle">Device handle</param>
		/// <returns>Preparsed data pointer or IntPtr.Zero if failed</returns>
		private IntPtr GetPreparsedData(IntPtr deviceHandle)
		{
			try
			{
				// Get size of preparsed data
				uint size = 0;
				GetRawInputDeviceInfo(deviceHandle, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
				
				if (size == 0)
					return IntPtr.Zero;
				
				// Allocate and get preparsed data
				IntPtr preparsedData = Marshal.AllocHGlobal((int)size);
				uint result = GetRawInputDeviceInfo(deviceHandle, RIDI_PREPARSEDDATA, preparsedData, ref size);
				
				if (result == uint.MaxValue || result == 0)
				{
					Marshal.FreeHGlobal(preparsedData);
					return IntPtr.Zero;
				}
				
				return preparsedData;
			}
			catch
			{
				return IntPtr.Zero;
			}
		}

		/// <summary>
		/// Invalidates the device mapping cache, forcing a rebuild on next check.
		/// </summary>
		public void InvalidateCache()
		{
			// Free preparsed data before clearing cache
			if (_preparsedDataCache != null)
			{
				foreach (var preparsedData in _preparsedDataCache.Values)
				{
					if (preparsedData != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(preparsedData);
					}
				}
			}
			
			_deviceMapping = null;
			_rawInputDeviceInfo = null;
			_preparsedDataCache = null;
			_lastDeviceCount = 0;
		}
	}
}
