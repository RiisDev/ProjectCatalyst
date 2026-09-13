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

		public bool TryHandleBack();
	}
}
