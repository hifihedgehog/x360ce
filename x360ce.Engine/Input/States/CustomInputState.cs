namespace x360ce.Engine.Input.States
{
	/// <summary>
	/// Standardized state representation for all input methods (RawInput, DirectInput, XInput, GamingInput).
	/// Provides a custom format: ((axes), (sliders), (buttons), (povs)).
	/// </summary>
	/// <remarks>
	/// Format Specification:
	/// • Axes: List of axis values (0-65535 range), empty list if no axes.
	/// • Sliders: List of slider values (0-65535 range), empty list if no sliders.
	/// • Buttons: List of button states (0=released, 1=pressed), empty list if no buttons.
	/// • POVs: List of POV/D-Pad values (-1=neutral, 0-27000 = direction in centidegrees, 0 = North, 9000 = East, 18000 = South, 27000 = West), empty list if no POVs.
	///
	/// Example: ((32100,3566,0,0,31540),(),(0,0,0,1,0,0,0,0,0,0),(-1,0))
	/// Note: Empty collections are represented as empty lists (), not null
	/// </remarks>
	public partial class CustomInputState
	{
		public const int MaxAxes = 24; // (3 x 8)
		public const int MaxSliders = 8; // (2 x 4).
		public const int MaxPOVs = 4;
		public const int MaxButtons = 256;

		/// <summary>
		/// Axis values in 0-65535 range.
		/// Typically includes: X, Y, Z, RX, RY, RZ axes.
		/// </summary>
		public int[] Axes { get; set; } = new int[MaxAxes];

		/// <summary>
		/// Slider values in 0-65535 range.
		/// </summary>
		public int[] Sliders { get; set; } = new int[MaxSliders];

		/// <summary>
		/// Button states: 0 = released, 1 = pressed.
		/// </summary>
		public int[] Buttons { get; set; } = new int[MaxButtons];

		/// <summary>
		/// POV/D-Pad values: -1 = neutral/centered, 0-27000 = direction in centidegrees.
		/// 0 = North, 9000 = East, 18000 = South, 27000 = West.
		/// </summary>
		public int[] POVs { get; set; } = new int[MaxPOVs] { -1, -1, -1, -1 };

		/// <summary>
		/// Initializes a new ListTypeState with empty collections.
		/// </summary>
		public CustomInputState()
		{
		}

	}
}
