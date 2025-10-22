using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace x360ce.App.Input.Triggers
{
	/// <summary>
	/// Monitors PnP Input device connections using Windows WM_DEVICECHANGE messages.
	/// Triggers device list updates when devices are connected or disconnected.
	/// Only monitors input device classes: HID, Keyboard, and Mouse.
	/// </summary>
	/// <remarks>
	/// Uses native Windows device change notifications (WM_DEVICECHANGE) for efficient,
	/// event-driven device monitoring without polling overhead.
	/// Filters device changes to match the device classes enumerated by PnPInputDeviceInfo:
	/// - GUID_DEVCLASS_HIDCLASS (HID devices)
	/// - GUID_DEVCLASS_KEYBOARD (Keyboards)
	/// - GUID_DEVCLASS_MOUSE (Mice and pointing devices)
	/// </remarks>
	internal class PnPInputDeviceConnection : IDisposable
	{
		#region Win32 Constants and Structures

		private const int WM_DEVICECHANGE = 0x0219;
		private const int DBT_DEVICEARRIVAL = 0x8000;
		private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
		private const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;

		/// <summary>
		/// Known input device class GUIDs - must match PnPInputDeviceInfo enumeration.
		/// </summary>
		private static readonly Guid GUID_DEVCLASS_HIDCLASS = new Guid("745a17a0-74d3-11d0-b6fe-00a0c90f57da");
		private static readonly Guid GUID_DEVCLASS_KEYBOARD = new Guid("4d36e96b-e325-11ce-bfc1-08002be10318");
		private static readonly Guid GUID_DEVCLASS_MOUSE = new Guid("4d36e96f-e325-11ce-bfc1-08002be10318");

		[StructLayout(LayoutKind.Sequential)]
		private struct DEV_BROADCAST_HDR
		{
			public int dbch_size;
			public int dbch_devicetype;
			public int dbch_reserved;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct DEV_BROADCAST_DEVICEINTERFACE
		{
			public int dbcc_size;
			public int dbcc_devicetype;
			public int dbcc_reserved;
			public Guid dbcc_classguid;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
			public string dbcc_name;
		}

		#endregion

		private MessageWindow _messageWindow;
		private bool _disposed;

		/// <summary>
		/// Event raised when a PnP Input device is connected or disconnected.
		/// </summary>
		public event EventHandler<DeviceConnectionEventArgs> DeviceChanged;

		/// <summary>
		/// Starts monitoring PnP Input device connections.
		/// </summary>
		public void StartMonitoring()
		{
			if (_messageWindow != null)
				return;

			_messageWindow = new MessageWindow(this);
		}

		/// <summary>
		/// Stops monitoring PnP Input device connections.
		/// </summary>
		public void StopMonitoring()
		{
			_messageWindow?.Dispose();
			_messageWindow = null;
		}

		/// <summary>
		/// Handles device change messages from Windows.
		/// Filters to only process input device classes (HID, Keyboard, Mouse).
		/// </summary>
		private void OnDeviceChange(int eventType, IntPtr data)
		{
			if (eventType != DBT_DEVICEARRIVAL && eventType != DBT_DEVICEREMOVECOMPLETE)
				return;

			if (data == IntPtr.Zero)
				return;

			var hdr = Marshal.PtrToStructure<DEV_BROADCAST_HDR>(data);
			if (hdr.dbch_devicetype != DBT_DEVTYP_DEVICEINTERFACE)
				return;

			// Extract device interface information to check device class GUID
			var deviceInterface = Marshal.PtrToStructure<DEV_BROADCAST_DEVICEINTERFACE>(data);
			
			// Only process input device classes that match PnPInputDeviceInfo enumeration
			if (!IsInputDeviceClass(deviceInterface.dbcc_classguid))
				return;

			var isConnected = eventType == DBT_DEVICEARRIVAL;
			DeviceChanged?.Invoke(this, new DeviceConnectionEventArgs(isConnected));
		}

		/// <summary>
		/// Determines if the device class GUID represents an input device.
		/// Must match the device classes enumerated by PnPInputDeviceInfo.
		/// </summary>
		/// <param name="classGuid">Device class GUID to check</param>
		/// <returns>True if the device class is an input device (HID, Keyboard, or Mouse)</returns>
		private bool IsInputDeviceClass(Guid classGuid)
		{
			return classGuid == GUID_DEVCLASS_HIDCLASS ||
			       classGuid == GUID_DEVCLASS_KEYBOARD ||
			       classGuid == GUID_DEVCLASS_MOUSE;
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			StopMonitoring();
			_disposed = true;
		}

		#region Message Window

		/// <summary>
		/// Hidden window for receiving device change messages.
		/// </summary>
		private class MessageWindow : NativeWindow, IDisposable
		{
			private readonly PnPInputDeviceConnection _parent;

			public MessageWindow(PnPInputDeviceConnection parent)
			{
				_parent = parent;
				CreateHandle(new CreateParams());
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == WM_DEVICECHANGE)
				{
					_parent.OnDeviceChange(m.WParam.ToInt32(), m.LParam);
				}

				base.WndProc(ref m);
			}

			public void Dispose()
			{
				DestroyHandle();
			}
		}

		#endregion
	}

	/// <summary>
	/// Event arguments for device connection changes.
	/// </summary>
	public class DeviceConnectionEventArgs : EventArgs
	{
		public bool IsConnected { get; }

		public DeviceConnectionEventArgs(bool isConnected)
		{
			IsConnected = isConnected;
		}
	}
}
