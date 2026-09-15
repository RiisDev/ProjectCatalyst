using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	public partial class ScreenshotViewerOverlay : OverlayControl, ICloseableOverlay
	{
		private IReadOnlyList<string> _imagePaths = [];
		private int _index;

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public ScreenshotViewerOverlay()
		{
			InitializeComponent();
			KeyDown += OnKeyDown;
		}

		public void Show(IReadOnlyList<string> imagePaths, int startIndex = 0)
		{
			if (imagePaths.Count == 0) return;

			_imagePaths = imagePaths;
			_index = Math.Clamp(startIndex, 0, imagePaths.Count - 1);
			UpdateImage();

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn(cardStartScale: 0.96, fadeMs: 180);
			FocusDefault();
		}

		public void Close()
		{
			if (!IsOpen) return;
			IsOpen = false;
			AnimateOut();
		}

		public void MoveHorizontal(int delta)
		{
			if (_imagePaths.Count == 0) return;
			_index = Math.Clamp(_index + delta, 0, _imagePaths.Count - 1);
			UpdateImage();
		}

		private void UpdateImage()
		{
			string path = _imagePaths[_index];

			try
			{
				BitmapImage bitmap = new();
				bitmap.BeginInit();
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.UriSource = new Uri(path, UriKind.Absolute);
				bitmap.EndInit();
				PhotoImage.Source = bitmap;
			}
			catch (Exception)
			{
				PhotoImage.Source = null;
			}

			FileNameText.Text = Path.GetFileName(path);
			PositionText.Text = $"{_index + 1} of {_imagePaths.Count}";
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Left:
					MoveHorizontal(-1);
					e.Handled = true;
					break;

				case Key.Right:
					MoveHorizontal(1);
					e.Handled = true;
					break;

				case Key.Escape:
					Close();
					e.Handled = true;
					break;
			}
		}
	}
}
