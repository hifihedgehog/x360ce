using x360ce.Engine.Data;

namespace x360ce.Engine.Input.Orchestration
{
	/// <summary>
	/// Resolves a <c>Windows.Gaming.Input.Gamepad</c> index for the specified <see cref="UserDevice" />.
	/// </summary>
	/// <remarks>
	/// The Windows Gaming Input API exposes gamepads via a global collection
	/// (<c>Windows.Gaming.Input.Gamepad.Gamepads</c>) where the ordering can change when devices connect/disconnect.
	/// x360ce therefore requires the host to provide a mapping strategy for multi-controller setups.
	/// </remarks>
	/// <param name="device">The device for which a Gaming Input state should be read.</param>
	/// <returns>
	/// Zero-based gamepad index, or <c>null</c> to let the processor fall back to its default selection logic.
	/// </returns>
	public delegate int? GamingInputGamepadIndexResolver(UserDevice device);
}

