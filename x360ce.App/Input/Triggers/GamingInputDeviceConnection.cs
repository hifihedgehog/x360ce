using System;
using Windows.Gaming.Input;

namespace x360ce.App.Input.Triggers
{
	/// <summary>
	/// Monitors GamingInput device connections using native event notifications.
	/// Triggers device list updates when GamingInput gamepads are connected or disconnected.
	/// </summary>
	/// <remarks>
	/// Uses native Windows.Gaming.Input event system (GamepadAdded/GamepadRemoved) for efficient,
	/// event-driven device monitoring without polling overhead.
	/// Requires Windows 10+ for Gaming Input API availability.
	/// </remarks>
	internal class GamingInputDeviceConnection : IDisposable
	{
		private bool _isMonitoring;
		private bool _disposed;
		private readonly object _lock = new object();

		/// <summary>
		/// Event raised when a GamingInput gamepad is connected or disconnected.
		/// </summary>
		public event EventHandler<DeviceConnectionEventArgs> DeviceChanged;

		/// <summary>
		/// Starts monitoring GamingInput gamepad connections using native events.
		/// </summary>
		public void StartMonitoring()
		{
			lock (_lock)
			{
				if (_isMonitoring)
					return;

				try
				{
					// Register for native Gaming Input events
					Gamepad.GamepadAdded += OnGamepadAdded;
					Gamepad.GamepadRemoved += OnGamepadRemoved;
					_isMonitoring = true;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"GamingInputDeviceConnection: Failed to start monitoring: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Stops monitoring GamingInput gamepad connections.
		/// </summary>
		public void StopMonitoring()
		{
			lock (_lock)
			{
				if (!_isMonitoring)
					return;

				try
				{
					// Unregister from native Gaming Input events
					Gamepad.GamepadAdded -= OnGamepadAdded;
					Gamepad.GamepadRemoved -= OnGamepadRemoved;
					_isMonitoring = false;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"GamingInputDeviceConnection: Error stopping monitoring: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Handles gamepad added event from Windows.Gaming.Input.
		/// </summary>
		private void OnGamepadAdded(object sender, Gamepad gamepad)
		{
			try
			{
				DeviceChanged?.Invoke(this, new DeviceConnectionEventArgs(true));
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"GamingInputDeviceConnection: Error in GamepadAdded handler: {ex.Message}");
			}
		}

		/// <summary>
		/// Handles gamepad removed event from Windows.Gaming.Input.
		/// </summary>
		private void OnGamepadRemoved(object sender, Gamepad gamepad)
		{
			try
			{
				DeviceChanged?.Invoke(this, new DeviceConnectionEventArgs(false));
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"GamingInputDeviceConnection: Error in GamepadRemoved handler: {ex.Message}");
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
