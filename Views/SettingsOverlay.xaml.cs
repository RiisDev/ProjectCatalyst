using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ProjectCatalyst.Views
{
	public partial class SettingsOverlay
	{
		public event EventHandler? RequestClose;

		public event EventHandler? RequestExitApp;

		public event EventHandler<Color>? AccentColorChanged;

		private static readonly Color[] AccentPresets =
		[
			(Color)ColorConverter.ConvertFromString("#00C2FF"),
			(Color)ColorConverter.ConvertFromString("#7C5CFF"),
			(Color)ColorConverter.ConvertFromString("#FF5C7C"),
			(Color)ColorConverter.ConvertFromString("#5CFF9D"),
			(Color)ColorConverter.ConvertFromString("#FFB05C")
		];

		public SettingsOverlay()
		{
			InitializeComponent();
			BuildAccentSwatches();
		}

		private void BuildAccentSwatches()
		{
			foreach (Color color in AccentPresets)
			{
				Ellipse swatch = new()
				{
					Width = 40,
					Height = 40,
					Margin = new Thickness(0, 0, 12, 0),
					Fill = new SolidColorBrush(color),
					Stroke = Brushes.White,
					StrokeThickness = 0,
					Cursor = Cursors.Hand
				};
				swatch.MouseLeftButtonUp += (_, _) => AccentColorChanged?.Invoke(this, color);
				AccentSwatches.Items.Add(swatch);
			}
		}

		public void Open()
		{
			Visibility = Visibility.Visible;
			DoubleAnimation anim = new(520, 0, TimeSpan.FromMilliseconds(220))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			PanelTransform.BeginAnimation(TranslateTransform.XProperty, anim);
			Focus();
			Keyboard.Focus(this);
		}

		public void Close()
		{
			DoubleAnimation anim = new(0, 520, TimeSpan.FromMilliseconds(180))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
			};
			anim.Completed += (_, _) => Visibility = Visibility.Collapsed;
			PanelTransform.BeginAnimation(TranslateTransform.XProperty, anim);
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key is not (Key.Escape or Key.Back)) return;

			RequestClose?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}

		private void OnBackClick(object sender, RoutedEventArgs e) => RequestClose?.Invoke(this, EventArgs.Empty);

		private void OnExitAppClick(object sender, RoutedEventArgs e) => RequestExitApp?.Invoke(this, EventArgs.Empty);
	}
}
