using System;
using System.Windows.Forms;

namespace x360ce.App
{
	/// <summary>
	/// Provides a single persistent invisible window handle for
	/// DirectInput DISCL_BACKGROUND cooperative level. This window
	/// is never shown and never participates in focus, so devices
	/// bound to it stay acquired permanently.
	/// </summary>
	public static class FFBackgroundWindow
	{
		private static readonly object _lock = new object();
		private static NativeWindow _nw;

		/// <summary>
		/// Get or create the hidden window handle.
		/// Thread-safe, lazily initialized, lives for process lifetime.
		/// </summary>
		public static IntPtr GetHandle()
		{
			if (_nw != null && _nw.Handle != IntPtr.Zero)
				return _nw.Handle;

			lock (_lock)
			{
				if (_nw != null && _nw.Handle != IntPtr.Zero)
					return _nw.Handle;

				_nw = new NativeWindow();
				_nw.CreateHandle(new CreateParams
				{
					Caption = "x360ce_FF_Background",
					Width = 1,
					Height = 1
					// No WS_VISIBLE — invisible window
				});

				return _nw.Handle;
			}
		}
	}
}
