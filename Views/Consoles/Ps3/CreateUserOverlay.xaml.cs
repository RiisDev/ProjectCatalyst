using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ProjectCatalyst.Models;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	public partial class CreateUserOverlay
	{
		private TaskCompletionSource<bool?>? _tcs;

		public bool IsOpen { get; private set; }

		public CreateUserOverlay()
		{
			InitializeComponent();
			Visibility = Visibility.Collapsed;
		}

		public Task<bool?> ShowAsync(EmulatorConfig? existing = null)
		{
			_tcs = new TaskCompletionSource<bool?>();
			ValidationText.Visibility = Visibility.Collapsed;

			UpdateUsernameBox.Text = existing is not null ? existing.ExecutablePath : string.Empty;

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();

			return _tcs.Task;
		}
		
		private void OnUpdateUsername(object sender, TextChangedEventArgs e)
		{
			string username = UpdateUsernameBox.Text;
			if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 16)
			{
				ShowValidationMessage("Please enter a valid username 3-16 characters.");
			}
		}

		private void OnCreateUserClick(object sender, RoutedEventArgs e)
		{
			string username = UpdateUsernameBox.Text;
			if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 16)
			{
				ShowValidationMessage("Please enter a valid username 3-16 characters.");
				return;
			}

			if (DataContext is MainWindow mainWindow)
			{
				if (mainWindow.ActiveConsoleView is Ps3ConsoleView consoleView)
				{
					consoleView.RPCS3.CreateUser(username);
					consoleView.ReloadCategories();
				}
			} else LogError("Failed to create username, cannot find ConsoleView");

			Close();
		}

		private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(null);

		private void ShowValidationMessage(string message)
		{
			ValidationText.Text = message;
			ValidationText.Visibility = Visibility.Visible;
		}

		private void Complete(bool? result)
		{
			if (_tcs is null) return;

			TaskCompletionSource<bool?> tcs = _tcs;
			_tcs = null;
			IsOpen = false;

			AnimateOut();
			tcs.TrySetResult(result);
		}

		public void Close()
		{
			if (!IsOpen) return;
			IsOpen = false;
			AnimateOut();
		}

		private void AnimateIn()
		{
			DoubleAnimation fade = new(0, 1, TimeSpan.FromMilliseconds(200));
			Backdrop.BeginAnimation(OpacityProperty, fade);

			DoubleAnimation cardFade = new(0, 1, TimeSpan.FromMilliseconds(220));
			DoubleAnimation cardScale = new(0.94, 1.0, TimeSpan.FromMilliseconds(220))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			Card.BeginAnimation(OpacityProperty, cardFade);
			CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, cardScale);
			CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, cardScale);
		}

		private void AnimateOut()
		{
			DoubleAnimation fadeOut = new(1, 0, TimeSpan.FromMilliseconds(160));
			fadeOut.Completed += (_, _) => Visibility = Visibility.Collapsed;
			Backdrop.BeginAnimation(OpacityProperty, fadeOut);
			Card.BeginAnimation(OpacityProperty, fadeOut);
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Escape) return;

			Complete(null);
			e.Handled = true;
		}
	}
}
