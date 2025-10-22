using SharpDX.DirectInput;
using System;
using System.Linq;
using System.Threading;

namespace x360ce.App.Input.Triggers
{
	/// <summary>
	/// Monitors DirectInput device connections using periodic polling.
	/// Triggers device list updates when DirectInput devices are connected or disconnected.
	/// </summary>
	/// <remarks>
	/// DirectInput API does not provide native device change notifications, so this class
	/// uses lightweight periodic polling combined with Windows device change messages for efficiency.
	/// Polling interval: 2000ms (2 seconds) - sufficient for user-initiated device changes.
	/// </remarks>
	internal class DirectInputDeviceConnection : IDisposable
	{
		private Timer _pollingTimer;
		private int _lastDeviceCount;
		private bool _disposed;
		private readonly object _lock = new object();

		// Polling interval in milliseconds (2 seconds is sufficient for device changes)
		private const int POLLING_INTERVAL_MS = 2000;

		/// <summary>
		/// Event raised when DirectInput device count changes (connection/disconnection detected).
		/// </summary>
		public event EventHandler<DeviceConnectionEventArgs> DeviceChanged;

		/// <summary>
		/// Starts monitoring DirectInput device connections using periodic polling.
		/// </summary>
		public void StartMonitoring()
		{
			lock (_lock)
			{
				if (_pollingTimer != null)
					return;

				// Initialize with current device count
				_lastDeviceCount = GetCurrentDeviceCount();

				// Start polling timer
				_pollingTimer = new Timer(OnPollingTick, null, POLLING_INTERVAL_MS, POLLING_INTERVAL_MS);
			}
		}

		/// <summary>
		/// Stops monitoring DirectInput device connections.
		/// </summary>
		public void StopMonitoring()
		{
			lock (_lock)
			{
				_pollingTimer?.Dispose();
				_pollingTimer = null;
			}
		}

		/// <summary>
		/// Polling timer callback that checks for device count changes.
		/// </summary>
		private void OnPollingTick(object state)
		{
			try
			{
				var currentCount = GetCurrentDeviceCount();

				lock (_lock)
				{
					if (currentCount != _lastDeviceCount)
					{
						var isConnected = currentCount > _lastDeviceCount;
						_lastDeviceCount = currentCount;

						// Raise event on change detection
						DeviceChanged?.Invoke(this, new DeviceConnectionEventArgs(isConnected));
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"DirectInputDeviceConnection: Error during polling: {ex.Message}");
			}
		}

		/// <summary>
		/// Gets the current count of DirectInput input devices (gamepads, keyboards, mice).
		/// Uses lightweight enumeration without creating device objects.
		/// Only counts device types that are processed by DirectInputDeviceInfo.
		/// </summary>
		private int GetCurrentDeviceCount()
		{
			try
			{
				using (var directInput = new DirectInput())
				{
					// Only count input device types that DirectInputDeviceInfo processes
					return directInput.GetDevices(
						DeviceClass.All,
						DeviceEnumerationFlags.AllDevices)
						.Count(device => IsInputDevice(device));
				}
			}
			catch
			{
				return 0;
			}
		}

		/// <summary>
		/// Determines if a device instance represents an actual input device.
		/// Filters out non-input devices to match DirectInputDeviceInfo filtering logic.
		/// This ensures only relevant device types trigger list updates.
		/// </summary>
		private bool IsInputDevice(DeviceInstance deviceInstance)
		{
			// Check device name/description for non-gaming input patterns
			var deviceName = (deviceInstance.InstanceName ?? "").ToLowerInvariant();
			var productName = (deviceInstance.ProductName ?? "").ToLowerInvariant();
			var combinedText = $"{deviceName} {productName}";
			
			// Filter out Input Configuration Devices and Portable Device Control
			if (combinedText.Contains("input configuration") ||
				combinedText.Contains("input_config") ||
				combinedText.Contains("inputconfig") ||
				combinedText.Contains("portable device control") ||
				combinedText.Contains("portable_device") ||
				combinedText.Contains("portabledevice"))
			{
				return false;
			}
			
			// Filter out Intel platform endpoints (HID Event Filter) - platform hotkey controllers
			// Examples: VID_494E54&PID_33D2 (INT33D2), VID_8087&PID_0000 (INTC816)
			var upperName = deviceName.ToUpperInvariant();
			if (upperName.Contains("INT33D2") || upperName.Contains("INTC816") ||
				upperName.Contains("494E54") || upperName.Contains("8087"))
			{
				return false;
			}
			
			switch (deviceInstance.Type)
			{
				// Input devices to monitor (matches DirectInputDeviceInfo filtering)
				case DeviceType.Mouse:
				case DeviceType.Keyboard:
				case DeviceType.Joystick:
				case DeviceType.Gamepad:
				case DeviceType.FirstPerson:
				case DeviceType.Flight:
				case DeviceType.Driving:
					return true;

				// Non-input devices to ignore
				default:
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
	}
}
