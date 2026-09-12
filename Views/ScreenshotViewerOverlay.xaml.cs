using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProjectCatalyst.Views
{
	public partial class ScreenshotViewerOverlay : UserControl
	{
		private IReadOnlyList<string> _imagePaths = [];
		private int _index;

		public bool IsOpen { get; private set; }

		public ScreenshotViewerOverlay()
		{
			InitializeComponent();
			Visibility = Visibility.Collapsed;
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
			AnimateIn();
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

		public void FocusDefault()
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
			{
				Focus();
				Keyboard.Focus(this);
			}));
		}

		private void AnimateIn()
		{
			DoubleAnimation fade = new(0, 1, TimeSpan.FromMilliseconds(180));
			Backdrop.BeginAnimation(OpacityProperty, fade);

			DoubleAnimation cardFade = new(0, 1, TimeSpan.FromMilliseconds(220));
			DoubleAnimation cardScale = new(0.96, 1.0, TimeSpan.FromMilliseconds(220))
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
