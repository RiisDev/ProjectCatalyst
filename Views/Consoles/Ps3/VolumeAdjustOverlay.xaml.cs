using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCatalyst.Views.Common;

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
	public partial class VolumeAdjustOverlay : OverlayControl, ICloseableOverlay
	{
		private const double GamepadStep = 0.05;

		private double _value;
		private readonly OverlayResult<double?> _result = new();

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public VolumeAdjustOverlay() => InitializeComponent();

		/// <summary>Opens the overlay pre-set to currentValue (0.0-1.0). Returns the confirmed value, or null if cancelled.</summary>
		public Task<double?> ShowAsync(string title, double currentValue)
		{
			Task<double?> task = _result.Begin();

			TitleTextBlock.Text = title;
			_value = Math.Clamp(currentValue, 0, 1);
			UpdateDisplay();

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();
			FocusDefault();

			return task;
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
			if (!_result.TryComplete(result)) return;

			IsOpen = false;
			AnimateOut();
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
