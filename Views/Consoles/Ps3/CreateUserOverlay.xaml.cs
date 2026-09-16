using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	public partial class CreateUserOverlay : ICloseableOverlay
	{
		private readonly OverlayResult<bool?> _result = new();

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public CreateUserOverlay() => InitializeComponent();

		public Task<bool?> ShowAsync()
		{
			Task<bool?> task = _result.Begin();
			ValidationText.Visibility = Visibility.Collapsed;
			UpdateUsernameBox.Text = string.Empty;

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();
			FocusInput(UpdateUsernameBox);

			return task;
		}

		private void OnUpdateUsername(object sender, TextChangedEventArgs e)
		{
			string username = UpdateUsernameBox.Text;
			if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 16)
			{
				ShowValidationMessage(ValidationText, "Please enter a valid username 3-16 characters.");
			}
		}

		private void OnCreateUserClick(object sender, RoutedEventArgs e)
		{
			string username = UpdateUsernameBox.Text;
			if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 16)
			{
				ShowValidationMessage(ValidationText, "Please enter a valid username 3-16 characters.");
				return;
			}

			if (DataContext is MainWindow { ActiveConsoleView: Ps3ConsoleView consoleView })
			{
				consoleView.RPCS3.CreateUser(username);
				consoleView.ReloadCategories();
			}
			else LogError("Failed to create username, cannot find ConsoleView");

			Complete(true);
		}

		private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(null);

		public void Close() => Complete(null);

		private void Complete(bool? result)
		{
			if (!_result.TryComplete(result)) return;

			IsOpen = false;
			AnimateOut();
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Escape) return;

			Complete(null);
			e.Handled = true;
		}
	}
}
