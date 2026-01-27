using SharpDX.DirectInput;
using SharpDX.XInput;
using x360ce.Engine.Data;
using x360ce.Engine.Input.Processors;

namespace x360ce.Engine.Input.Orchestration
{
	/// <summary>
	/// Coordinates force feedback application for physical devices based on feedback received from
	/// virtual controller slots.
	/// </summary>
	/// <remarks>
	/// This type is intended to be called by the host/orchestrator layer.
	/// It keeps the Engine free of App-only dependencies (such as ViGEm) by consuming feedback via
	/// <see cref="IVirtualControllerFeedbackProvider" />.
	/// </remarks>
	public sealed class ForceFeedbackCoordinator
	{
		private readonly IVirtualControllerFeedbackProvider _feedbackProvider;
		private readonly XInputProcessor _xinputProcessor;

		/// <summary>
		/// Creates a new coordinator instance.
		/// </summary>
		/// <param name="feedbackProvider">Provider of per-slot virtual feedback values.</param>
		/// <param name="xinputProcessor">XInput processor used to apply vibration to physical XInput devices.</param>
		public ForceFeedbackCoordinator(
			IVirtualControllerFeedbackProvider feedbackProvider,
			XInputProcessor xinputProcessor)
		{
			_feedbackProvider = feedbackProvider;
			_xinputProcessor = xinputProcessor;
		}

		/// <summary>
		/// Applies force feedback to a physical device based on a mapped virtual controller slot.
		/// </summary>
		/// <param name="device">Physical device to apply force feedback to.</param>
		/// <param name="padSetting">Pad setting which controls force feedback behavior.</param>
		/// <param name="mapTo">Virtual controller slot the device is mapped to.</param>
		/// <remarks>
		/// Mapping rules:
		/// - Virtual feedback values are expected in 0..255 byte motor form.
		/// - DirectInput force feedback uses <see cref="ForceFeedbackState.SetDeviceForces" />.
		/// - XInput force feedback uses <see cref="XInputProcessor.ApplyXInputVibration" />.
		/// - Raw Input does not support force feedback output.
		/// </remarks>
		public void ApplyForDevice(UserDevice device, PadSetting padSetting, MapTo mapTo)
		{
			if (device == null)
				return;
			if (padSetting == null)
				return;
			if (padSetting.ForceEnable != "1")
				return;
			if (_feedbackProvider == null)
				return;

			if (!_feedbackProvider.TryGetFeedback(mapTo, out var feedback))
				return;

			// Convert ViGEm-style byte motor values to the SharpDX.XInput.Vibration used by ForceFeedbackState.
			var vibration = new Vibration
			{
				LeftMotorSpeed = ConvertHelper.ConvertMotorSpeed(feedback.LargeMotor),
				RightMotorSpeed = ConvertHelper.ConvertMotorSpeed(feedback.SmallMotor),
			};

			// Apply according to input method.
			switch (device.InputMethod)
			{
				case InputSourceType.DirectInput:
					ApplyDirectInputForceFeedback(device, padSetting, vibration);
					break;
				case InputSourceType.XInput:
					ApplyXInputForceFeedback(device, vibration);
					break;
				default:
					// GamingInput and RawInput are not wired yet in this coordinator.
					break;
			}
		}

		private static void ApplyDirectInputForceFeedback(UserDevice device, PadSetting padSetting, Vibration vibration)
		{
			if (device == null)
				return;
			if (device.DirectInputDevice == null)
				return;

			// Lazily create per-device state.
			if (device.FFState == null)
				device.FFState = new ForceFeedbackState();

			// Only attempt if the underlying DirectInput device reports force feedback capability.
			var caps = device.DirectInputDevice.Capabilities;
			if (!caps.Flags.HasFlag(DeviceFlags.ForceFeedback))
				return;

			device.FFState.SetDeviceForces(device, device.DirectInputDevice, padSetting, vibration);
		}

		private void ApplyXInputForceFeedback(UserDevice device, Vibration vibration)
		{
			if (device == null)
				return;
			if (_xinputProcessor == null)
				return;

			// Convert to unsigned 0..65535 range expected by ApplyXInputVibration.
			// SharpDX.XInput.Vibration represents motor values as signed shorts.
			var left = ConvertHelper.ConvertMotorSpeedToUshort(vibration.LeftMotorSpeed);
			var right = ConvertHelper.ConvertMotorSpeedToUshort(vibration.RightMotorSpeed);
			_xinputProcessor.ApplyXInputVibration(device, left, right);
		}
	}
}

