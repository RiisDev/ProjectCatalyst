using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using ProjectCatalyst.Models;

namespace ProjectCatalyst.Views
{
	public partial class FirmwareInstallOverlay
	{
		private TaskCompletionSource<EmulatorConfig?>? _tcs;
		private EmulatorConfig? _editing;

		private bool _usersDirectoryAutoFilled;
		private bool _gamesDirectoryAutoFilled;

		public bool IsOpen { get; private set; }

		public FirmwareInstallOverlay()
		{
			InitializeComponent();
			Visibility = Visibility.Collapsed;
		}

		public Task<EmulatorConfig?> ShowAsync(EmulatorConfig? existing = null)
		{
			_tcs = new TaskCompletionSource<EmulatorConfig?>();
			_editing = existing;
			_usersDirectoryAutoFilled = false;
			_gamesDirectoryAutoFilled = false;
			ValidationText.Visibility = Visibility.Collapsed;

			if (existing is not null)
			{
				RefreshEmulatorOptions(existing.SystemType);
				ExecutablePathBox.Text = existing.ExecutablePath;
			}
			else
			{
				ExecutablePathBox.Text = string.Empty;
			}

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();

			return _tcs.Task;
		}

		private void RefreshEmulatorOptions(SystemType systemType)
		{
		}

		private void OnSystemTypeChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		private void OnBrowseExecutableClick(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new()
			{
				Title = "Select Emulator Executable",
				Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
			};

			if (dialog.ShowDialog() == true)
			{
				ExecutablePathBox.Text = dialog.FileName;
			}
		}
		
		private static bool BrowseForFolder(out string folder)
		{
			OpenFolderDialog dialog = new() { Title = "Select Folder" };
			if (dialog.ShowDialog() == true)
			{
				folder = dialog.FolderName;
				return true;
			}

			folder = string.Empty;
			return false;
		}

		private void OnExecutablePathChanged(object sender, TextChangedEventArgs e)
		{
			string path = ExecutablePathBox.Text;
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

			
		}

		private void OnSaveClick(object sender, RoutedEventArgs e)
		{
			
			if (string.IsNullOrWhiteSpace(ExecutablePathBox.Text) || !File.Exists(ExecutablePathBox.Text))
			{
				ShowValidationMessage("Choose a valid emulator executable.");
				return;
			}


			Complete(null);
		}

		private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(null);

		private void ShowValidationMessage(string message)
		{
			ValidationText.Text = message;
			ValidationText.Visibility = Visibility.Visible;
		}

		private void Complete(EmulatorConfig? result)
		{
			if (_tcs is null) return;

			TaskCompletionSource<EmulatorConfig?> tcs = _tcs;
			_tcs = null;
			IsOpen = false;

			AnimateOut();
			tcs.TrySetResult(result);
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
