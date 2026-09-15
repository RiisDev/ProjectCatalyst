using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views
{
	public partial class DownloadOverlay : OverlayControl
	{
		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public DownloadOverlay() => InitializeComponent();

		public void Open(string title = "Installing")
		{
			TitleTextBlock.Text = title;
			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();
		}

		public void Close()
		{
			IsOpen = false;
			AnimateOut(fadeMs: 180);
		}

		public void UpdateProgress(string fileName, double progress, int currentStep = 1, int totalSteps = 1, string? status = null)
		{
			progress = Math.Clamp(progress, 0.0, 1.0);

			FileNameText.Text = fileName;
			StepText.Text = totalSteps > 1 ? $"STEP {currentStep} OF {totalSteps}" : "DOWNLOADING";
			PercentText.Text = progress.ToString("P0");
			StatusText.Text = status ?? (progress >= 1.0 ? "Finishing up…" : "Downloading…");

			FillColumn.Width = new GridLength(progress, GridUnitType.Star);
			RemainderColumn.Width = new GridLength(1.0 - progress, GridUnitType.Star);
		}
	}
}
