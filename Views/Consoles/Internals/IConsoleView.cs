using System.Windows.Input;
using ProjectCatalyst.Services;

namespace ProjectCatalyst.Views.Consoles.Internals
{
	public interface IConsoleView
	{
		event EventHandler? BackRequested;

		void MoveHorizontal(int delta);

		void MoveVertical(int delta);

		void ActivateSelected();

		void FocusDefault();

		public void PlayDirectionalAudio(Key key = default, GamepadButton gKey = default);

		public string GetResource(string category, string name);

		/// <summary>
		/// Called when Back/B/Escape is pressed while this view is active,
		/// before the default "return to the launcher" behavior. Return true
		/// if the view handled it itself (e.g. closed an internal overlay
		/// like a fullscreen photo viewer) - false to let the default
		/// behavior (return to launcher) proceed.
		/// </summary>
		public bool TryHandleBack();
	}
}
