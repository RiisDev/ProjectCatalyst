using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProjectCatalyst.Models;
using ProjectCatalyst.Util;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views
{
	public partial class EmulatorSetupOverlay : OverlayControl
	{
		private readonly OverlayResult<EmulatorConfig?> _result = new();
		private EmulatorConfig? _editing;

		private bool _usersDirectoryAutoFilled;
		private bool _gamesDirectoryAutoFilled;

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public EmulatorSetupOverlay()
		{
			InitializeComponent();
			SystemTypeCombo.ItemsSource = Enum.GetValues<SystemType>();
		}

		public Task<EmulatorConfig?> ShowAsync(EmulatorConfig? existing = null)
		{
			Task<EmulatorConfig?> task = _result.Begin();
			_editing = existing;
			_usersDirectoryAutoFilled = false;
			_gamesDirectoryAutoFilled = false;
			ValidationText.Visibility = Visibility.Collapsed;

			if (existing is not null)
			{
				SystemTypeCombo.SelectedItem = existing.SystemType;
				RefreshEmulatorOptions(existing.SystemType);
				EmulatorCombo.SelectedItem = EmulatorCatalog.ForSystem(existing.SystemType)
					.FirstOrDefault(e => e.Name == existing.EmulatorName);
				ExecutablePathBox.Text = existing.ExecutablePath;
				UsersDirectoryBox.Text = existing.UsersDirectory;
				GamesDirectoryBox.Text = existing.GamesDirectory;
			}
			else
			{
				ExecutablePathBox.Text = string.Empty;
				UsersDirectoryBox.Text = string.Empty;
				GamesDirectoryBox.Text = string.Empty;
				SystemTypeCombo.SelectedIndex = 0; // triggers OnSystemTypeChanged -> populates EmulatorCombo
			}

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();
			FocusInput(ExecutablePathBox);

			return task;
		}

		private void RefreshEmulatorOptions(SystemType systemType)
		{
			EmulatorCombo.ItemsSource = EmulatorCatalog.ForSystem(systemType).ToList();
			EmulatorCombo.SelectedIndex = EmulatorCombo.Items.Count > 0 ? 0 : -1;
		}

		private void OnSystemTypeChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SystemTypeCombo.SelectedItem is SystemType systemType)
			{
				RefreshEmulatorOptions(systemType);
			}
		}

		private void OnBrowseExecutableClick(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new()
			{
				Title = "Select Emulator Executable",
				Filter = "Executable files (*.exe;*.AppImage)|*.exe;*.AppImage|All files (*.*)|*.*"
			};

			if (dialog.ShowDialog() == true)
			{
				ExecutablePathBox.Text = dialog.FileName;
			}
		}

		private void OnBrowseUsersDirectoryClick(object sender, RoutedEventArgs e)
		{
			if (!BrowseForFolder(out string folder)) return;
			
			UsersDirectoryBox.Text = folder;
			_usersDirectoryAutoFilled = false; // user chose it explicitly - don't overwrite with a later auto-detect
		}

		private void OnBrowseGamesDirectoryClick(object sender, RoutedEventArgs e)
		{
			if (!BrowseForFolder(out string folder)) return;
			
			GamesDirectoryBox.Text = folder;
			_gamesDirectoryAutoFilled = false;
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

		private async void OnExecutablePathChanged(object sender, TextChangedEventArgs e)
		{
			string path = ExecutablePathBox.Text;
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
			if (EmulatorCombo.SelectedItem is not EmulatorDefinition definition || definition.Detector is null) return;

			if (string.Equals(Path.GetExtension(path), ".AppImage", StringComparison.OrdinalIgnoreCase))
				await ExecutableRunner.EnsureAppImageDataLink();

			// Only fill fields the user hasn't manually set themselves - either
			// still blank, or previously auto-filled and not edited since.
			if (string.IsNullOrWhiteSpace(UsersDirectoryBox.Text) || _usersDirectoryAutoFilled)
			{
				string? detected = definition.Detector.TryDetectUsersDirectory(path);
				if (detected is not null)
				{
					UsersDirectoryBox.Text = detected;
					_usersDirectoryAutoFilled = true;
				}
			}

			if (string.IsNullOrWhiteSpace(GamesDirectoryBox.Text) || _gamesDirectoryAutoFilled)
			{
				string? detected = definition.Detector.TryDetectGamesDirectory(path);
				if (detected is null) return;

				GamesDirectoryBox.Text = detected;
				_gamesDirectoryAutoFilled = true;
			}
		}

		private void OnSaveClick(object sender, RoutedEventArgs e)
		{
			if (SystemTypeCombo.SelectedItem is not SystemType systemType || EmulatorCombo.SelectedItem is not EmulatorDefinition definition)
			{
				ShowValidationMessage(ValidationText, "Choose a system and an emulator.");
				return;
			}

			if (string.IsNullOrWhiteSpace(ExecutablePathBox.Text) || !File.Exists(ExecutablePathBox.Text))
			{
				ShowValidationMessage(ValidationText, "Choose a valid emulator executable.");
				return;
			}

			EmulatorConfig config = new()
			{
				Id = _editing?.Id ?? Guid.NewGuid().ToString("N"),
				SystemType = systemType,
				EmulatorName = definition.Name,
				ExecutablePath = ExecutablePathBox.Text.Trim(),
				UsersDirectory = UsersDirectoryBox.Text.Trim(),
				GamesDirectory = GamesDirectoryBox.Text.Trim()
			};

			Complete(config);
		}

		private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(null);

		private void Complete(EmulatorConfig? result)
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
