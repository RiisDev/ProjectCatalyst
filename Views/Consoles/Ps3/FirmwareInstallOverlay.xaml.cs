using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	public partial class FirmwareInstallOverlay : OverlayControl, ICloseableOverlay
	{
		private readonly OverlayResult<string?> _result = new();

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public FirmwareInstallOverlay() => InitializeComponent();

		public Task<string?> ShowAsync()
		{
			Task<string?> task = _result.Begin();
			ValidationText.Visibility = Visibility.Collapsed;
			UpdateFilePathBox.Text = string.Empty;

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();
			FocusInput(UpdateFilePathBox);

			return task;
		}

		private void OnUpdateFileClick(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new()
			{
				Title = "Select Emulator Executable",
				Filter = "PS3 Update FIle (*.pup)|*.pup"
			};

			if (dialog.ShowDialog() == true)
			{
				UpdateFilePathBox.Text = dialog.FileName;
			}
		}

		private void OnUpdateFilePathChanged(object sender, TextChangedEventArgs e)
		{
			string path = UpdateFilePathBox.Text;
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				ShowValidationMessage(ValidationText, "File not found");
			}
		}

		private void OnSaveClick(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(UpdateFilePathBox.Text) || !File.Exists(UpdateFilePathBox.Text))
			{
				ShowValidationMessage(ValidationText, "Choose a valid PS3 update file.");
				return;
			}

			Complete(UpdateFilePathBox.Text);
		}

		private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(null);

		public void Close() => Complete(null);

		private void Complete(string? result)
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
