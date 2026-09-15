using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	/// <summary>
	/// Reusable slider overlay for adjusting a 0.0-1.0 volume level -
	/// used for both "Ambient Volume" and "Movement Volume" in Settings,
	/// parameterized by title/starting value rather than being two
	/// near-identical screens.
	///
	/// Gamepad-navigable: the hosting console view calls Adjust/ConfirmSelected/Close
	/// directly while IsOpen is true, since gamepad input bypasses WPF's
	/// normal keyboard focus/bubbling entirely (same pattern as
	/// ScreenshotViewerOverlay / BackgroundVideoSelectorOverlay).
	/// </summary>
	public partial class VolumeAdjustOverlay : UserControl
	{
		private const double GamepadStep = 0.05;

		private double _value;
		private TaskCompletionSource<double?>? _tcs;

		public bool IsOpen { get; private set; }

		public VolumeAdjustOverlay()
		{
			InitializeComponent();
			Visibility = Visibility.Collapsed;
		}

		/// <summary>Opens the overlay pre-set to currentValue (0.0-1.0). Returns the confirmed value, or null if cancelled.</summary>
		public Task<double?> ShowAsync(string title, double currentValue)
		{
			_tcs = new TaskCompletionSource<double?>();

			TitleTextBlock.Text = title;
			_value = Math.Clamp(currentValue, 0, 1);
			UpdateDisplay();

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();
			FocusDefault();

			return _tcs.Task;
		}

		/// <summary>Nudges the value by delta (positive or negative), clamped to 0.0-1.0.</summary>
		public void Adjust(double delta)
		{
			_value = Math.Clamp(_value + delta, 0, 1);
			UpdateDisplay();
		}

		private void UpdateDisplay()
		{
			VolumeSlider.Value = _value;
			PercentText.Text = _value.ToString("P0");
		}

		public void ConfirmSelected() => Complete(_value);

		public void Close() => Complete(null);

		private void Complete(double? result)
		{
			if (_tcs is null) return;

			TaskCompletionSource<double?> tcs = _tcs;
			_tcs = null;
			IsOpen = false;

			AnimateOut();
			tcs.TrySetResult(result);
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

		private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			_value = e.NewValue;
			PercentText.Text = _value.ToString("P0");
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Left:
					Adjust(-GamepadStep);
					e.Handled = true;
					break;

				case Key.Right:
					Adjust(GamepadStep);
					e.Handled = true;
					break;

				case Key.Enter:
				case Key.Space:
					ConfirmSelected();
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
