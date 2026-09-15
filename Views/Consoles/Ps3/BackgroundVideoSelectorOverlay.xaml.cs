using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	public partial class BackgroundVideoSelectorOverlay : OverlayControl, ICloseableOverlay
	{
		private readonly record struct VideoOption(string Key, string DisplayName, string FilePath);

		private readonly List<VideoOption> _options = [];
		private readonly List<Border> _rowBorders = [];
		private readonly List<MediaElement> _thumbnails = [];
		private int _index;
		private readonly OverlayResult<string?> _result = new();

		private static readonly SolidColorBrush SelectedBorderBrush = new((Color)ColorConverter.ConvertFromString("#3ECBFF"));
		private static readonly SolidColorBrush SelectedFillBrush = new(Color.FromArgb(40, 0x3E, 0xCB, 0xFF));

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public BackgroundVideoSelectorOverlay() => InitializeComponent();

		public Task<string?> ShowAsync(IReadOnlyDictionary<string, string> variantFileNames, string videosFolder, string? currentKey)
		{
			Task<string?> task = _result.Begin();

			BuildRows(variantFileNames, videosFolder, currentKey);

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();
			FocusDefault();

			return task;
		}

		private void BuildRows(IReadOnlyDictionary<string, string> variantFileNames, string videosFolder, string? currentKey)
		{
			StopAllThumbnails();
			OptionsPanel.Children.Clear();
			_rowBorders.Clear();
			_thumbnails.Clear();
			_options.Clear();

			foreach (KeyValuePair<string, string> pair in variantFileNames)
			{
				_options.Add(new VideoOption(pair.Key, ToDisplayName(pair.Key), Path.Combine(videosFolder, pair.Value)));
			}

			foreach (VideoOption option in _options)
			{
				Border thumbFrame = new()
				{
					Width = 160,
					Height = 90,
					CornerRadius = new CornerRadius(6),
					ClipToBounds = true,
					Background = (Brush)FindResource("PanelBorderBrush")
				};

				MediaElement thumbnail = new()
				{
					Stretch = Stretch.UniformToFill,
					LoadedBehavior = MediaState.Manual,
					UnloadedBehavior = MediaState.Manual,
					Volume = 0,
					IsHitTestVisible = false
				};
				thumbnail.MediaEnded += (_, _) =>
				{
					thumbnail.Position = TimeSpan.Zero;
					thumbnail.Play();
				};

				try
				{
					if (File.Exists(option.FilePath))
					{
						thumbnail.Source = new Uri(option.FilePath, UriKind.Absolute);
						thumbnail.Play();
					}
				}
				catch (Exception)
				{
					// Missing/corrupt file - the frame just stays blank.
				}

				thumbFrame.Child = thumbnail;

				TextBlock nameText = new()
				{
					Text = option.DisplayName,
					Foreground = Brushes.White,
					FontFamily = new FontFamily("Segoe UI"),
					FontSize = 18,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(20, 0, 0, 0)
				};

				Grid rowGrid = new();
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				Grid.SetColumn(thumbFrame, 0);
				Grid.SetColumn(nameText, 1);
				rowGrid.Children.Add(thumbFrame);
				rowGrid.Children.Add(nameText);

				Border row = new()
				{
					CornerRadius = new CornerRadius(10),
					Padding = new Thickness(12),
					Margin = new Thickness(0, 0, 0, 8),
					BorderThickness = new Thickness(2),
					BorderBrush = Brushes.Transparent,
					Background = Brushes.Transparent,
					Child = rowGrid
				};

				OptionsPanel.Children.Add(row);
				_rowBorders.Add(row);
				_thumbnails.Add(thumbnail);
			}

			_index = Math.Max(0, _options.FindIndex(o => o.Key == currentKey));
			UpdateSelectionVisual();
		}

		private static string ToDisplayName(string key)
		{
			// "deep-blue" -> "Deep Blue"
			string[] words = key.Split('-', StringSplitOptions.RemoveEmptyEntries);
			return string.Join(" ", words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
		}

		private void UpdateSelectionVisual()
		{
			for (int i = 0; i < _rowBorders.Count; i++)
			{
				bool selected = i == _index;
				_rowBorders[i].BorderBrush = selected ? SelectedBorderBrush : Brushes.Transparent;
				_rowBorders[i].Background = selected ? SelectedFillBrush : Brushes.Transparent;
			}

			if (_index >= 0 && _index < _rowBorders.Count)
				_rowBorders[_index].BringIntoView();
		}

		public void MoveVertical(int delta)
		{
			if (_options.Count == 0) return;
			_index = Math.Clamp(_index + delta, 0, _options.Count - 1);
			UpdateSelectionVisual();
		}

		public void ActivateSelected()
		{
			if (_options.Count == 0)
			{
				Close();
				return;
			}

			Complete(_options[_index].Key);
		}

		public void Close() => Complete(null);

		private void StopAllThumbnails()
		{
			foreach (MediaElement thumbnail in _thumbnails)
			{
				thumbnail.Stop();
				thumbnail.Close();
			}
		}

		private void Complete(string? key)
		{
			if (!_result.TryComplete(key)) return;

			IsOpen = false;
			StopAllThumbnails();
			AnimateOut();
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Up:
					MoveVertical(-1);
					e.Handled = true;
					break;

				case Key.Down:
					MoveVertical(1);
					e.Handled = true;
					break;

				case Key.Enter:
				case Key.Space:
					ActivateSelected();
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
