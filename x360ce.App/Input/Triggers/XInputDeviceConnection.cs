using System;
using System.Threading;
using SharpDX.XInput;

namespace x360ce.App.Input.Triggers
{
	/// <summary>
	/// Monitors XInput device connections using periodic polling.
	/// Triggers device list updates when XInput controllers are connected or disconnected.
	/// </summary>
	/// <remarks>
	/// XInput API does not provide native device change notifications, so this class
	/// uses lightweight periodic polling of the 4 XInput controller slots.
	/// Polling interval: 1000ms (1 second) - XInput polling is very lightweight.
	/// </remarks>
	internal class XInputDeviceConnection : IDisposable
	{
		private Timer _pollingTimer;
		private readonly bool[] _lastSlotStates = new bool[4];
		private bool _disposed;
		private readonly object _lock = new object();

		// Polling interval in milliseconds (1 second - XInput polling is very fast)
		private const int POLLING_INTERVAL_MS = 1000;

		/// <summary>
		/// Event raised when XInput controller connection state changes.
		/// </summary>
		public event EventHandler<DeviceConnectionEventArgs> DeviceChanged;

		/// <summary>
		/// Starts monitoring XInput controller connections using periodic polling.
		/// </summary>
		public void StartMonitoring()
		{
			lock (_lock)
			{
				if (_pollingTimer != null)
					return;

				// Initialize with current slot states
				UpdateSlotStates();

				// Start polling timer
				_pollingTimer = new Timer(OnPollingTick, null, POLLING_INTERVAL_MS, POLLING_INTERVAL_MS);
			}
		}

		/// <summary>
		/// Stops monitoring XInput controller connections.
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
		/// Polling timer callback that checks for controller state changes.
		/// </summary>
		private void OnPollingTick(object state)
		{
			try
			{
				bool changeDetected = false;
				bool isConnected = false;

				lock (_lock)
				{
					for (int i = 0; i < 4; i++)
					{
						var controller = new Controller((UserIndex)i);
						var currentState = controller.IsConnected;

						if (currentState != _lastSlotStates[i])
						{
							_lastSlotStates[i] = currentState;
							changeDetected = true;
							isConnected = currentState;
						}
					}
				}

				// Raise event if any slot changed (outside lock to avoid deadlock)
				if (changeDetected)
				{
					DeviceChanged?.Invoke(this, new DeviceConnectionEventArgs(isConnected));
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"XInputDeviceConnection: Error during polling: {ex.Message}");
			}
		}

		/// <summary>
		/// Updates the current state of all XInput controller slots.
		/// </summary>
		private void UpdateSlotStates()
		{
			for (int i = 0; i < 4; i++)
			{
				try
				{
					var controller = new Controller((UserIndex)i);
					_lastSlotStates[i] = controller.IsConnected;
				}
				catch
				{
					_lastSlotStates[i] = false;
				}
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
