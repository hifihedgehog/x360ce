using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.States
{
	/// <summary>
	/// Provides NON-BLOCKING RawInput device state reading using Windows WM_INPUT messages.
	/// This is the ONLY way to read RawInput without blocking other input methods.
	/// </summary>
	/// <remarks>
	/// NON-BLOCKING ARCHITECTURE:
	/// • Uses Windows Raw Input API (RegisterRawInputDevices + WM_INPUT messages)
	/// • Message-based system runs in background without blocking
	/// • Caches latest states from WM_INPUT messages
	/// • Never opens HID device handles directly
	/// • Safe for concurrent use with DirectInput, XInput, GamingInput
	/// 
	/// This is a lightweight, self-contained implementation that doesn't depend on
	/// RawInputProcessor or any other processor classes.
	/// </remarks>
	internal class StatesRawInput : IDisposable
	{
		#region Windows Raw Input API

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

		[DllImport("kernel32.dll")]
		private static extern uint GetLastError();

		private const uint RIM_TYPEHID = 2;
		private const uint RID_INPUT = 0x10000003;
		private const uint RIDEV_INPUTSINK = 0x00000100;
		private const ushort USAGE_PAGE_GENERIC_DESKTOP = 0x01;
		private const ushort USAGE_JOYSTICK = 0x04;
		private const ushort USAGE_GAMEPAD = 0x05;
		private const ushort USAGE_MULTI_AXIS = 0x08;
		// RAWHID_DATA_OFFSET removed - now computed dynamically using Marshal.SizeOf

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWINPUTDEVICE
		{
			public ushort usUsagePage;
			public ushort usUsage;
			public uint dwFlags;
			public IntPtr hwndTarget;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWINPUTHEADER
		{
			public uint dwType;
			public uint dwSize;
			public IntPtr hDevice;
			public IntPtr wParam;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWINPUT
		{
			public RAWINPUTHEADER header;
			public RAWHID hid;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWHID
		{
			public uint dwSizeHid;
			public uint dwCount;
			// Variable length data follows
		}

		#endregion

		#region Message Window for WM_INPUT

		/// <summary>
		/// Hidden window that receives WM_INPUT messages without blocking.
		/// </summary>
		private class RawInputMessageWindow : System.Windows.Forms.NativeWindow, IDisposable
		{
			private const int WM_INPUT = 0x00FF;
			private readonly StatesRawInput _parent;

			public RawInputMessageWindow(StatesRawInput parent)
			{
				_parent = parent;
				CreateHandle(new System.Windows.Forms.CreateParams
				{
					Caption = "RawInputStatesWindow",
					Style = 0,
					ExStyle = 0,
					ClassStyle = 0,
					X = 0,
					Y = 0,
					Width = 0,
					Height = 0,
					Parent = IntPtr.Zero
				});
			}

			protected override void WndProc(ref System.Windows.Forms.Message m)
			{
				if (m.Msg == WM_INPUT)
				{
					_parent.ProcessRawInputMessage(m.LParam);
				}
				base.WndProc(ref m);
			}

			public void Dispose()
			{
				if (Handle != IntPtr.Zero)
					DestroyHandle();
			}
		}

		#endregion

		private RawInputMessageWindow _messageWindow;
		private readonly ConcurrentDictionary<string, byte[]> _cachedStates = new ConcurrentDictionary<string, byte[]>();
		private readonly ConcurrentDictionary<IntPtr, string> _handleToPath = new ConcurrentDictionary<IntPtr, string>();
		private bool _isInitialized;
		private bool _disposed;
		
		// Cached struct sizes to avoid repeated Marshal.SizeOf calls
		private static readonly int s_rawinputDeviceSize = Marshal.SizeOf(typeof(RAWINPUTDEVICE));
		private static readonly int s_rawinputHeaderSize = Marshal.SizeOf(typeof(RAWINPUTHEADER));
		private static readonly int s_rawhidSize = Marshal.SizeOf(typeof(RAWHID));

		/// <summary>
		/// Initializes the RawInput message receiver (non-blocking).
		/// </summary>
		public StatesRawInput()
		{
			try
			{
				// Create hidden window for WM_INPUT messages
				_messageWindow = new RawInputMessageWindow(this);

				// Register for ALL input devices: Gaming devices, Keyboard, and Mouse
				// This ensures we receive WM_INPUT messages from all input device types
				var devices = new RAWINPUTDEVICE[5];
				
				// Joystick (0x04) - Flight sticks, racing wheels, Xbox controllers often report as this
				devices[0].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[0].usUsage = USAGE_JOYSTICK;
				devices[0].dwFlags = RIDEV_INPUTSINK;
				devices[0].hwndTarget = _messageWindow.Handle;
				
				// Gamepad (0x05) - Standard gamepads
				devices[1].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[1].usUsage = USAGE_GAMEPAD;
				devices[1].dwFlags = RIDEV_INPUTSINK;
				devices[1].hwndTarget = _messageWindow.Handle;
				
				// Multi-axis Controller (0x08) - Complex controllers with many axes
				devices[2].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[2].usUsage = USAGE_MULTI_AXIS;
				devices[2].dwFlags = RIDEV_INPUTSINK;
				devices[2].hwndTarget = _messageWindow.Handle;
				
				// Keyboard (0x06) - All keyboard devices
				devices[3].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[3].usUsage = 0x06;
				devices[3].dwFlags = RIDEV_INPUTSINK;
				devices[3].hwndTarget = _messageWindow.Handle;
				
				// Mouse (0x02) - All mouse devices
				devices[4].usUsagePage = USAGE_PAGE_GENERIC_DESKTOP;
				devices[4].usUsage = 0x02;
				devices[4].dwFlags = RIDEV_INPUTSINK;
				devices[4].hwndTarget = _messageWindow.Handle;
	
				bool success = RegisterRawInputDevices(devices, 5, (uint)s_rawinputDeviceSize);
				if (!success)
				{
					uint errorCode = GetLastError();
					System.Diagnostics.Debug.WriteLine($"StatesRawInput: RegisterRawInputDevices failed with error code: {errorCode}");
				}
				_isInitialized = success;
			}
			catch
			{
				_isInitialized = false;
			}
		}

		/// <summary>
		/// Processes WM_INPUT messages and caches device states (non-blocking).
		/// </summary>
		private void ProcessRawInputMessage(IntPtr lParam)
		{
			uint dwSize = 0;
			uint headerSize = (uint)s_rawinputHeaderSize;
			
			// First call: get required buffer size
			uint sizeResult = GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);
			if (sizeResult == uint.MaxValue || dwSize == 0)
				return;

			IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
			try
			{
				// Second call: get actual data
				uint result = GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, headerSize);
				if (result == uint.MaxValue || result < headerSize)
					return;

				var rawInput = Marshal.PtrToStructure<RAWINPUT>(buffer);
				if (rawInput.header.dwType != RIM_TYPEHID)
					return;

				// Compute sizes dynamically to avoid hard-coded offsets
				int offset = s_rawinputHeaderSize + s_rawhidSize;
				int totalAvailable = (int)dwSize - offset;
				int hidReportSize = (int)rawInput.hid.dwSizeHid;
				int hidCount = Math.Max(1, (int)rawInput.hid.dwCount);
				int bytesToCopy = Math.Min(totalAvailable, hidReportSize * hidCount);

				if (bytesToCopy <= 0)
					return;

				// Copy HID report data (handles multiple reports if dwCount > 1)
				byte[] report = new byte[bytesToCopy];
				Marshal.Copy(IntPtr.Add(buffer, offset), report, 0, bytesToCopy);

				// Cache the report by device handle
				if (_handleToPath.TryGetValue(rawInput.header.hDevice, out string path))
				{
					_cachedStates[path] = report;
				}
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		/// <summary>
		/// Returns cached RawInput device state (non-blocking).
		/// </summary>
		public byte[] GetRawInputDeviceState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.InterfacePath == null)
				return null;

			// Register device handle to path mapping (thread-safe)
			if (deviceInfo.DeviceHandle != IntPtr.Zero)
			{
				_handleToPath.TryAdd(deviceInfo.DeviceHandle, deviceInfo.InterfacePath);
			}
	
			// Return cached state (non-blocking, thread-safe)
			_cachedStates.TryGetValue(deviceInfo.InterfacePath, out byte[] cachedState);
			return cachedState;
		}

		/// <summary>
		/// Returns cached RawInput device state and clears it (non-blocking).
		/// This ensures button detection only happens when NEW messages arrive between checks.
		/// </summary>
		public byte[] GetAndClearRawInputDeviceState(RawInputDeviceInfo deviceInfo)
		{
			if (deviceInfo?.InterfacePath == null)
				return null;

			// Register device handle to path mapping (thread-safe)
			if (deviceInfo.DeviceHandle != IntPtr.Zero)
			{
				_handleToPath.TryAdd(deviceInfo.DeviceHandle, deviceInfo.InterfacePath);
			}
	
			// Get and remove cached state in one operation (non-blocking, thread-safe)
			_cachedStates.TryRemove(deviceInfo.InterfacePath, out byte[] cachedState);
			return cachedState;
		}

		/// <summary>
		/// Returns cached state by interface path (non-blocking).
		/// </summary>
		public byte[] GetRawInputState(string interfacePath)
		{
			if (string.IsNullOrEmpty(interfacePath))
				return null;
			
			_cachedStates.TryGetValue(interfacePath, out byte[] cachedState);
			return cachedState;
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_messageWindow?.Dispose();
			_cachedStates.Clear();
			_handleToPath.Clear();
			_disposed = true;
		}

		public string GetDiagnosticInfo()
		{
			var info = new System.Text.StringBuilder(256); // Pre-allocate capacity
			info.AppendLine("=== RawInput State Reader (Non-Blocking) ===");
			info.AppendLine($"Initialized: {_isInitialized}");
			info.AppendLine($"Cached States: {_cachedStates.Count}");
			info.AppendLine($"Tracked Devices: {_handleToPath.Count}");
			info.AppendLine();
			info.AppendLine("Implementation: WM_INPUT message-based (non-blocking)");
			info.AppendLine("✅ Safe for concurrent use with all input methods");
			return info.ToString();
		}
	}
}
