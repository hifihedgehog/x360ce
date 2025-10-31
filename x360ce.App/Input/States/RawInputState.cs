using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
	internal class RawInputState : IDisposable
	{
		#region Singleton Pattern

		private static readonly object _lock = new object();
		private static RawInputState _instance;

		/// <summary>
		/// Gets the singleton instance of RawInputState.
		/// CRITICAL: Only ONE instance can exist to prevent WM_INPUT message routing conflicts.
		/// </summary>
		public static RawInputState Instance
		{
			get
			{
				if (_instance == null)
				{
					lock (_lock)
					{
						if (_instance == null)
						{
							_instance = new RawInputState();
						}
					}
				}
				return _instance;
			}
		}

		#endregion

		#region Windows Raw Input API
	
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
	
		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
	
		[DllImport("kernel32.dll")]
		private static extern uint GetLastError();
	
		/// <summary>
		/// Gets the current state of a virtual key (including mouse buttons and keyboard keys).
		/// Returns non-zero if the key is currently pressed.
		/// </summary>
		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);
	
		// Virtual key codes for mouse buttons
		private const int VK_LBUTTON = 0x01;  // Left mouse button
		private const int VK_RBUTTON = 0x02;  // Right mouse button
		private const int VK_MBUTTON = 0x04;  // Middle mouse button
		private const int VK_XBUTTON1 = 0x05; // X1 mouse button
		private const int VK_XBUTTON2 = 0x06; // X2 mouse button

		private const uint RIM_TYPEMOUSE = 0;
		private const uint RIM_TYPEKEYBOARD = 1;
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

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWMOUSE
		{
			public ushort usFlags;
			public ushort usButtonFlags;
			public ushort usButtonData;
			public uint ulRawButtons;
			public int lLastX;
			public int lLastY;
			public uint ulExtraInformation;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWKEYBOARD
		{
			public ushort MakeCode;
			public ushort Flags;
			public ushort Reserved;
			public ushort VKey;
			public uint Message;
			public uint ExtraInformation;
		}

		// Mouse button flags
		private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
		private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
		private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
		private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
		private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
		private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
		private const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
		private const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
		private const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
		private const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;

		#endregion

		#region Message Window for WM_INPUT

		/// <summary>
		/// Hidden window that receives WM_INPUT messages without blocking.
		/// </summary>
		private class RawInputMessageWindow : System.Windows.Forms.NativeWindow, IDisposable
		{
			private const int WM_INPUT = 0x00FF;
			private readonly RawInputState _parent;

			public RawInputMessageWindow(RawInputState parent)
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
		private readonly ConcurrentDictionary<IntPtr, byte> _mouseButtonStates = new ConcurrentDictionary<IntPtr, byte>();
		private readonly ConcurrentDictionary<IntPtr, byte[]> _keyboardKeyStates = new ConcurrentDictionary<IntPtr, byte[]>();
		
		// Per-device mouse axis accumulation to prevent conflicts between multiple mouse devices
		private readonly ConcurrentDictionary<IntPtr, int> _mouseAccumulatedX = new ConcurrentDictionary<IntPtr, int>();
		private readonly ConcurrentDictionary<IntPtr, int> _mouseAccumulatedY = new ConcurrentDictionary<IntPtr, int>();
		private readonly ConcurrentDictionary<IntPtr, int> _mouseAccumulatedZ = new ConcurrentDictionary<IntPtr, int>();
		private bool _isInitialized;
		private bool _disposed;
		
		// Device list for immediate state conversion
		private System.Collections.Generic.List<Devices.RawInputDeviceInfo> _deviceInfoList;
		
		// Cached struct sizes to avoid repeated Marshal.SizeOf calls
		private static readonly int s_rawinputDeviceSize = Marshal.SizeOf(typeof(RAWINPUTDEVICE));
		private static readonly int s_rawinputHeaderSize = Marshal.SizeOf(typeof(RAWINPUTHEADER));
		private static readonly int s_rawhidSize = Marshal.SizeOf(typeof(RAWHID));

		/// <summary>
		/// Initializes the RawInput message receiver (non-blocking).
		/// </summary>
		public RawInputState()
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
					System.Diagnostics.Debug.WriteLine($"RawInputState: RegisterRawInputDevices failed with error code: {errorCode}");
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
		/// Handles HID devices (gamepads), mice, and keyboards.
		/// </summary>
		private void ProcessRawInputMessage(IntPtr lParam)
		{
			uint dwSize = 0;
			uint headerSize = (uint)s_rawinputHeaderSize;
			
			// First call: get required buffer size
			uint sizeResult = GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);
			if (sizeResult == uint.MaxValue || dwSize == 0)
				return;
			
			// Debug: Log ALL WM_INPUT messages to see what's arriving
			// System.Diagnostics.Debug.WriteLine($"RawInputState: WM_INPUT message received, size: {dwSize}");
	
			IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
			try
			{
				// Second call: get actual data
				uint result = GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, headerSize);
				if (result == uint.MaxValue || result < headerSize)
					return;
	
				var rawInput = Marshal.PtrToStructure<RAWINPUT>(buffer);
				
				// Debug: Log device type
				string deviceTypeName = rawInput.header.dwType == RIM_TYPEHID ? "HID" :
				                       rawInput.header.dwType == RIM_TYPEMOUSE ? "Mouse" :
				                       rawInput.header.dwType == RIM_TYPEKEYBOARD ? "Keyboard" : "Unknown";
				//System.Diagnostics.Debug.WriteLine($"RawInputState: Processing {deviceTypeName} input, Handle: 0x{rawInput.header.hDevice.ToInt64():X8}");
				
				// Process based on device type
				if (rawInput.header.dwType == RIM_TYPEHID)
				{
					ProcessHidInput(buffer, dwSize, rawInput);
				}
				else if (rawInput.header.dwType == RIM_TYPEMOUSE)
				{
					ProcessMouseInput(buffer, rawInput);
				}
				else if (rawInput.header.dwType == RIM_TYPEKEYBOARD)
				{
					ProcessKeyboardInput(buffer, rawInput);
				}
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		/// <summary>
		/// Processes HID device input (gamepads, joysticks).
		/// </summary>
		private void ProcessHidInput(IntPtr buffer, uint dwSize, RAWINPUT rawInput)
		{
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
	
			// Get interface path from device handle (handles may change between enumeration and runtime)
			string path = null;
			if (!_handleToPath.TryGetValue(rawInput.header.hDevice, out path))
			{
				path = GetDeviceInterfacePath(rawInput.header.hDevice);
				if (!string.IsNullOrEmpty(path))
				{
					_handleToPath[rawInput.header.hDevice] = path;
				}
			}
	
			if (!string.IsNullOrEmpty(path))
			{
				// Check for button state changes before updating cache
				if (_cachedStates.TryGetValue(path, out byte[] previousReport))
				{
					DetectHidButtonStateChanges(path, previousReport, report, rawInput.header.hDevice);
				}
				else
				{
					// First time seeing this device - log initial state
					//Debug.WriteLine($"RawInputState: HID device first seen - Handle: 0x{rawInput.header.hDevice.ToInt64():X8}, Path: {path}, ReportSize: {bytesToCopy}");
				}
				
				// Update cached state with report
				_cachedStates[path] = report;
				
				// IMMEDIATE CONVERSION: Convert and save to device ListInputState property
				ConvertAndUpdateDeviceState(path, report);
			}
		}

		/// <summary>
		/// Processes mouse input and creates a raw report with button states and RAW axis deltas.
		/// Report format: [0]=buttons, [1-4]=X delta (int), [5-8]=Y delta (int), [9-12]=Z delta (int)
		/// </summary>
		private void ProcessMouseInput(IntPtr buffer, RAWINPUT rawInput)
		{
			// Read RAWMOUSE structure from buffer at the correct offset
			// RAWINPUTHEADER is followed immediately by the device-specific data
			int mouseOffset = s_rawinputHeaderSize;
			IntPtr mousePtr = IntPtr.Add(buffer, mouseOffset);
			var mouse = Marshal.PtrToStructure<RAWMOUSE>(mousePtr);
			
			// Debug: Log the raw structure values to verify we're reading correctly
			//System.Diagnostics.Debug.WriteLine($"RawInputState: RAWMOUSE structure - lLastX={mouse.lLastX}, lLastY={mouse.lLastY}, " +
			//	$"usButtonFlags=0x{mouse.usButtonFlags:X4}, usButtonData={mouse.usButtonData}");
	
			// Get current button state for this device (or 0 if first time)
			byte previousButtonState = _mouseButtonStates.GetOrAdd(rawInput.header.hDevice, 0);
			byte buttonState = previousButtonState;
			
			// Track button changes for debug output
			bool hasButtonChanges = mouse.usButtonFlags != 0;
			
			// Update button state based on DOWN events (set bits)
			if ((mouse.usButtonFlags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0)
				buttonState |= 0x01;
			if ((mouse.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0)
				buttonState |= 0x02;
			if ((mouse.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0)
				buttonState |= 0x04;
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_4_DOWN) != 0)
				buttonState |= 0x08;
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_5_DOWN) != 0)
				buttonState |= 0x10;
	
			// Update button state based on UP events (clear bits)
			if ((mouse.usButtonFlags & RI_MOUSE_LEFT_BUTTON_UP) != 0)
				buttonState &= unchecked((byte)~0x01);
			if ((mouse.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_UP) != 0)
				buttonState &= unchecked((byte)~0x02);
			if ((mouse.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_UP) != 0)
				buttonState &= unchecked((byte)~0x04);
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_4_UP) != 0)
				buttonState &= unchecked((byte)~0x08);
			if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_5_UP) != 0)
				buttonState &= unchecked((byte)~0x10);
	
			// Debug: Log mouse button state changes
			if (hasButtonChanges && buttonState != previousButtonState)
			{
				string path = GetDeviceInterfacePath(rawInput.header.hDevice);
				//Debug.WriteLine($"RawInputState: Mouse button change - Handle: 0x{rawInput.header.hDevice.ToInt64():X8}, " +
				//              $"Flags: 0x{mouse.usButtonFlags:X4}, Previous: 0x{previousButtonState:X2}, New: 0x{buttonState:X2}, " +
				 //             $"Path: {path ?? "Unknown"}");
			}
	
			// Store updated button state for this device
			_mouseButtonStates[rawInput.header.hDevice] = buttonState;
	
			// Get interface path
			string devicePath = GetDeviceInterfacePath(rawInput.header.hDevice);
		
			// Get RAW axis deltas from WM_INPUT (NO accumulation, NO sensitivity)
			int rawDeltaX = mouse.lLastX;
			int rawDeltaY = mouse.lLastY;
			int rawDeltaZ = 0;
			
			// Handle mouse wheel (Z axis) - usButtonData contains wheel delta when RI_MOUSE_WHEEL flag is set
			if ((mouse.usButtonFlags & 0x0400) != 0) // RI_MOUSE_WHEEL = 0x0400
			{
				// usButtonData is a signed short representing wheel delta (typically ±120 per notch)
				rawDeltaZ = unchecked((short)mouse.usButtonData);
			}
			
			// Debug: Log RAW deltas from WM_INPUT to verify they're being read correctly
			if (rawDeltaX != 0 || rawDeltaY != 0 || rawDeltaZ != 0)
			{
				System.Diagnostics.Debug.WriteLine($"RawInputState: RAW WM_INPUT deltas - X={rawDeltaX}, Y={rawDeltaY}, Z={rawDeltaZ}, " +
					$"ButtonFlags=0x{mouse.usButtonFlags:X4}, Handle=0x{rawInput.header.hDevice.ToInt64():X8}");
			}
			
			// CRITICAL: Perform accumulation here per-device to prevent conflicts between multiple mice
			// Get current accumulated values for this specific device handle
			int currentX = _mouseAccumulatedX.GetOrAdd(rawInput.header.hDevice, 32767);
			int currentY = _mouseAccumulatedY.GetOrAdd(rawInput.header.hDevice, 32767);
			int currentZ = _mouseAccumulatedZ.GetOrAdd(rawInput.header.hDevice, 0);
			
			// Apply sensitivity and accumulate (default sensitivity: X=20, Y=20, Z=50)
			int sensitivity = 20; // Could be made configurable per device later
			int wheelSensitivity = 50;
			
			int newX = currentX + (rawDeltaX * sensitivity);
			int newY = currentY + (rawDeltaY * sensitivity);
			int newZ = currentZ + (rawDeltaZ * wheelSensitivity);
			
			// Clamp to 0-65535 range
			newX = Math.Max(0, Math.Min(65535, newX));
			newY = Math.Max(0, Math.Min(65535, newY));
			newZ = Math.Max(0, Math.Min(65535, newZ));
			
			// Store accumulated values back to per-device dictionaries
			_mouseAccumulatedX[rawInput.header.hDevice] = newX;
			_mouseAccumulatedY[rawInput.header.hDevice] = newY;
			_mouseAccumulatedZ[rawInput.header.hDevice] = newZ;
			
			// Debug: Log accumulation for this specific device
			if (rawDeltaX != 0 || rawDeltaY != 0 || rawDeltaZ != 0)
			{
				System.Diagnostics.Debug.WriteLine($"RawInputState: Device 0x{rawInput.header.hDevice.ToInt64():X8} accumulated - " +
					$"Deltas: X={rawDeltaX}, Y={rawDeltaY}, Z={rawDeltaZ}, " +
					$"Accumulated: X={newX}, Y={newY}, Z={newZ}");
			}
		
			// Create 13-byte report: [0]=buttons, [1-4]=X accumulated, [5-8]=Y accumulated, [9-12]=Z accumulated
			byte[] report = new byte[13];
			report[0] = buttonState;
			
			// Store ACCUMULATED axis values as int (little-endian, 4 bytes each)
			byte[] xBytes = BitConverter.GetBytes(newX);
			byte[] yBytes = BitConverter.GetBytes(newY);
			byte[] zBytes = BitConverter.GetBytes(newZ);
			
			Array.Copy(xBytes, 0, report, 1, 4);
			Array.Copy(yBytes, 0, report, 5, 4);
			Array.Copy(zBytes, 0, report, 9, 4);
	
			if (!string.IsNullOrEmpty(devicePath))
			{
				// Update cached state with RAW report
				_cachedStates[devicePath] = report;
				// Also update handle-to-path mapping for future lookups
				_handleToPath[rawInput.header.hDevice] = devicePath;
				
				// IMMEDIATE CONVERSION: Convert and save to device ListInputState property
				ConvertAndUpdateDeviceState(devicePath, report);
			}
		}

		/// <summary>
		/// Processes keyboard input and creates a synthetic report with key states.
		/// </summary>
		private void ProcessKeyboardInput(IntPtr buffer, RAWINPUT rawInput)
		{
			// Read RAWKEYBOARD structure from buffer
			int keyboardOffset = s_rawinputHeaderSize;
			IntPtr keyboardPtr = IntPtr.Add(buffer, keyboardOffset);
			var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(keyboardPtr);
	
			// Create a synthetic report with scan code
			// Byte 0: Reserved (0)
			// Byte 1: Reserved (0)
			// Byte 2: Scan code (if key is pressed)
			byte[] report = new byte[8];
			
			// Check if this is a key down event (bit 0 of Flags is 0 for key down)
			bool isKeyDown = (keyboard.Flags & 0x01) == 0;
			
			if (isKeyDown && keyboard.MakeCode != 0)
			{
				report[2] = (byte)keyboard.MakeCode; // Store scan code in byte 2
			}
			
			// Debug: Log keyboard state changes
			if (keyboard.MakeCode != 0)
			{
				string path = GetDeviceInterfacePath(rawInput.header.hDevice);
				//Debug.WriteLine($"RawInputState: Keyboard {(isKeyDown ? "DOWN" : "UP")} - Handle: 0x{rawInput.header.hDevice.ToInt64():X8}, " +
				//              $"VKey: 0x{keyboard.VKey:X2}, MakeCode: 0x{keyboard.MakeCode:X2}, " +
				//              $"Path: {path ?? "Unknown"}");
			}
	
			// Get interface path
			string devicePath = null;
			if (!_handleToPath.TryGetValue(rawInput.header.hDevice, out devicePath))
			{
				devicePath = GetDeviceInterfacePath(rawInput.header.hDevice);
				if (!string.IsNullOrEmpty(devicePath))
				{
					_handleToPath[rawInput.header.hDevice] = devicePath;
				}
			}
	
			if (!string.IsNullOrEmpty(devicePath))
			{
				// Update cached state with report
				_cachedStates[devicePath] = report;
				
				// IMMEDIATE CONVERSION: Convert and save to device ListInputState property
				ConvertAndUpdateDeviceState(devicePath, report);
			}
		}

		/// <summary>
		/// Returns cached RawInput device state (non-blocking).
		/// For mouse and keyboard devices, polls ACTUAL current state using GetAsyncKeyState to ensure accuracy.
		/// </summary>
		/// 

		/// <summary>
		/// Starts listening to WM_INPUT messages from RawInput devices.
		/// Message window is already created in constructor and registered for WM_INPUT.
		/// This method is called when state collection starts (event-driven, NOT timer-based).
		/// </summary>
		/// <param name="startListening">True to start listening (no-op, already listening)</param>
		public void StartListeningWMInputMessagesFromRawInputDevices(bool startListening)
		{
			if (startListening)
			{
				// Message window is already created in constructor and registered for WM_INPUT
				// No additional action needed - messages are automatically processed
				//Debug.WriteLine("RawInputState: Started listening to WM_INPUT messages (event-driven, not timer-based)");
			}
			else
			{
				// This should not be called - use StopListeningWMInputMessagesFromRawInputDevices instead
				//Debug.WriteLine("RawInputState: Warning - StartListeningWMInputMessagesFromRawInputDevices called with false");
			}
		}
	
		/// <summary>
		/// Stops listening to WM_INPUT messages from RawInput devices.
		/// Clears all cached states when stopping.
		/// </summary>
		/// <param name="stopListening">True to stop listening (clears caches)</param>
		public void StopListeningWMInputMessagesFromRawInputDevices(bool stopListening)
		{
			if (stopListening)
			{
				// Clear cached states when stopping
				_cachedStates.Clear();
				_handleToPath.Clear();
				_mouseButtonStates.Clear();
				_keyboardKeyStates.Clear();
				Debug.WriteLine("RawInputState: Stopped listening to WM_INPUT messages and cleared cached states");
			}
		}


        public byte[] GetRawInputState(RawInputDeviceInfo riDeviceInfo)
		{
			if (riDeviceInfo?.InterfacePath == null)
				return null;
	
			// Register device handle to path mapping (thread-safe)
			if (riDeviceInfo.DeviceHandle != IntPtr.Zero)
			{
				_handleToPath.TryAdd(riDeviceInfo.DeviceHandle, riDeviceInfo.InterfacePath);
			}
	
			// For mouse devices, poll button state and return cached axis deltas from WM_INPUT
			if (riDeviceInfo.RawInputDeviceType == RawInputDeviceType.Mouse)
			{
				// Poll actual current button state using GetAsyncKeyState
				byte currentButtonState = GetCurrentMouseButtonState();
				
				// Get cached report from ProcessMouseInput (contains RAW WM_INPUT deltas)
				if (_cachedStates.TryGetValue(riDeviceInfo.InterfacePath, out byte[] cachedReport) && cachedReport != null && cachedReport.Length >= 13)
				{
					// Update button state in cached report
					cachedReport[0] = currentButtonState;
					return cachedReport;
				}
				
				// No cached report yet - return initial state with polled buttons
				byte[] report = new byte[13];
				report[0] = currentButtonState;
				// Axes default to 0 (no movement)
				
				// Update cache
				_cachedStates[riDeviceInfo.InterfacePath] = report;
				
				// Also update accumulated button state for consistency
				if (riDeviceInfo.DeviceHandle != IntPtr.Zero)
				{
					_mouseButtonStates[riDeviceInfo.DeviceHandle] = currentButtonState;
				}
				
				return report;
			}
	
			// For keyboard devices, poll ACTUAL current key state directly
			// This ensures we detect key holds even if WM_INPUT messages are missed between polling intervals
			if (riDeviceInfo.RawInputDeviceType == RawInputDeviceType.Keyboard)
			{
				// Poll actual current keyboard state using GetAsyncKeyState
				byte[] currentKeyState = GetCurrentKeyboardState();
				
				// Update cache with current polled state
				_cachedStates[riDeviceInfo.InterfacePath] = currentKeyState;
				
				// Also update accumulated state for consistency with WM_INPUT processing
				if (riDeviceInfo.DeviceHandle != IntPtr.Zero)
				{
					_keyboardKeyStates[riDeviceInfo.DeviceHandle] = currentKeyState;
				}
				
				return currentKeyState;
			}
	
			// For HID devices, return cached state (non-blocking, thread-safe)
			_cachedStates.TryGetValue(riDeviceInfo.InterfacePath, out byte[] cachedState);
			return cachedState;
		}
	
		/// <summary>
		/// Polls the ACTUAL current mouse button state using GetAsyncKeyState.
		/// This ensures we detect button holds even if WM_INPUT messages are missed.
		/// CRITICAL: This is called at high frequency (up to 1000Hz), so it must be ultra-fast.
		/// </summary>
		/// <returns>Byte with button state bits set (0x01=left, 0x02=right, 0x04=middle, 0x08=X1, 0x10=X2)</returns>
		private static byte GetCurrentMouseButtonState()
		{
			// Optimized: Combine all checks with bitwise operations to reduce branching
			// Each GetAsyncKeyState call checks high bit (0x8000) and shifts result to button bit position
			return (byte)(
				((GetAsyncKeyState(VK_LBUTTON) >> 15) & 0x01) |
				((GetAsyncKeyState(VK_RBUTTON) >> 14) & 0x02) |
				((GetAsyncKeyState(VK_MBUTTON) >> 13) & 0x04) |
				((GetAsyncKeyState(VK_XBUTTON1) >> 12) & 0x08) |
				((GetAsyncKeyState(VK_XBUTTON2) >> 11) & 0x10)
			);
		}
	
		/// <summary>
		/// Polls the ACTUAL current keyboard state using GetAsyncKeyState.
		/// This ensures we detect key holds even if WM_INPUT messages are missed.
		/// CRITICAL: This is called at high frequency (up to 1000Hz), so it must be ultra-fast.
		/// </summary>
		/// <returns>Byte array with keyboard report format (8 bytes: [0]=modifiers, [1]=reserved, [2-7]=scan codes)</returns>
		private static byte[] GetCurrentKeyboardState()
		{
			byte[] report = new byte[8];
			int keyIndex = 2; // Start at byte 2 for scan codes (bytes 0-1 are modifiers/reserved)
	
			// Scan virtual key codes to find pressed keys
			// Skip 0x00-0x07 (undefined/mouse buttons) and 0xFF (reserved)
			// Optimized: Check most common ranges first, early exit when buffer full
			for (int vKey = 0x08; vKey <= 0xFE; vKey++)
			{
				// Skip mouse button virtual keys (already handled by mouse polling)
				if (vKey <= VK_XBUTTON2)
					continue;
	
				// Check if key is currently pressed (high bit set)
				if ((GetAsyncKeyState(vKey) & 0x8000) != 0)
				{
					// Store vKey as scan code (simplified for performance)
					report[keyIndex++] = (byte)vKey;
					
					// Stop after 6 keys (standard USB keyboard report limit)
					if (keyIndex >= 8)
						return report;
				}
			}
	
			return report;
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
	/// Gets the device interface path from a device handle using GetRawInputDeviceInfo.
	/// This is necessary because device handles can change between enumeration and runtime.
	/// </summary>
	private string GetDeviceInterfacePath(IntPtr hDevice)
	{
		uint size = 0;
		GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);

		if (size == 0)
			return null;

		IntPtr buffer = Marshal.AllocHGlobal((int)size * 2); // Unicode characters
		try
		{
			uint result = GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, buffer, ref size);
			return result == uint.MaxValue ? null : Marshal.PtrToStringUni(buffer);
		}
		finally
		{
			Marshal.FreeHGlobal(buffer);
		}
	}

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

	private const uint RIDI_DEVICENAME = 0x20000007;

		/// <summary>
		/// Detects button state changes in HID device reports and logs them for debugging.
		/// This method analyzes the button data portion of HID reports to identify state changes.
		/// </summary>
		/// <param name="devicePath">Device interface path for identification</param>
		/// <param name="previousReport">Previous HID report data</param>
		/// <param name="currentReport">Current HID report data</param>
		/// <param name="deviceHandle">Device handle for identification</param>
		private void DetectHidButtonStateChanges(string devicePath, byte[] previousReport, byte[] currentReport, IntPtr deviceHandle)
		{
			// Skip if reports are null or different sizes
			if (previousReport == null || currentReport == null || previousReport.Length != currentReport.Length)
				return;
			
			// Compare reports byte by byte to detect changes
			bool hasChanges = false;
			for (int i = 0; i < Math.Min(previousReport.Length, currentReport.Length); i++)
			{
				if (previousReport[i] != currentReport[i])
				{
					hasChanges = true;
					break;
				}
			}
			
			// Only log if there are actual changes
			if (hasChanges)
			{
				// Create a compact hex representation of the changed bytes
				var changedBytes = new System.Text.StringBuilder();
				for (int i = 0; i < Math.Min(previousReport.Length, currentReport.Length); i++)
				{
					if (previousReport[i] != currentReport[i])
					{
						if (changedBytes.Length > 0) changedBytes.Append(" ");
						changedBytes.Append($"[{i}]: 0x{previousReport[i]:X2}→0x{currentReport[i]:X2}");
					}
				}
				
				Debug.WriteLine($"RawInputState: HID button change - Handle: 0x{deviceHandle.ToInt64():X8}, " +
				              $"Path: {devicePath}, Changes: {changedBytes}");
			}
		}

		/// <summary>
		/// Sets the device list for immediate state conversion.
		/// This enables ConvertAndUpdateDeviceState to find and update device ListInputState properties.
		/// </summary>
		/// <param name="deviceList">List of RawInput devices to enable immediate conversion for</param>
		public void SetDeviceList(System.Collections.Generic.List<RawInputDeviceInfo> deviceList)
		{
			_deviceInfoList = deviceList;
			
			// CRITICAL: Clear cached mouse reports when device list is updated
			// This prevents stale cached deltas from previous enumeration cycles
			var mouseDevicePaths = new System.Collections.Generic.List<string>();
			foreach (var kvp in _cachedStates)
			{
				// Check if this cached state belongs to a mouse device
				RawInputDeviceInfo mouseDevice = null;
				if (deviceList != null)
				{
					foreach (var device in deviceList)
					{
						if (string.Equals(device.InterfacePath, kvp.Key, StringComparison.OrdinalIgnoreCase))
						{
							mouseDevice = device;
							break;
						}
					}
				}
				
				if (mouseDevice?.RawInputDeviceType == RawInputDeviceType.Mouse)
				{
					mouseDevicePaths.Add(kvp.Key);
				}
			}
			
			// Clear cached reports for mouse devices to prevent stale delta issues
			foreach (var path in mouseDevicePaths)
			{
				_cachedStates.TryRemove(path, out _);
				System.Diagnostics.Debug.WriteLine($"RawInputState: Cleared stale cached mouse report for device: {path}");
			}
			
			//Debug.WriteLine($"RawInputState: Device list set with {deviceList?.Count ?? 0} devices for immediate conversion");
		}

		/// <summary>
		/// Immediately converts RawInput state to ListInputState and updates the device's ListInputState property.
		/// This method is called directly from WM_INPUT message processing for event-driven conversion.
		/// </summary>
		/// <param name="devicePath">Device interface path to identify the device</param>
		/// <param name="rawReport">Raw HID/Mouse/Keyboard report from WM_INPUT message</param>
		private void ConvertAndUpdateDeviceState(string devicePath, byte[] rawReport)
		{
			if (_deviceInfoList == null || string.IsNullOrEmpty(devicePath) || rawReport == null)
				return;

			// Find the device in our list by interface path
			RawInputDeviceInfo device = null;
			foreach (var d in _deviceInfoList)
			{
				if (string.Equals(d.InterfacePath, devicePath, StringComparison.OrdinalIgnoreCase))
				{
					device = d;
					break;
				}
			}
			
			if (device == null)
			{
				// Debug: Only log occasionally to avoid spam for unknown devices
				//Debug.WriteLine($"RawInputState: Device not found in list for immediate conversion - Path: {devicePath}");
				return;
			}

			// Convert RawInput state to ListInputState and update device immediately
			var listInputState = RawInputStateToListInputState.ConvertRawInputStateToListInputStateAndUpdate(rawReport, device);
			
			// Debug: Log the immediate conversion result
			if (listInputState != null)
			{
				//Debug.WriteLine($"RawInputState: Immediate conversion and update successful - " +
				//              $"Handle: 0x{device.DeviceHandle.ToInt64():X8}, " +
				 //             $"Type: {device.RawInputDeviceType}");
			}
			else
			{
				//Debug.WriteLine($"RawInputState: Immediate conversion failed - " +
				 //             $"Handle: 0x{device.DeviceHandle.ToInt64():X8}, " +
				 //             $"Type: {device.RawInputDeviceType}");
			}
		}

		public void Dispose()
		{
			if (_disposed)
				return;
	
			_messageWindow?.Dispose();
			_cachedStates.Clear();
			_handleToPath.Clear();
			_mouseButtonStates.Clear();
			_keyboardKeyStates.Clear();
			_disposed = true;
		}

		public string GetDiagnosticInfo()
		{
			// Optimized: Use string interpolation with pre-calculated values
			int cachedCount = _cachedStates.Count;
			int trackedCount = _handleToPath.Count;
			
			return $"=== RawInput State Reader (Non-Blocking) ===\n" +
				   $"Initialized: {_isInitialized}\n" +
				   $"Cached States: {cachedCount}\n" +
				   $"Tracked Devices: {trackedCount}\n\n" +
				   "Implementation: WM_INPUT message-based (non-blocking)\n" +
				   "✅ Safe for concurrent use with all input methods\n";
		}
	}
}
