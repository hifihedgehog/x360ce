using JocysCom.ClassLibrary.IO;
using Microsoft.Win32;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using x360ce.Engine.Data;

namespace x360ce.App.DInput
{
	public partial class DInputHelper
	{

		#region Device Detector

		// True, update device list as soon as possible.
		public bool UpdateDevicesEnabled = true;

		#endregion

		object UpdateDevicesLock = new object();
		public int RefreshDevicesCount;

		// ── ViGEm slot tracking state (shared between Step1 and Step2 via partial class) ──
		private static int _lastKnownVigemCount = -1; // -1 = not yet initialized
		private static uint _lastKnownSlotMask = 0;

		void UpdateDiDevices(DirectInput manager)
		{
			if (!UpdateDevicesPending)
				return;
			UpdateDevicesPending = false;
			// Make sure that interface handle is created, before starting device updates.
			UserDevice[] deleteDevices;
			// Add connected devices.
			var insertDevices = new List<UserDevice>();
			// List of connected devices (can be a very long operation).
			var devices = new List<DeviceInstance>();
			// Controllers.
			var controllerInstances = manager.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices).ToList();
			foreach (var item in controllerInstances)
				devices.Add(item);
			// Pointers.
			var pointerInstances = manager.GetDevices(DeviceClass.Pointer, DeviceEnumerationFlags.AllDevices).ToList();
			foreach (var item in pointerInstances)
				devices.Add(item);
			// Keyboards.
			var keyboardInstances = manager.GetDevices(DeviceClass.Keyboard, DeviceEnumerationFlags.AllDevices).ToList();
			foreach (var item in keyboardInstances)
				devices.Add(item);
			if (Program.IsClosing)
				return;
			// We need device tree info for DInput device filtering.
			DeviceInfo[] devInfos = DeviceDetector.GetDevices();
			DeviceInfo[] intInfos = DeviceDetector.GetInterfaces();
			// ══════════════════════════════════════════════════════════
			// Detect which XInput slots are ViGEm-owned.
			// Uses count-based detection + delta tracking (NOT IG_xx).
			// IG_xx in HID paths is the HID collection index, NOT the
			// XInput user index — using it caused the wrong slot to be
			// marked as ViGEm, breaking physical controller input.
			// ══════════════════════════════════════════════════════════
			UpdateViGEmSlotTracking();
			// List of connected devices.
			var deviceInstanceGuid = devices.Select(x => x.InstanceGuid).ToList();
			// ══════════════════════════════════════════════════════════
			// Native XInput enumeration — probe slots 0-3 directly.
			// Assigns contiguous virtual numbers (0, 1, 2, …) to real
			// non-ViGEm slots so the UI shows "Controller 1, 2, …"
			// regardless of which hardware slot they occupy.
			// ══════════════════════════════════════════════════════════
			// Build the list of *real* connected XInput slots excluding ViGEm.
			var connectedRealSlots = XInputInterop.GetRealConnectedSlots();
			connectedRealSlots.Sort();

			// Update logical->real slot map so Step2 can translate correctly.
			XInputInterop.UpdateLogicalSlotMap(connectedRealSlots);

			// Start HID polling for Share button (runs once, threads are background).
			XInputInterop.StartShareButtonPolling();

			// Create synthetic devices using LOGICAL indices (0..count-1)
			var nativeXInputGuids = new List<Guid>();
			for (uint logical = 0; logical < (uint)connectedRealSlots.Count && logical < 4; logical++)
			{
				var syntheticGuid = XInputInterop.GetSyntheticInstanceGuid(logical);
				nativeXInputGuids.Add(syntheticGuid);
				deviceInstanceGuid.Add(syntheticGuid);
			}
			// ══════════════════════════════════════════════════════════
			// List of current devices.
			var uds = SettingsManager.UserDevices.ItemsToArraySyncronized();
			var currentInstanceGuids = uds.Select(x => x.InstanceGuid).ToArray();
			deleteDevices = uds.Where(x => !deviceInstanceGuid.Contains(x.InstanceGuid)).ToArray();
			var addedDevices = devices.Where(x => !currentInstanceGuids.Contains(x.InstanceGuid)).ToArray();
			var updatedDevices = devices.Where(x => currentInstanceGuids.Contains(x.InstanceGuid)).ToArray();
			for (int i = 0; i < addedDevices.Length; i++)
			{
				var device = addedDevices[i];
				var ud = new UserDevice();
				DeviceInfo hid;
				RefreshDevice(manager, ud, device, devInfos, intInfos, out hid);
				var isVirtual = false;
				if (hid != null)
				{
					DeviceInfo p = hid;
					do
					{
						p = devInfos.FirstOrDefault(x => x.DeviceId == p.ParentDeviceId);
						if (p != null && !string.IsNullOrEmpty(p.HardwareIds) && VirtualDriverInstaller.ViGEmBusHardwareIds.Any(x => p.HardwareIds.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
						{
							isVirtual = true;
							break;
						}
					} while (p != null);
				}
				if (!isVirtual)
					insertDevices.Add(ud);
			}
			for (int i = 0; i < updatedDevices.Length; i++)
			{
				var device = updatedDevices[i];
				var ud = uds.First(x => x.InstanceGuid.Equals(device.InstanceGuid));
				DeviceInfo hid;
				RefreshDevice(manager, ud, device, devInfos, intInfos, out hid);
			}
			if (Program.IsClosing)
				return;
			// Remove disconnected devices.
			for (int i = 0; i < deleteDevices.Length; i++)
			{
				lock (SettingsManager.UserDevices.SyncRoot)
					deleteDevices[i].IsOnline = false;
			}
			for (int i = 0; i < insertDevices.Count; i++)
			{
				var ud = insertDevices[i];
				lock (SettingsManager.UserDevices.SyncRoot)
					SettingsManager.UserDevices.Items.Add(ud);
			}
			// ══════════════════════════════════════════════════════════
			// Native XInput: Insert or update synthetic UserDevices.
			// ══════════════════════════════════════════════════════════
			var xiSnapshot = SettingsManager.UserDevices.ItemsToArraySyncronized();
			foreach (var synGuid in nativeXInputGuids)
			{
				var existing = xiSnapshot.FirstOrDefault(x => x.InstanceGuid == synGuid);
				if (existing == null)
				{
					var slot = XInputInterop.GetSlotFromSyntheticGuid(synGuid);
					if (slot.HasValue)
					{
						var synDevice = XInputInterop.NewSyntheticUserDevice(slot.Value);
						synDevice.IsOnline = true;
						lock (SettingsManager.UserDevices.SyncRoot)
							SettingsManager.UserDevices.Items.Add(synDevice);
					}
				}
				else
				{
					var slot = XInputInterop.GetSlotFromSyntheticGuid(synGuid);
					if (slot.HasValue)
					{
						var displayNumber = slot.Value + 1;
						var expectedName = string.Format("Native XInput Controller {0}", displayNumber);
						lock (SettingsManager.UserDevices.SyncRoot)
						{
							if (existing.InstanceName != expectedName)
								existing.InstanceName = expectedName;
							if (existing.ProductName != expectedName)
								existing.ProductName = expectedName;
							if (existing.HidDescription != expectedName)
								existing.HidDescription = expectedName;
						}
					}
					if (!existing.IsOnline)
						lock (SettingsManager.UserDevices.SyncRoot)
							existing.IsOnline = true;
				}
			}
			// Mark disconnected native XInput devices as offline.
			var xiSnapshotOffline = SettingsManager.UserDevices.ItemsToArraySyncronized();
			for (uint slot = 0; slot < 4; slot++)
			{
				var synGuid = XInputInterop.GetSyntheticInstanceGuid(slot);
				if (!nativeXInputGuids.Contains(synGuid))
				{
					var existing = xiSnapshotOffline.FirstOrDefault(x => x.InstanceGuid == synGuid);
					if (existing != null && existing.IsOnline)
						lock (SettingsManager.UserDevices.SyncRoot)
							existing.IsOnline = false;
				}
			}
			// ══════════════════════════════════════════════════════════
			// Enable Test instances.
			TestDeviceHelper.EnableTestInstances();
			RefreshDevicesCount++;
			var ev = DevicesUpdated;
			if (ev != null)
				ev(this, new DInputEventArgs());
		}

		/// <summary>
		/// Refresh device.
		/// </summary>
		void RefreshDevice(DirectInput manager, UserDevice ud, DeviceInstance device, DeviceInfo[] allDevices, DeviceInfo[] allInterfaces, out DeviceInfo hid)
		{
			hid = null;
			if (Program.IsClosing)
				return;
			if (ud.Device == null)
			{
				try
				{
					lock (SettingsManager.UserDevices.SyncRoot)
					{
						var joystick = new Joystick(manager, device.InstanceGuid);
						ud.Device = joystick;
						ud.IsExclusiveMode = null;
						ud.LoadCapabilities(joystick.Capabilities);
					}
				}
				catch (Exception) { }
			}
			lock (SettingsManager.UserDevices.SyncRoot)
			{
				ud.LoadInstance(device);
			}
			if (!ud.IsOnline)
				lock (SettingsManager.UserDevices.SyncRoot)
					ud.IsOnline = true;
			var dev = allDevices.FirstOrDefault(x => x.DeviceId == ud.HidDeviceId);
			lock (SettingsManager.UserDevices.SyncRoot)
			{
				ud.LoadDevDeviceInfo(dev);
				if (dev != null)
					ud.ConnectionClass = DeviceDetector.GetConnectionDevice(dev, allDevices)?.ClassGuid ?? Guid.Empty;
			}
			if (device.IsHumanInterfaceDevice && ud.Device != null)
			{
				var interfacePath = ud.Device.Properties.InterfacePath;
				hid = allInterfaces.FirstOrDefault(x => x.DevicePath == interfacePath);
				lock (SettingsManager.UserDevices.SyncRoot)
				{
					ud.LoadHidDeviceInfo(hid);
					if (hid != null)
						ud.ConnectionClass = DeviceDetector.GetConnectionDevice(hid, allDevices)?.ClassGuid ?? Guid.Empty;
					ud.DevManufacturer = ud.HidManufacturer;
					ud.DevDescription = ud.HidDescription;
					ud.DevVendorId = ud.HidVendorId;
					ud.DevProductId = ud.HidProductId;
					ud.DevRevision = ud.HidRevision;
				}
			}
		}

		// ═══════════════════════════════════════════════════════════
		// ViGEm slot tracking: count-based + delta approach.
		//
		// WHY NOT IG_xx?
		// The IG_00/01/02/03 suffix in HID device instance IDs is
		// the HID *collection* index (gamepad, headset, etc.), NOT
		// the XInput user index. Every Xbox controller has IG_00
		// regardless of which slot it occupies. Using it caused the
		// wrong slot to be marked as ViGEm → physical controller
		// input was lost.
		//
		// HOW THIS WORKS:
		// 1. Count ViGEm gamepad devices via RawInput + parent walk
		//    (reliable — doesn't depend on slot mapping).
		// 2. On first detection: physical controllers connected
		//    before the app start occupy the LOWEST XInput slots;
		//    ViGEm controllers created during app init get the
		//    HIGHEST available slots. Mark the highest N as ViGEm.
		// 3. On runtime changes (ViGEm count increases/decreases):
		//    use slot-mask delta to identify exactly which slot
		//    appeared or disappeared.
		// ═══════════════════════════════════════════════════════════

		private static void UpdateViGEmSlotTracking()
		{
			int vigemCount = CountViGEmXInputDevices();
			uint currentMask = XInputInterop.GetConnectedSlotMask();

			if (_lastKnownVigemCount < 0)
			{
				// ── First run: initial detection ──
				_lastKnownVigemCount = vigemCount;
				_lastKnownSlotMask = currentMask;

				if (vigemCount <= 0)
				{
					XInputInterop.SetViGEmOwnedSlots(null);
					return;
				}

				// Physical controllers that were connected before the app
				// started occupy the LOWEST XInput slots (assigned by Windows
				// at boot/plug-in time). ViGEm controllers created during
				// x360ce initialization get the next available (HIGHEST) slots.
				var allSlots = new List<uint>();
				for (uint i = 0; i < 4; i++)
					if ((currentMask & (1u << (int)i)) != 0)
						allSlots.Add(i);

				var vigemSlots = new HashSet<uint>();
				for (int vi = 0; vi < vigemCount && vi < allSlots.Count; vi++)
					vigemSlots.Add(allSlots[allSlots.Count - 1 - vi]);

				XInputInterop.SetViGEmOwnedSlots(vigemSlots);
				return;
			}

			// ── Runtime: detect ViGEm count changes via delta ──
			if (vigemCount > _lastKnownVigemCount)
			{
				// New ViGEm device(s) created.
				// Any XInput slot that appeared since last check is ViGEm.
				uint newBits = currentMask & ~_lastKnownSlotMask;
				for (uint i = 0; i < 4; i++)
					if ((newBits & (1u << (int)i)) != 0)
						XInputInterop.RegisterViGEmSlot(i);
			}
			else if (vigemCount < _lastKnownVigemCount)
			{
				// ViGEm device(s) removed.
				// Slots that disappeared and were ViGEm-owned → unregister.
				uint removedBits = _lastKnownSlotMask & ~currentMask;
				for (uint i = 0; i < 4; i++)
					if ((removedBits & (1u << (int)i)) != 0 && XInputInterop.IsViGEmOwnedSlot(i))
						XInputInterop.UnregisterViGEmSlot(i);
			}

			_lastKnownVigemCount = vigemCount;
			_lastKnownSlotMask = currentMask;
		}

		// ═══════════════════════════════════════════════════════════
		// Count ViGEm Xbox controller devices via RawInput + cfgmgr32.
		// Counts only IG_00 (primary gamepad collection) to get
		// exactly one count per physical/virtual controller.
		// ═══════════════════════════════════════════════════════════

		[StructLayout(LayoutKind.Sequential)]
		private struct RAWINPUTDEVICELIST
		{
			public IntPtr hDevice;
			public uint dwType;
		}

		private const uint RIM_TYPEHID = 2;
		private const uint RIDI_DEVICENAME = 0x20000007;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetRawInputDeviceList(
			[In, Out] RAWINPUTDEVICELIST[] pRawInputDeviceList,
			ref uint puiNumDevices,
			uint cbSize);

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern uint GetRawInputDeviceInfo(
			IntPtr hDevice,
			uint uiCommand,
			StringBuilder pData,
			ref uint pcbSize);

		private static int CountViGEmXInputDevices()
		{
			int count = 0;

			uint num = 0;
			uint cbSize = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));

			if (GetRawInputDeviceList(null, ref num, cbSize) == 0xFFFFFFFF)
				return 0;

			var list = new RAWINPUTDEVICELIST[num];
			if (GetRawInputDeviceList(list, ref num, cbSize) == 0xFFFFFFFF)
				return 0;

			for (int i = 0; i < list.Length; i++)
			{
				if (list[i].dwType != RIM_TYPEHID)
					continue;

				uint nameSize = 0;
				GetRawInputDeviceInfo(list[i].hDevice, RIDI_DEVICENAME, null, ref nameSize);
				if (nameSize == 0)
					continue;

				var sb = new StringBuilder((int)nameSize);
				if (GetRawInputDeviceInfo(list[i].hDevice, RIDI_DEVICENAME, sb, ref nameSize) == 0xFFFFFFFF)
					continue;

				var rawName = sb.ToString();

				// Only count IG_00 (primary gamepad collection) — one per controller.
				if (rawName.IndexOf("IG_00", StringComparison.OrdinalIgnoreCase) < 0)
					continue;

				var pnpId = RawInputNameToPnPInstanceId(rawName);
				if (string.IsNullOrEmpty(pnpId))
					continue;

				if (PnP.IsUnderViGEmBus_ByServiceOrName(pnpId))
					count++;
			}

			return count;
		}

		private static string RawInputNameToPnPInstanceId(string rawName)
		{
			if (string.IsNullOrEmpty(rawName))
				return null;

			var s = rawName;

			if (s.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
				s = s.Substring(4);

			var guidIdx = s.IndexOf("#{", StringComparison.OrdinalIgnoreCase);
			if (guidIdx >= 0)
				s = s.Substring(0, guidIdx);

			s = s.Replace('#', '\\');

			return s;
		}

		// ── PnP parent-walk + Enum registry inspection ──
		private static class PnP
		{
			private const int CR_SUCCESS = 0;

			[DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
			private static extern int CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, int ulFlags);

			[DllImport("cfgmgr32.dll")]
			private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, int ulFlags);

			[DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
			private static extern int CM_Get_Device_IDW(uint dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags);

			public static bool IsUnderViGEmBus_ByServiceOrName(string deviceInstanceId)
			{
				if (string.IsNullOrEmpty(deviceInstanceId))
					return false;

				if (CM_Locate_DevNodeW(out var devInst, deviceInstanceId, 0) != CR_SUCCESS)
					return false;

				for (int i = 0; i < 64; i++)
				{
					var id = GetDeviceInstanceId(devInst);
					if (!string.IsNullOrEmpty(id) && IsViGEmBusNode_ByEnumRegistry(id))
						return true;

					if (CM_Get_Parent(out var parent, devInst, 0) != CR_SUCCESS)
						break;

					devInst = parent;
				}

				return false;
			}

			private static string GetDeviceInstanceId(uint devInst)
			{
				var sb = new StringBuilder(1024);
				return CM_Get_Device_IDW(devInst, sb, sb.Capacity, 0) == CR_SUCCESS ? sb.ToString() : null;
			}

			private static bool IsViGEmBusNode_ByEnumRegistry(string instanceId)
			{
				try
				{
					using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + instanceId, false))
					{
						if (key == null) return false;

						var service = key.GetValue("Service") as string;
						if (!string.IsNullOrEmpty(service) &&
							service.Equals("ViGEmBus", StringComparison.OrdinalIgnoreCase))
							return true;

						var friendly = key.GetValue("FriendlyName") as string;
						var desc = key.GetValue("DeviceDesc") as string;
						var text = (friendly ?? "") + "\n" + (desc ?? "");

						if (text.IndexOf("Virtual Gamepad Emulation Bus", StringComparison.OrdinalIgnoreCase) >= 0)
							return true;
						if (text.IndexOf("Nefarius", StringComparison.OrdinalIgnoreCase) >= 0)
							return true;
					}
				}
				catch { }

				return false;
			}
		}
	}
}
