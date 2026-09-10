using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ProjectCatalyst.Views
{
	public partial class DownloadOverlay
	{
		public DownloadOverlay()
		{
			InitializeComponent();
			Visibility = Visibility.Collapsed;
		}

		public void Open(string title = "Installing")
		{
			TitleTextBlock.Text = title;
			Visibility = Visibility.Visible;

			DoubleAnimation fade = new(0, 1, TimeSpan.FromMilliseconds(200));
			Backdrop.BeginAnimation(OpacityProperty, fade);

			DoubleAnimation cardFade = new(0, 1, TimeSpan.FromMilliseconds(220));
			DoubleAnimation cardScale = new(0.94, 1.0, TimeSpan.FromMilliseconds(220))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			Card.BeginAnimation(OpacityProperty, cardFade);
			CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, cardScale);
			CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, cardScale);
		}

		public void Close()
		{
			DoubleAnimation fadeOut = new(1, 0, TimeSpan.FromMilliseconds(180));
			fadeOut.Completed += (_, _) => Visibility = Visibility.Collapsed;
			Backdrop.BeginAnimation(OpacityProperty, fadeOut);
			Card.BeginAnimation(OpacityProperty, fadeOut);
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
