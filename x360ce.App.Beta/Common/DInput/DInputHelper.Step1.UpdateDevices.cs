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
			// List of connected devices.
			var deviceInstanceGuid = devices.Select(x => x.InstanceGuid).ToList();
			// We need device tree info to know which XInput slots are backed by ViGEmBus (virtual pads).
			// Without this, x360ce will detect its own virtual controllers and loop back.
			DeviceInfo[] devInfos = DeviceDetector.GetDevices();
			DeviceInfo[] intInfos = DeviceDetector.GetInterfaces();

			// Detect ViGEm-backed XInput slots robustly (no registry XInputIndex, no DeviceDetector parent chain).
			var vigemSlots = DetectViGEmOwnedXInputSlots_FromRawInput();
			XInputInterop.SetViGEmOwnedSlots(vigemSlots);
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

			// Create synthetic devices using LOGICAL indices (0..count-1)
			var nativeXInputGuids = new List<Guid>();
			for (uint logical = 0; logical < connectedRealSlots.Count && logical < 4; logical++)
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
			//Joystick    = new Guid("6f1d2b70-d5a0-11cf-bfc7-444553540000");
			//SysMouse    = new Guid("6f1d2b60-d5a0-11cf-bfc7-444553540000");
			//SysKeyboard = new Guid("6f1d2b61-d5a0-11cf-bfc7-444553540000");
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
						// If ViGEm hardware found then...
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
			//if (insertDevices.Count > 0)
			//{
			//	CloudPanel.Add(CloudAction.Insert, insertDevices.ToArray(), true);
			//}
			for (int i = 0; i < updatedDevices.Length; i++)
			{
				var device = updatedDevices[i];
				var ud = uds.First(x => x.InstanceGuid.Equals(device.InstanceGuid));
				DeviceInfo hid;
				// Will refresh device and fill more values with new x360ce app if available.
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
			//
			// Use a snapshot (array copy) for all reads/queries, matching
			// the pattern used throughout the rest of this method.
			// Only touch Items directly (inside lock) for writes.
			// This avoids "Collection was modified" exceptions caused by
			// Items.Add() triggering CollectionChanged events that
			// re-entrantly enumerate the same collection.
			// ══════════════════════════════════════════════════════════
			// Take a fresh snapshot AFTER the DInput inserts above.
			var xiSnapshot = SettingsManager.UserDevices.ItemsToArraySyncronized();
			foreach (var synGuid in nativeXInputGuids)
			{
				var existing = xiSnapshot.FirstOrDefault(x => x.InstanceGuid == synGuid);
				if (existing == null)
				{
					// Determine slot from GUID.
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
					// Ensure the display name includes the slot number
					// (handles devices that were persisted with an older name format).
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
					// Mark online if not already.
					if (!existing.IsOnline)
						lock (SettingsManager.UserDevices.SyncRoot)
							existing.IsOnline = true;
				}
			}
			// Mark disconnected native XInput devices as offline.
			// Re-snapshot after potential adds above.
			var xiSnapshotOffline = SettingsManager.UserDevices.ItemsToArraySyncronized();
			for (uint slot = 0; slot < 4; slot++)
			{
				var synGuid = XInputInterop.GetSyntheticInstanceGuid(slot);
				// If this slot is NOT in the connected list...
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
			//	var game = CurrentGame;
			//	if (game != null)
			//	{
			//		// Auto-configure new devices.
			//		AutoConfigure(game);
			//	}
		}

		/// <summary>
		/// Refresh device.
		/// </summary>
		void RefreshDevice(DirectInput manager, UserDevice ud, DeviceInstance device, DeviceInfo[] allDevices, DeviceInfo[] allInterfaces, out DeviceInfo hid)
		{
			hid = null;
			if (Program.IsClosing)
				return;
			// If device added then...
			if (ud.Device == null)
			{
				try
				{
					// Lock to avoid Exception: Collection was modified; enumeration operation may not execute.
					lock (SettingsManager.UserDevices.SyncRoot)
					{
						// Getting state can fail.
						var joystick = new Joystick(manager, device.InstanceGuid);
						ud.Device = joystick;
						ud.IsExclusiveMode = null;
						ud.LoadCapabilities(joystick.Capabilities);
					}
				}
				catch (Exception) { }
			}
			// Lock to avoid Exception: Collection was modified; enumeration operation may not execute.
			lock (SettingsManager.UserDevices.SyncRoot)
			{
				ud.LoadInstance(device);
			}
			// If device is set as offline then make it online.
			if (!ud.IsOnline)
				lock (SettingsManager.UserDevices.SyncRoot)
					ud.IsOnline = true;
			// Get device info for added devices.
			var dev = allDevices.FirstOrDefault(x => x.DeviceId == ud.HidDeviceId);
			// Lock to avoid Exception: Collection was modified; enumeration operation may not execute.
			lock (SettingsManager.UserDevices.SyncRoot)
			{
				ud.LoadDevDeviceInfo(dev);
				if (dev != null)
					ud.ConnectionClass = DeviceDetector.GetConnectionDevice(dev, allDevices)?.ClassGuid ?? Guid.Empty;
			}
			// InterfacePath is available for HID devices.
			if (device.IsHumanInterfaceDevice && ud.Device != null)
			{
				var interfacePath = ud.Device.Properties.InterfacePath;
				// Get interface info for added devices.
				hid = allInterfaces.FirstOrDefault(x => x.DevicePath == interfacePath);
				// Lock to avoid Exception: Collection was modified; enumeration operation may not execute.
				lock (SettingsManager.UserDevices.SyncRoot)
				{
					ud.LoadHidDeviceInfo(hid);
					if (hid != null)
						ud.ConnectionClass = DeviceDetector.GetConnectionDevice(hid, allDevices)?.ClassGuid ?? Guid.Empty;
					// Workaround: 
					// Override Device values and description from the Interface, 
					// because it is more accurate and present.
					// Note 1: Device fields below, probably, should not be used.
					// Note 2: Available when device is online.
					ud.DevManufacturer = ud.HidManufacturer;
					ud.DevDescription = ud.HidDescription;
					ud.DevVendorId = ud.HidVendorId;
					ud.DevProductId = ud.HidProductId;
					ud.DevRevision = ud.HidRevision;
				}
			}
		}
		// -------------------------
		// RawInput -> XInput slot map
		// -------------------------

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

		private static HashSet<uint> DetectViGEmOwnedXInputSlots_FromRawInput()
		{
			var slots = new HashSet<uint>();

			uint num = 0;
			uint cbSize = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));

			if (GetRawInputDeviceList(null, ref num, cbSize) == 0xFFFFFFFF)
				return slots;

			var list = new RAWINPUTDEVICELIST[num];
			if (GetRawInputDeviceList(list, ref num, cbSize) == 0xFFFFFFFF)
				return slots;

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
				var slot = TryParseSlotFromRawInputName(rawName);
				if (!slot.HasValue)
					continue;

				var pnpId = RawInputNameToPnPInstanceId(rawName);
				if (string.IsNullOrEmpty(pnpId))
					continue;

				// Key change: detect ViGEm by walking parents and checking Enum registry values
				if (PnP.IsUnderViGEmBus_ByServiceOrName(pnpId))
					slots.Add(slot.Value);
			}

			return slots;
		}
		private static uint? TryParseSlotFromRawInputName(string rawName)
		{
			var ig = rawName?.IndexOf("IG_", StringComparison.OrdinalIgnoreCase) ?? -1;
			if (ig < 0 || ig + 5 > rawName.Length)
				return null;

			var two = rawName.Substring(ig + 3, 2);
			if (!uint.TryParse(two, out var slot))
				return null;

			return slot <= 3 ? (uint?)slot : null;
		}

		private static string RawInputNameToPnPInstanceId(string rawName)
		{
			// Convert:
			// \\?\HID#VID_045E&PID_028E&IG_00#A#B#{GUID}
			// -> HID\VID_045E&PID_028E&IG_00\A\B
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

		// -------------------------
		// PnP parent-walk + Enum registry inspection
		// (works with your ROOT\SYSTEM\0001 bus)
		// -------------------------
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
				// This is the important part for your case:
				// the bus node may be ROOT\SYSTEM\0001 with generic HWID.
				// But it will still have meaningful Enum values like Service/FriendlyName/DeviceDesc.
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

						// Match what you see in Device Manager
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
