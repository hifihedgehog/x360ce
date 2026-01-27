namespace x360ce.Engine.Input.Orchestration
{
	/// <summary>
	/// Provides force feedback values for virtual controller slots.
	/// </summary>
	/// <remarks>
	/// Implemented by the host (for example <c>x360ce.App</c>) to supply feedback captured from the
	/// virtual device layer (for example ViGEm). This keeps <c>x360ce.Engine</c> free of App-only
	/// dependencies.
	/// </remarks>
	public interface IVirtualControllerFeedbackProvider
	{
		/// <summary>
		/// Attempts to get the latest feedback for the specified virtual controller slot.
		/// </summary>
		/// <param name="mapTo">Virtual controller slot.</param>
		/// <param name="feedback">Latest feedback values, if available.</param>
		/// <returns><c>true</c> if feedback is available; otherwise <c>false</c>.</returns>
		bool TryGetFeedback(global::x360ce.Engine.MapTo mapTo, out VirtualControllerFeedback feedback);
	}
}

