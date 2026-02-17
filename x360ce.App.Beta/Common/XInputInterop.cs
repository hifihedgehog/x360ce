using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using SharpDX.DirectInput;

namespace x360ce.App
{
	/// <summary>
	/// Direct P/Invoke against the real System32\xinput1_4.dll.
	/// Safe alongside Xidi because Xidi only replaces dinput8.dll.
	/// XInput has no cooperative level, no window handle, no acquisition.
	/// Vibration works regardless of foreground state.
	/// </summary>
	public static class XInputInterop
	{
		#region Structs

		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_VIBRATION
		{
			public ushort wLeftMotorSpeed;
			public ushort wRightMotorSpeed;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_GAMEPAD
		{
			public ushort wButtons;
			public byte bLeftTrigger;
			public byte bRightTrigger;
			public short sThumbLX;
			public short sThumbLY;
			public short sThumbRX;
			public short sThumbRY;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_STATE
		{
			public uint dwPacketNumber;
			public XINPUT_GAMEPAD Gamepad;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct XINPUT_CAPABILITIES
		{
			public byte Type;
			public byte SubType;
			public ushort Flags;
			public XINPUT_GAMEPAD Gamepad;
			public XINPUT_VIBRATION Vibration;
		}

		#endregion

		#region Button flag constants

		// Standard buttons
		public const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
		public const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
		public const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
		public const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
		public const ushort XINPUT_GAMEPAD_START = 0x0010;
		public const ushort XINPUT_GAMEPAD_BACK = 0x0020;
		public const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
		public const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
		public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
		public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
		// Guide button — only returned by XInputGetStateEx (ordinal #100)
		public const ushort XINPUT_GAMEPAD_GUIDE = 0x0400;
		// Share button — NOT returned by XInput; read via HID side-channel
		public const ushort XINPUT_GAMEPAD_SHARE = 0x0800;
		public const ushort XINPUT_GAMEPAD_A = 0x1000;
		public const ushort XINPUT_GAMEPAD_B = 0x2000;
		public const ushort XINPUT_GAMEPAD_X = 0x4000;
		public const ushort XINPUT_GAMEPAD_Y = 0x8000;

		// XInput capability flags
		public const ushort XINPUT_CAPS_FFB_SUPPORTED = 0x0001;
		public const ushort XINPUT_CAPS_WIRELESS = 0x0002;
		public const ushort XINPUT_CAPS_NO_NAVIGATION = 0x0010;

		// XInput device subtypes
		public const byte XINPUT_DEVSUBTYPE_UNKNOWN = 0x00;
		public const byte XINPUT_DEVSUBTYPE_GAMEPAD = 0x01;

		// XInputGetCapabilities flag
		public const uint XINPUT_FLAG_GAMEPAD = 0x00000001;

		#endregion

		#region P/Invoke

		[DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
		private static extern uint _XInputSetState(
			uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

		// Standard XInputGetState — does NOT return Guide button.
		[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
		private static extern uint _XInputGetState(
			uint dwUserIndex, ref XINPUT_STATE pState);

		// ═══════════════════════════════════════════════════════
		// XInputGetStateEx — hidden ordinal #100.
		// Identical to XInputGetState but includes the Guide
		// button as bit 0x0400 in XINPUT_GAMEPAD.wButtons.
		// ═══════════════════════════════════════════════════════
		[DllImport("xinput1_4.dll", EntryPoint = "#100")]
		private static extern uint _XInputGetStateEx(
			uint dwUserIndex, ref XINPUT_STATE pState);

		[DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
		private static extern uint _XInputGetCapabilities(
			uint dwUserIndex, uint dwFlags, ref XINPUT_CAPABILITIES pCapabilities);

		#endregion

		private const uint ERROR_SUCCESS = 0;
		private const uint ERROR_DEVICE_NOT_CONNECTED = 0x048F;

		#region Synthetic device identity

		/// <summary>
		/// Well-known ProductGuid for synthetic "Native XInput" devices
		/// created by x360ce. Last 6 bytes spell "XINPAT" in ASCII.
		/// </summary>
		public static readonly Guid NativeXInputProductGuid =
			new Guid("584e4950-5554-0000-0000-58494e504154");

		/// <summary>
		/// Generate a stable, deterministic InstanceGuid for a given XInput slot.
		/// Each slot gets a unique GUID that persists across restarts.
		/// </summary>
		public static Guid GetSyntheticInstanceGuid(uint slot)
		{
			// Base:  584e4950-5554-00SS-0000-58494e504154
			// where SS = slot index (00-03)
			var bytes = NativeXInputProductGuid.ToByteArray();
			bytes[7] = (byte)(slot & 0xFF);
			return new Guid(bytes);
		}

		/// <summary>
		/// Returns true if a ProductGuid matches our synthetic "Native XInput" identifier.
		/// </summary>
		public static bool IsSyntheticXInputDevice(Guid productGuid)
		{
			return productGuid == NativeXInputProductGuid;
		}

		/// <summary>
		/// Extract the XInput slot (0-3) from a synthetic InstanceGuid.
		/// Returns null if the GUID isn't one of ours.
		/// </summary>
		public static uint? GetSlotFromSyntheticGuid(Guid instanceGuid)
		{
			for (uint i = 0; i < 4; i++)
			{
				if (instanceGuid == GetSyntheticInstanceGuid(i))
					return i;
			}
			return null;
		}

		#endregion

		#region ViGEm virtual controller detection

		/// <summary>
		/// Set of XInput user indices currently occupied by x360ce's own
		/// ViGEm virtual controllers. Updated by the ViGEm feeder code
		/// whenever a virtual controller is plugged in or unplugged.
		/// </summary>
		private static readonly HashSet<uint> _vigemOwnedSlots = new HashSet<uint>();
		private static readonly object _vigemSlotsLock = new object();

		/// <summary>
		/// Register an XInput slot as being owned by x360ce's ViGEm bus.
		/// Call this when a virtual controller is plugged in.
		/// </summary>
		public static void RegisterViGEmSlot(uint slot)
		{
			lock (_vigemSlotsLock)
				_vigemOwnedSlots.Add(slot);
		}

		/// <summary>
		/// Unregister an XInput slot from x360ce's ViGEm bus.
		/// Call this when a virtual controller is unplugged.
		/// </summary>
		public static void UnregisterViGEmSlot(uint slot)
		{
			lock (_vigemSlotsLock)
				_vigemOwnedSlots.Remove(slot);
		}

		/// <summary>
		/// Check whether the given XInput slot is owned by x360ce's own
		/// ViGEm virtual controller. These must be skipped during native
		/// XInput enumeration to avoid feedback loops.
		/// </summary>
		public static bool IsViGEmOwnedSlot(uint slot)
		{
			lock (_vigemSlotsLock)
				return _vigemOwnedSlots.Contains(slot);
		}

		/// <summary>
		/// Returns a snapshot of all currently registered ViGEm-owned slots.
		/// </summary>
		public static HashSet<uint> GetViGEmOwnedSlots()
		{
			lock (_vigemSlotsLock)
				return new HashSet<uint>(_vigemOwnedSlots);
		}

		/// <summary>
		/// Attempt to detect whether an XInput slot is a ViGEm virtual
		/// controller by examining its capabilities. ViGEm Xbox 360
		/// controllers typically report SubType 0x01 (Gamepad) and do NOT
		/// report the wireless flag. This is a heuristic fallback used
		/// when the ViGEm slot registration is not available.
		///
		/// NOTE: This heuristic alone is NOT reliable enough to distinguish
		/// real from virtual controllers. It is only used as extra confirmation
		/// alongside the slot registration approach.
		/// </summary>
		public static bool GetCapabilities(uint userIndex, out XINPUT_CAPABILITIES caps)
		{
			caps = new XINPUT_CAPABILITIES();
			return _XInputGetCapabilities(userIndex, XINPUT_FLAG_GAMEPAD, ref caps) == ERROR_SUCCESS;
		}

		/// <summary>
		/// Replace the current set of ViGEm-owned XInput slots.
		/// Use this when slots are detected via device-tree/registry probing.
		/// </summary>
		public static void SetViGEmOwnedSlots(IEnumerable<uint> slots)
		{
			lock (_vigemSlotsLock)
			{
				_vigemOwnedSlots.Clear();
				if (slots == null) return;
				foreach (var s in slots)
				{
					if (s < 4)
						_vigemOwnedSlots.Add(s);
				}
			}
		}

		#endregion

		#region Public API — Vibration

		/// <summary>
		/// Set vibration on physical XInput controller.
		/// Motor values 0-255 (ViGEmBus byte scale).
		/// </summary>
		public static bool SetVibration(uint userIndex,
			byte largeMotor, byte smallMotor)
		{
			var vib = new XINPUT_VIBRATION
			{
				wLeftMotorSpeed = (ushort)(largeMotor * 257),
				wRightMotorSpeed = (ushort)(smallMotor * 257)
			};
			return _XInputSetState(userIndex, ref vib) == ERROR_SUCCESS;
		}

		/// <summary>
		/// Stop all vibration on the given controller.
		/// </summary>
		public static bool StopVibration(uint userIndex)
		{
			return SetVibration(userIndex, 0, 0);
		}

		#endregion

		#region Public API — State reading

		/// <summary>
		/// Check if XInput controller is connected at the given slot.
		/// Uses the Ex variant so the call path is consistent.
		/// </summary>
		public static bool IsConnected(uint userIndex)
		{
			var state = new XINPUT_STATE();
			return _XInputGetStateEx(userIndex, ref state) == ERROR_SUCCESS;
		}

		/// <summary>
		/// Check if a real (non-ViGEm) XInput controller is connected.
		/// Returns false for slots owned by x360ce's own ViGEm bus.
		/// </summary>
		public static bool IsRealControllerConnected(uint userIndex)
		{
			// Skip slots that x360ce owns via ViGEm.
			if (IsViGEmOwnedSlot(userIndex))
				return false;
			return IsConnected(userIndex);
		}

		/// <summary>
		/// Read full controller state using XInputGetStateEx (ordinal #100).
		/// Returns true if the controller is connected and state is valid.
		/// The returned state includes the Guide button (bit 0x0400).
		/// </summary>
		public static bool GetStateEx(uint userIndex, out XINPUT_STATE state)
		{
			state = new XINPUT_STATE();
			return _XInputGetStateEx(userIndex, ref state) == ERROR_SUCCESS;
		}

		/// <summary>
		/// Returns all connected XInput slot indices (0-3).
		/// </summary>
		public static List<uint> GetConnectedSlots()
		{
			var result = new List<uint>();
			for (uint i = 0; i < 4; i++)
			{
				if (IsConnected(i))
					result.Add(i);
			}
			return result;
		}

		/// <summary>
		/// Returns all connected real (non-ViGEm) XInput slot indices (0-3).
		/// Excludes slots owned by x360ce's own ViGEm virtual controllers.
		/// </summary>
		public static List<uint> GetRealConnectedSlots()
		{
			var result = new List<uint>();
			for (uint i = 0; i < 4; i++)
			{
				if (IsRealControllerConnected(i))
					result.Add(i);
			}
			return result;
		}

		#endregion

		#region PIDVID detection (Microsoft DInput-to-XInput shim)

		/// <summary>
		/// Detect whether a DirectInput product GUID is an XInput device
		/// exposed through Microsoft's XInput-to-DirectInput shim.
		/// The last 6 bytes of the GUID spell "PIDVID" in ASCII.
		/// </summary>
		public static bool IsXInputDeviceViaProductGuid(Guid productGuid)
		{
			byte[] b = productGuid.ToByteArray();
			return b[10] == 0x50   // P
				&& b[11] == 0x49   // I
				&& b[12] == 0x44   // D
				&& b[13] == 0x56   // V
				&& b[14] == 0x49   // I
				&& b[15] == 0x44;  // D
		}

		/// <summary>
		/// Returns a bitmask of connected XInput slots (bit 0 = slot 0, etc.).
		/// </summary>
		public static uint GetConnectedSlotMask()
		{
			uint mask = 0;
			for (uint i = 0; i < 4; i++)
			{
				if (IsConnected(i))
					mask |= (1u << (int)i);
			}
			return mask;
		}

		#endregion

		#region State conversion — XINPUT_STATE → JoystickState

		/// <summary>
		/// Convert an XINPUT_STATE (from XInputGetStateEx) into a SharpDX
		/// JoystickState so that the existing DInput→XInput mapping pipeline
		/// (Step3.UpdateXiStates) works unchanged.
		///
		/// Axis mapping matches a standard Xbox controller as seen through DInput:
		///   X  (axis 0) = Left Stick X       Y  (axis 1) = Left Stick Y
		///   Z  (axis 2) = Left Trigger        Rx (axis 3) = Right Stick X
		///   Ry (axis 4) = Right Stick Y       Rz (axis 5) = Right Trigger
		///
		/// Button order matches the Xbox One controller DInput layout:
		///   0=A  1=B  2=X  3=Y  4=LB  5=RB  6=Back  7=Start
		///   8=LeftStick  9=RightStick  10=Guide
		///
		/// D-Pad bits → POV[0] in centidegrees (-1 if released).
		/// </summary>
		public static JoystickState ConvertToJoystickState(XINPUT_STATE xiState)
		{
			var js = new JoystickState();
			var gp = xiState.Gamepad;

			// ────────────────────────────────────────
			// Axes: XInput signed → DInput unsigned
			//   DInput range:  0 – 65535  (center 32767)
			//   XInput sticks: -32768 – 32767  (center 0)
			//   Conversion:    diValue = xiValue + 32768
			//
			//   XInput triggers: 0 – 255
			//   Conversion:      diValue = trigger * 257
			// ────────────────────────────────────────
			js.X = (int)((ushort)(gp.sThumbLX - short.MinValue));       // Left Stick X
			js.Y = (int)((ushort)(-gp.sThumbLY - short.MinValue - 1)); // Left Stick Y (DInput Y+ = down)
			js.Z = (int)(gp.bLeftTrigger * 257);                        // Left Trigger
			js.RotationX = (int)((ushort)(gp.sThumbRX - short.MinValue));       // Right Stick X
			js.RotationY = (int)((ushort)(-gp.sThumbRY - short.MinValue - 1)); // Right Stick Y (DInput Y+ = down)
			js.RotationZ = (int)(gp.bRightTrigger * 257);                       // Right Trigger

			// ────────────────────────────────────────
			// Buttons
			// ────────────────────────────────────────
			ushort b = gp.wButtons;
			js.Buttons[0] = (b & XINPUT_GAMEPAD_A) != 0;
			js.Buttons[1] = (b & XINPUT_GAMEPAD_B) != 0;
			js.Buttons[2] = (b & XINPUT_GAMEPAD_X) != 0;
			js.Buttons[3] = (b & XINPUT_GAMEPAD_Y) != 0;
			js.Buttons[4] = (b & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0;
			js.Buttons[5] = (b & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0;
			js.Buttons[6] = (b & XINPUT_GAMEPAD_BACK) != 0;
			js.Buttons[7] = (b & XINPUT_GAMEPAD_START) != 0;
			js.Buttons[8] = (b & XINPUT_GAMEPAD_LEFT_THUMB) != 0;
			js.Buttons[9] = (b & XINPUT_GAMEPAD_RIGHT_THUMB) != 0;
			js.Buttons[10] = (b & XINPUT_GAMEPAD_GUIDE) != 0;  // Guide — only from GetStateEx!
			js.Buttons[11] = (b & XINPUT_GAMEPAD_SHARE) != 0;  // Share — from HID polling

			// ────────────────────────────────────────
			// D-Pad → POV[0] in centidegrees
			// ────────────────────────────────────────
			bool up = (b & XINPUT_GAMEPAD_DPAD_UP) != 0;
			bool down = (b & XINPUT_GAMEPAD_DPAD_DOWN) != 0;
			bool left = (b & XINPUT_GAMEPAD_DPAD_LEFT) != 0;
			bool right = (b & XINPUT_GAMEPAD_DPAD_RIGHT) != 0;

			int pov = -1; // released
			if (up && right) pov = 4500;
			else if (right && down) pov = 13500;
			else if (down && left) pov = 22500;
			else if (left && up) pov = 31500;
			else if (up) pov = 0;
			else if (right) pov = 9000;
			else if (down) pov = 18000;
			else if (left) pov = 27000;

			js.PointOfViewControllers[0] = pov;

			return js;
		}

		#endregion

		#region Synthetic device metadata

		/// <summary>
		/// Returns DeviceObjectItem array describing the layout of a native
		/// XInput controller. Used so the UI shows correct axis/button labels.
		/// Matches the standard Xbox controller DInput object table.
		/// </summary>
		public static Engine.DeviceObjectItem[] GetNativeXInputDeviceObjects()
		{
			var list = new List<Engine.DeviceObjectItem>();
			// Axes (6 axes matching Xbox controller DInput layout)
			list.Add(new Engine.DeviceObjectItem(4, ObjectGuid.XAxis, ObjectAspect.Position, DeviceObjectTypeFlags.AbsoluteAxis, 0, "X Axis"));
			list.Add(new Engine.DeviceObjectItem(0, ObjectGuid.YAxis, ObjectAspect.Position, DeviceObjectTypeFlags.AbsoluteAxis, 1, "Y Axis"));
			list.Add(new Engine.DeviceObjectItem(16, ObjectGuid.ZAxis, ObjectAspect.Position, DeviceObjectTypeFlags.AbsoluteAxis, 2, "Z Axis"));
			list.Add(new Engine.DeviceObjectItem(12, ObjectGuid.RxAxis, ObjectAspect.Position, DeviceObjectTypeFlags.AbsoluteAxis, 3, "X Rotation"));
			list.Add(new Engine.DeviceObjectItem(8, ObjectGuid.RyAxis, ObjectAspect.Position, DeviceObjectTypeFlags.AbsoluteAxis, 4, "Y Rotation"));
			list.Add(new Engine.DeviceObjectItem(20, ObjectGuid.RzAxis, ObjectAspect.Position, DeviceObjectTypeFlags.AbsoluteAxis, 5, "Z Rotation"));
			// POV
			list.Add(new Engine.DeviceObjectItem(24, ObjectGuid.PovController, 0, DeviceObjectTypeFlags.PointOfViewController, 0, "Hat Switch"));
			// Buttons (11: A B X Y LB RB Back Start LStick RStick Guide)
			list.Add(new Engine.DeviceObjectItem(56, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 0, "Button 0"));
			list.Add(new Engine.DeviceObjectItem(57, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 1, "Button 1"));
			list.Add(new Engine.DeviceObjectItem(58, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 2, "Button 2"));
			list.Add(new Engine.DeviceObjectItem(59, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 3, "Button 3"));
			list.Add(new Engine.DeviceObjectItem(60, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 4, "Button 4"));
			list.Add(new Engine.DeviceObjectItem(61, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 5, "Button 5"));
			list.Add(new Engine.DeviceObjectItem(62, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 6, "Button 6"));
			list.Add(new Engine.DeviceObjectItem(63, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 7, "Button 7"));
			list.Add(new Engine.DeviceObjectItem(64, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 8, "Button 8"));
			list.Add(new Engine.DeviceObjectItem(65, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 9, "Button 9"));
			list.Add(new Engine.DeviceObjectItem(66, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 10, "System Main Menu"));
			list.Add(new Engine.DeviceObjectItem(67, ObjectGuid.Button, 0, DeviceObjectTypeFlags.PushButton, 11, "Share"));
			// Collections (informational only)
			list.Add(new Engine.DeviceObjectItem(0, ObjectGuid.Unknown, 0, DeviceObjectTypeFlags.Collection | DeviceObjectTypeFlags.NoData, 0, "Collection 0 - Game Pad"));
			// Update DIndexes
			for (int i = 0; i < list.Count; i++)
				list[i].DiIndex = list[i].Instance;
			return list.ToArray();
		}

		/// <summary>
		/// Create a new synthetic UserDevice representing a native XInput
		/// controller at the given slot. Used by Step1 device enumeration.
		/// The 1-based slot number is included in the device name for display.
		/// </summary>
		public static Engine.Data.UserDevice NewSyntheticUserDevice(uint slot)
		{
			var ud = new Engine.Data.UserDevice();
			var displayNumber = slot + 1;
			ud.InstanceGuid = GetSyntheticInstanceGuid(slot);
			ud.ProductGuid = NativeXInputProductGuid;
			ud.InstanceName = string.Format("Native XInput Controller {0}", displayNumber);
			ud.ProductName = string.Format("Native XInput Controller {0}", displayNumber);
			ud.DevManufacturer = "Microsoft";
			ud.HidDescription = string.Format("Native XInput Controller {0}", displayNumber);
			// Capabilities matching Xbox 360 controller
			ud.CapType = (int)SharpDX.DirectInput.DeviceType.Gamepad;
			ud.CapSubtype = 258;
			ud.CapFlags = 5;
			ud.CapAxeCount = 6;
			ud.CapButtonCount = 12; // A B X Y LB RB Back Start LS RS Guide Share
			ud.CapPovCount = 1;
			ud.HidClassGuid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
			ud.IsEnabled = true;
			return ud;
		}

		#endregion

		#region Logical (display) index -> real XInput user index mapping

		private static readonly object _logicalMapLock = new object();

		// logical index 0..3 => real xinput slot 0..3 (or null if not currently assigned)
		private static readonly uint?[] _logicalToRealSlot = new uint?[4];

		/// <summary>
		/// Update mapping so logical controller 0..N maps to the connected real slots list.
		/// Example: real slots [2,3] => logical0->2, logical1->3.
		/// </summary>
		public static void UpdateLogicalSlotMap(IList<uint> connectedRealSlots)
		{
			lock (_logicalMapLock)
			{
				for (int i = 0; i < 4; i++)
					_logicalToRealSlot[i] = null;

				if (connectedRealSlots == null)
					return;

				for (int i = 0; i < connectedRealSlots.Count && i < 4; i++)
				{
					var slot = connectedRealSlots[i];
					if (slot < 4)
						_logicalToRealSlot[i] = slot;
				}
			}
		}

		/// <summary>
		/// Try to map a logical index (0..3) to current real XInput slot (0..3).
		/// </summary>
		public static bool TryGetRealSlot(uint logicalIndex, out uint realSlot)
		{
			realSlot = 0;
			if (logicalIndex >= 4)
				return false;

			lock (_logicalMapLock)
			{
				var v = _logicalToRealSlot[logicalIndex];
				if (!v.HasValue)
					return false;
				realSlot = v.Value;
				return true;
			}
		}

		/// <summary>
		/// Convenience: map our synthetic InstanceGuid (which encodes logical index) to real slot.
		/// </summary>
		public static bool TryGetRealSlotFromSyntheticGuid(Guid instanceGuid, out uint realSlot)
		{
			realSlot = 0;
			var logical = GetSlotFromSyntheticGuid(instanceGuid); // NOTE: now treated as logical index
			return logical.HasValue && TryGetRealSlot(logical.Value, out realSlot);
		}

		/// <summary>
		/// Rebuild logical slot map from current system state (excluding registered ViGEm slots).
		/// </summary>
		public static List<uint> RefreshLogicalSlotMapFromSystem()
		{
			var realSlots = GetRealConnectedSlots(); // uses IsViGEmOwnedSlot() to exclude virtuals
			realSlots.Sort();
			UpdateLogicalSlotMap(realSlots);
			return realSlots;
		}

		#endregion

		#region Share Button HID Polling

		private static readonly bool[] _shareButtonState = new bool[4];
		private static readonly object _shareStateLock = new object();
		private static volatile bool _sharePollingRunning;
		private static readonly HashSet<string> _claimedHidPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> _claimedControllerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly object _hidClaimLock = new object();

		// Xbox controller PIDs known to have Share button via HID.
		// 0x02FD = Xbox One S (Bluetooth)
		// 0x02FF = Xbox One S rev2 (Bluetooth)
		// 0x0B12 = Xbox Series X|S (USB)
		// 0x0B13 = Xbox Series X|S (Bluetooth / Xbox Wireless)
		private static readonly ushort[] ShareButtonPids = { 0x02FD, 0x0B12, 0x0B13, 0x02FF };

		/// <summary>
		/// Get the current Share button state for a given REAL XInput slot (0-3).
		/// </summary>
		public static bool GetShareButtonState(uint realSlot)
		{
			if (realSlot >= 4) return false;
			lock (_shareStateLock)
				return _shareButtonState[realSlot];
		}

		/// <summary>
		/// Start background HID polling threads for Share button detection.
		/// Safe to call multiple times — only starts once.
		/// </summary>
		public static void StartShareButtonPolling()
		{
			if (_sharePollingRunning) return;
			_sharePollingRunning = true;

			for (uint i = 0; i < 4; i++)
			{
				var slot = i;
				var t = new Thread(() => PollHidForShareButton(slot))
				{
					IsBackground = true,
					Name = "ShareBtn_Slot" + slot,
					Priority = ThreadPriority.BelowNormal
				};
				t.Start();
			}
		}

		/// <summary>
		/// Signal Share button polling threads to stop.
		/// </summary>
		public static void StopShareButtonPolling()
		{
			_sharePollingRunning = false;
		}

		#region HID / SetupAPI P/Invoke

		private static readonly IntPtr INVALID_HANDLE_PTR = new IntPtr(-1);
		private const uint HID_GENERIC_READ = 0x80000000;
		private const uint HID_FILE_SHARE_RW = 0x00000003;
		private const uint HID_OPEN_EXISTING = 3;
		private const uint DIGCF_PRESENT = 0x00000002;
		private const uint DIGCF_DEVICEINTERFACE = 0x00000010;

		[StructLayout(LayoutKind.Sequential)]
		private struct HIDD_ATTRIBUTES
		{
			public uint Size;
			public ushort VendorID;
			public ushort ProductID;
			public ushort VersionNumber;
		}

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

		[StructLayout(LayoutKind.Sequential)]
		private struct SP_DEVICE_INTERFACE_DATA
		{
			public uint cbSize;
			public Guid InterfaceClassGuid;
			public uint Flags;
			public IntPtr Reserved;
		}

		[DllImport("hid.dll")]
		private static extern void HidD_GetHidGuid(out Guid hidGuid);

		[DllImport("hid.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool HidD_GetAttributes(IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

		[DllImport("hid.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool HidD_GetPreparsedData(IntPtr HidDeviceObject, out IntPtr PreparsedData);

		[DllImport("hid.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

		[DllImport("hid.dll")]
		private static extern int HidP_GetCaps(IntPtr PreparsedData, out HIDP_CAPS Capabilities);

		[DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, string Enumerator, IntPtr hwndParent, uint Flags);

		[DllImport("setupapi.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData,
			ref Guid InterfaceClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

		[DllImport("setupapi.dll", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet,
			ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData,
			uint DeviceInterfaceDetailDataSize, out uint RequiredSize, IntPtr DeviceInfoData);

		[DllImport("setupapi.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess,
			uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
			uint dwFlagsAndAttributes, IntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer,
			uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CloseHandle(IntPtr hObject);

		#endregion

		#region Device Tree Helpers (cfgmgr32)

		private const int CM_CR_SUCCESS = 0;

		[DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
		private static extern int CM_Locate_DevNodeW(
			out uint pdnDevInst, string pDeviceID, int ulFlags);

		[DllImport("cfgmgr32.dll")]
		private static extern int CM_Get_Parent(
			out uint pdnDevInst, uint dnDevInst, int ulFlags);

		[DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
		private static extern int CM_Get_Device_IDW(
			uint dnDevInst, System.Text.StringBuilder Buffer, int BufferLen, int ulFlags);

		/// <summary>
		/// Convert a HID device interface path to a PnP device instance ID.
		/// Example input:  \\?\HID#VID_045E&amp;PID_0B13&amp;IG_02#7&amp;abc#0000#{guid}
		/// Example output: HID\VID_045E&amp;PID_0B13&amp;IG_02\7&amp;abc\0000
		/// </summary>
		private static string DevicePathToInstanceId(string devicePath)
		{
			if (string.IsNullOrEmpty(devicePath))
				return null;

			var s = devicePath;

			// Remove \\?\ prefix
			if (s.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
				s = s.Substring(4);

			// Remove #{GUID} suffix
			var guidIdx = s.IndexOf("#{", StringComparison.OrdinalIgnoreCase);
			if (guidIdx >= 0)
				s = s.Substring(0, guidIdx);

			// Replace # with backslash
			s = s.Replace('#', '\\');

			return s;
		}

		/// <summary>
		/// Get a stable identity string for the physical controller that owns
		/// the given HID device interface path.
		///
		/// Walks up the device tree to find the nearest ancestor that has a
		/// vendor ID (VID_ or VID&amp;) but is NOT a USB multi-interface function
		/// child (MI_).  For USB controllers this yields the USB composite
		/// device; for Bluetooth it yields the BTHENUM service device.
		///
		/// Falls back to the immediate parent if no matching ancestor is found
		/// (e.g. Xbox Wireless Adapter with a non-standard tree layout).
		///
		/// Returns null on failure.
		/// </summary>
		private static string GetControllerIdentity(string devicePath)
		{
			try
			{
				var instanceId = DevicePathToInstanceId(devicePath);
				if (string.IsNullOrEmpty(instanceId))
					return null;

				uint devInst;
				if (CM_Locate_DevNodeW(out devInst, instanceId, 0) != CM_CR_SUCCESS)
					return null;

				// Walk up looking for the physical device node.
				// USB composite:  USB\VID_045E&PID_0B12\serial   (has VID_, no MI_)
				// Bluetooth:      BTHENUM\{uuid}_VID&0002045e_PID&0b13\...  (has VID&, no MI_)
				uint current = devInst;
				for (int depth = 0; depth < 8; depth++)
				{
					uint parentInst;
					if (CM_Get_Parent(out parentInst, current, 0) != CM_CR_SUCCESS)
						break;

					var sb = new System.Text.StringBuilder(1024);
					if (CM_Get_Device_IDW(parentInst, sb, sb.Capacity, 0) != CM_CR_SUCCESS)
						break;

					var parentId = sb.ToString();

					bool hasVendor =
						parentId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase) >= 0 ||
						parentId.IndexOf("VID&", StringComparison.OrdinalIgnoreCase) >= 0;

					bool isUsbFunction =
						parentId.IndexOf("MI_", StringComparison.OrdinalIgnoreCase) >= 0;

					if (hasVendor && !isUsbFunction)
						return parentId;

					current = parentInst;
				}

				// Fallback: immediate parent (unique per device even when
				// the tree layout doesn't match the patterns above).
				uint origInst;
				if (CM_Locate_DevNodeW(out origInst, instanceId, 0) == CM_CR_SUCCESS)
				{
					uint fallbackParent;
					if (CM_Get_Parent(out fallbackParent, origInst, 0) == CM_CR_SUCCESS)
					{
						var sb = new System.Text.StringBuilder(1024);
						if (CM_Get_Device_IDW(fallbackParent, sb, sb.Capacity, 0) == CM_CR_SUCCESS)
							return sb.ToString();
					}
				}
			}
			catch { }

			return null;
		}

		#endregion

		/// <summary>
		/// Background thread: poll one HID device for Share button state.
		/// Each thread (one per XInput slot 0-3) claims exactly one HID device
		/// from a unique physical controller.
		///
		/// Staggered startup ensures deterministic enumeration order.
		/// Controller identity tracking prevents two threads from claiming
		/// different HID interfaces that belong to the same physical controller
		/// (which would leave the other controller's Share button unread).
		///
		/// Only polls when the slot is connected and NOT ViGEm-owned.
		/// </summary>
		private static void PollHidForShareButton(uint controllerIndex)
		{
			// Stagger startup so controllers claim HID devices in deterministic order.
			Thread.Sleep((int)(controllerIndex * 500));

			IntPtr hDevice = IntPtr.Zero;
			string myClaimedPath = null;
			string myClaimedControllerId = null;

			System.Diagnostics.Debug.WriteLine(
				string.Format("[ShareBtn] Slot {0}: Polling thread started.", controllerIndex));

			while (_sharePollingRunning)
			{
				Thread.Sleep(1); // ~1000 Hz

				if (hDevice == IntPtr.Zero)
				{
					// Only search if this real XInput slot is connected and not ViGEm.
					if (!IsConnected(controllerIndex) || IsViGEmOwnedSlot(controllerIndex))
						continue;

					string controllerId;
					hDevice = FindAndClaimShareHidDevice(out myClaimedPath, out controllerId);
					myClaimedControllerId = controllerId;

					if (hDevice == IntPtr.Zero)
					{
						// No device found — back off before retrying.
						Thread.Sleep(500);
					}
					else
					{
						System.Diagnostics.Debug.WriteLine(
							string.Format("[ShareBtn] Slot {0}: Claimed HID device. Path={1}, Controller={2}",
								controllerIndex,
								myClaimedPath ?? "(null)",
								myClaimedControllerId ?? "(unknown)"));
					}
					continue;
				}

				// Read HID report.
				var buf = new byte[32];
				uint bytesRead;
				if (ReadFile(hDevice, buf, (uint)buf.Length, out bytesRead, IntPtr.Zero))
				{
					bool pressed = (buf[0] == 0x00 && (buf[12] & 0x08) != 0);
					lock (_shareStateLock)
						_shareButtonState[controllerIndex] = pressed;
				}
				else
				{
					// Device lost — close, unclaim, backoff.
					CloseHandle(hDevice);
					hDevice = IntPtr.Zero;

					lock (_shareStateLock)
						_shareButtonState[controllerIndex] = false;

					if (myClaimedPath != null || myClaimedControllerId != null)
					{
						lock (_hidClaimLock)
						{
							if (myClaimedPath != null)
								_claimedHidPaths.Remove(myClaimedPath);
							if (myClaimedControllerId != null)
								_claimedControllerIds.Remove(myClaimedControllerId);
						}

						System.Diagnostics.Debug.WriteLine(
							string.Format("[ShareBtn] Slot {0}: HID device lost, will re-enumerate. Path={1}, Controller={2}",
								controllerIndex,
								myClaimedPath ?? "(null)",
								myClaimedControllerId ?? "(unknown)"));

						myClaimedPath = null;
						myClaimedControllerId = null;
					}

					// Staggered backoff by controller index.
					Thread.Sleep(200 + (int)(controllerIndex * 200));
				}
			}

			// Cleanup on shutdown.
			if (hDevice != IntPtr.Zero)
			{
				CloseHandle(hDevice);
				lock (_hidClaimLock)
				{
					if (myClaimedPath != null)
						_claimedHidPaths.Remove(myClaimedPath);
					if (myClaimedControllerId != null)
						_claimedControllerIds.Remove(myClaimedControllerId);
				}
			}

			lock (_shareStateLock)
				_shareButtonState[controllerIndex] = false;

			System.Diagnostics.Debug.WriteLine(
				string.Format("[ShareBtn] Slot {0}: Polling thread stopped.", controllerIndex));
		}

		/// <summary>
		/// Enumerate HID devices, find an unclaimed Xbox controller with the
		/// 16-byte Share button report, claim it, and return the handle.
		///
		/// Uses controller identity tracking (via device-tree parent walk) to
		/// ensure each polling thread claims a device from a DIFFERENT physical
		/// controller.  Without this, two threads could each claim a different
		/// HID interface from the same physical controller, leaving the second
		/// controller's Share button completely unread.
		/// </summary>
		private static IntPtr FindAndClaimShareHidDevice(
			out string claimedPath, out string claimedControllerId)
		{
			claimedPath = null;
			claimedControllerId = null;

			Guid hidGuid;
			HidD_GetHidGuid(out hidGuid);

			IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero,
				DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
			if (hDevInfo == IntPtr.Zero || hDevInfo == INVALID_HANDLE_PTR)
				return IntPtr.Zero;

			try
			{
				var ifData = new SP_DEVICE_INTERFACE_DATA();
				ifData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

				for (uint idx = 0;
					SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, idx, ref ifData);
					idx++)
				{
					uint reqSize;
					SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData,
						IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
					if (reqSize == 0)
						continue;

					IntPtr detailBuf = Marshal.AllocHGlobal((int)reqSize);
					try
					{
						// cbSize of SP_DEVICE_INTERFACE_DETAIL_DATA
						int cbSize = IntPtr.Size == 8 ? 8 : (4 + Marshal.SystemDefaultCharSize);
						Marshal.WriteInt32(detailBuf, cbSize);

						if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData,
							detailBuf, reqSize, out _, IntPtr.Zero))
							continue;

						string devicePath = Marshal.PtrToStringUni(IntPtr.Add(detailBuf, 4));
						if (string.IsNullOrEmpty(devicePath))
							continue;

						// ── Pre-open checks (no file handle needed yet) ──

						// Already claimed path?
						lock (_hidClaimLock)
						{
							if (_claimedHidPaths.Contains(devicePath))
								continue;
						}

						// Resolve physical controller identity from device tree.
						string controllerId = GetControllerIdentity(devicePath);

						// If this physical controller is already claimed by another
						// thread, skip ALL its remaining HID interfaces.
						if (controllerId != null)
						{
							lock (_hidClaimLock)
							{
								if (_claimedControllerIds.Contains(controllerId))
								{
									System.Diagnostics.Debug.WriteLine(
										string.Format("[ShareBtn] Skipping (controller already claimed): Path={0}, Controller={1}",
											devicePath, controllerId));
									continue;
								}
							}
						}

						// ── Open and validate ──

						IntPtr hDev = CreateFileW(devicePath, HID_GENERIC_READ,
							HID_FILE_SHARE_RW, IntPtr.Zero, HID_OPEN_EXISTING,
							0, IntPtr.Zero);

						if (hDev == IntPtr.Zero || hDev == INVALID_HANDLE_PTR)
							continue;

						bool matched = false;
						try
						{
							// Check VID / PID.
							var attrs = new HIDD_ATTRIBUTES();
							attrs.Size = (uint)Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));

							if (!HidD_GetAttributes(hDev, ref attrs) ||
								attrs.VendorID != 0x045E ||
								Array.IndexOf(ShareButtonPids, attrs.ProductID) < 0)
								continue;

							// Check HID collection — must be the 16-byte report.
							IntPtr preparsed;
							if (!HidD_GetPreparsedData(hDev, out preparsed))
								continue;

							try
							{
								HIDP_CAPS caps;
								bool ok = (HidP_GetCaps(preparsed, out caps) == 0x00110000) // HIDP_STATUS_SUCCESS
									   && (caps.InputReportByteLength == 16);

								if (!ok)
									continue;
							}
							finally
							{
								HidD_FreePreparsedData(preparsed);
							}

							// ── Claim path + controller identity (double-check under lock) ──
							lock (_hidClaimLock)
							{
								// Another thread may have claimed between our earlier
								// check and now — verify again inside the lock.
								if (_claimedHidPaths.Contains(devicePath))
									continue;
								if (controllerId != null && _claimedControllerIds.Contains(controllerId))
									continue;

								_claimedHidPaths.Add(devicePath);
								if (controllerId != null)
									_claimedControllerIds.Add(controllerId);
							}

							claimedPath = devicePath;
							claimedControllerId = controllerId;
							matched = true;

							System.Diagnostics.Debug.WriteLine(
								string.Format("[ShareBtn] Claimed Share HID: PID=0x{0:X4}, ReportLen=16, Path={1}, Controller={2}",
									attrs.ProductID, devicePath, controllerId ?? "(unknown)"));

							return hDev;
						}
						finally
						{
							if (!matched)
								CloseHandle(hDev);
						}
					}
					finally
					{
						Marshal.FreeHGlobal(detailBuf);
					}
				}
			}
			finally
			{
				SetupDiDestroyDeviceInfoList(hDevInfo);
			}

			return IntPtr.Zero;
		}

		#endregion
	}
}
