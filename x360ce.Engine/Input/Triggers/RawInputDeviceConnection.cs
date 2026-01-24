using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace x360ce.Engine.Input.Triggers
{
	/// <summary>
	/// Monitors RawInput device connections using Windows WM_INPUT_DEVICE_CHANGE messages.
	/// Triggers device list updates when RawInput devices are connected or disconnected.
	/// Only monitors Mouse, Keyboard, and HID device types that are processed by RawInputDeviceInfo.
	/// </summary>
	/// <remarks>
	/// Uses native Windows RawInput device change notifications (WM_INPUT_DEVICE_CHANGE) for efficient,
	/// event-driven device monitoring without polling overhead.
	/// Filters device changes to only trigger updates for device types that are actually enumerated
	/// (Mouse, Keyboard, and HID devices), preventing unnecessary list updates for other device types.
	/// </remarks>
	public class RawInputDeviceConnection : IDisposable
	{
		#region Win32 Constants and Structures

		private const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
		private const int GIDC_ARRIVAL = 1;
		private const int GIDC_REMOVAL = 2;

		// RawInput device types
		private const uint RIM_TYPEMOUSE = 0;
		private const uint RIM_TYPEKEYBOARD = 1;
		private const uint RIM_TYPEHID = 2;

		// RawInput API constants
		private const uint RIDI_DEVICEINFO = 0x2000000b;

		/// <summary>
		/// Contains information about a raw input device.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct RID_DEVICE_INFO
		{
			public uint cbSize;
			public uint dwType;
			// Union members not needed for type checking
		}

		/// <summary>
		/// Gets information about the raw input device.
		/// </summary>
		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint GetRawInputDeviceInfo(
			IntPtr hDevice,
			uint uiCommand,
			IntPtr pData,
			ref uint pcbSize);

		#endregion

		private MessageWindow _messageWindow;
		private bool _disposed;

		/// <summary>
		/// Event raised when a RawInput device is connected or disconnected.
		/// </summary>
		public event EventHandler<DeviceConnectionEventArgs> DeviceChanged;

		/// <summary>
		/// Starts monitoring RawInput device connections.
		/// </summary>
		public void StartMonitoring()
		{
			if (_messageWindow != null)
				return;

			_messageWindow = new MessageWindow(this);
		}

		/// <summary>
		/// Stops monitoring RawInput device connections.
		/// </summary>
		public void StopMonitoring()
		{
			_messageWindow?.Dispose();
			_messageWindow = null;
		}

		/// <summary>
		/// Handles RawInput device change messages from Windows.
		/// Only triggers updates for Mouse, Keyboard, and HID device types that are processed by RawInputDeviceInfo.
		/// </summary>
		private void OnDeviceChange(int changeType, IntPtr deviceHandle)
		{
			if (changeType != GIDC_ARRIVAL && changeType != GIDC_REMOVAL)
				return;

			// Filter: Only process device types that RawInputDeviceInfo enumerates
			if (!IsMonitoredDeviceType(deviceHandle))
				return;

			var isConnected = changeType == GIDC_ARRIVAL;
			DeviceChanged?.Invoke(this, new DeviceConnectionEventArgs(isConnected));
		}

		/// <summary>
		/// Checks if the device type should be monitored based on what RawInputDeviceInfo processes.
		/// Returns true for Mouse, Keyboard, and HID devices only.
		/// </summary>
		/// <param name="deviceHandle">Device handle from WM_INPUT_DEVICE_CHANGE message</param>
		/// <returns>True if device type should trigger list updates</returns>
		private bool IsMonitoredDeviceType(IntPtr deviceHandle)
		{
			try
			{
				// Get device info to determine device type
				uint size = (uint)Marshal.SizeOf<RID_DEVICE_INFO>();
				IntPtr buffer = Marshal.AllocHGlobal((int)size);
				try
				{
					uint result = GetRawInputDeviceInfo(deviceHandle, RIDI_DEVICEINFO, buffer, ref size);
					if (result == uint.MaxValue)
						return false;

					var deviceInfo = Marshal.PtrToStructure<RID_DEVICE_INFO>(buffer);
					
					// Only monitor device types that RawInputDeviceInfo.GetRawInputDeviceInfoList() processes:
					// - Mouse (RIM_TYPEMOUSE = 0)
					// - Keyboard (RIM_TYPEKEYBOARD = 1)
					// - HID (RIM_TYPEHID = 2)
					// These match the filtering in RawInputDeviceInfo.ShouldProcessDevice()
					return deviceInfo.dwType == RIM_TYPEMOUSE ||
					       deviceInfo.dwType == RIM_TYPEKEYBOARD ||
					       deviceInfo.dwType == RIM_TYPEHID;
				}
				finally
				{
					Marshal.FreeHGlobal(buffer);
				}
			}
			catch
			{
				// If we can't determine device type, don't trigger update to avoid unnecessary processing
				return false;
			}
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
		/// Hidden window for receiving RawInput device change messages.
		/// </summary>
		private class MessageWindow : NativeWindow, IDisposable
		{
			private readonly RawInputDeviceConnection _parent;

			public MessageWindow(RawInputDeviceConnection parent)
			{
				_parent = parent;
				CreateHandle(new CreateParams());
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == WM_INPUT_DEVICE_CHANGE)
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
}
