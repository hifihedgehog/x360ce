namespace x360ce.Engine.Input.Orchestration
{
	/// <summary>
	/// Represents force feedback reported by a virtual controller slot (for example, an Xbox 360 virtual target).
	/// </summary>
	/// <remarks>
	/// x360ce typically receives feedback values in 0..255 "byte motor" form (large motor, small motor),
	/// which are then converted to the appropriate output format for the physical input method.
	/// </remarks>
	public struct VirtualControllerFeedback
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="VirtualControllerFeedback" /> struct.
		/// </summary>
		public VirtualControllerFeedback(byte largeMotor, byte smallMotor, byte ledNumber)
		{
			LargeMotor = largeMotor;
			SmallMotor = smallMotor;
			LedNumber = ledNumber;
		}

		/// <summary>Large motor speed (0..255).</summary>
		public byte LargeMotor { get; }

		/// <summary>Small motor speed (0..255).</summary>
		public byte SmallMotor { get; }

		/// <summary>LED number (0..255). Not used for physical device output.</summary>
		public byte LedNumber { get; }

		/// <summary>Gets an "all zeros" feedback value.</summary>
		public static readonly VirtualControllerFeedback Empty = new VirtualControllerFeedback(0, 0, 0);
	}
}

