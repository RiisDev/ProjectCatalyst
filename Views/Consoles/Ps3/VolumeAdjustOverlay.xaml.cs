using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	public partial class VolumeAdjustOverlay : OverlayControl, ICloseableOverlay
	{
		private const double GamepadStep = 0.05;

		private double _value;
		private readonly OverlayResult<double?> _result = new();

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public VolumeAdjustOverlay() => InitializeComponent();

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
