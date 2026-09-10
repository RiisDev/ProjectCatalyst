using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ProjectCatalyst.Models;

namespace ProjectCatalyst.Views
{
	public partial class CatalystMessageBox
	{
		private readonly List<(Button Button, CatalystMessageBoxResult Result)> _buttons = [];
		private TaskCompletionSource<CatalystMessageBoxResult>? _tcs;
		private int _selectedIndex;

		public bool IsOpen { get; private set; }

		public CatalystMessageBox()
		{
			InitializeComponent();
			Visibility = Visibility.Collapsed;
		}

		public Task<CatalystMessageBoxResult> ShowAsync(string title, string message, CatalystMessageBoxButtons buttons = CatalystMessageBoxButtons.Ok, CatalystMessageBoxIcon icon = CatalystMessageBoxIcon.Info)
		{
			_tcs?.TrySetResult(CatalystMessageBoxResult.None);

			_tcs = new TaskCompletionSource<CatalystMessageBoxResult>();

			TitleTextBlock.Text = title;
			MessageTextBlock.Text = message;
			ApplyIcon(icon);
			BuildButtons(buttons);

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();

			return _tcs.Task;
		}

		private void ApplyIcon(CatalystMessageBoxIcon icon)
		{
			(string resourceKey, string label) = icon switch
			{
				CatalystMessageBoxIcon.Question => ("QuestionBrush", "QUESTION"),
				CatalystMessageBoxIcon.Warning => ("WarningBrush", "WARNING"),
				CatalystMessageBoxIcon.Error => ("ErrorBrush", "ERROR"),
				_ => ("AccentBrush", "INFO")
			};

			if (Application.Current?.Resources[resourceKey] is not Brush brush) return;
			IconDot.Fill = brush;
			IconLabel.Foreground = brush;
			IconLabel.Text = label;
		}

		private void BuildButtons(CatalystMessageBoxButtons buttons)
		{
			ButtonPanel.Children.Clear();
			_buttons.Clear();

			switch (buttons)
			{
				case CatalystMessageBoxButtons.Ok:
					AddButton("OK", CatalystMessageBoxResult.Ok);
					break;

				case CatalystMessageBoxButtons.OkCancel:
					AddButton("Cancel", CatalystMessageBoxResult.Cancel);
					AddButton("OK", CatalystMessageBoxResult.Ok);
					break;

				case CatalystMessageBoxButtons.YesNo:
					AddButton("No", CatalystMessageBoxResult.No);
					AddButton("Yes", CatalystMessageBoxResult.Yes);
					break;

				case CatalystMessageBoxButtons.YesNoCancel:
					AddButton("Cancel", CatalystMessageBoxResult.Cancel);
					AddButton("No", CatalystMessageBoxResult.No);
					AddButton("Yes", CatalystMessageBoxResult.Yes);
					break;
			}

			_selectedIndex = _buttons.Count - 1;
			FocusSelectedButton();
		}

		private void AddButton(string content, CatalystMessageBoxResult result)
		{
			if (Application.Current?.Resources["CatalystButton"] is not Style style) return;
			
			Button button = new()
			{
				Content = content,
				Style = style,
				Margin = new Thickness(10, 0, 0, 0),
				MinWidth = 110
			};
			button.Click += (_, _) => Complete(result);

			_buttons.Add((button, result));
			ButtonPanel.Children.Add(button);
		}

		private void FocusSelectedButton()
		{
			if (_buttons.Count == 0) return;
			Button button = _buttons[_selectedIndex].Button;
			button.Focus();
			Keyboard.Focus(button);
		}

		public void MoveSelection(int delta)
		{
			if (_buttons.Count == 0) return;
			_selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _buttons.Count - 1);
			FocusSelectedButton();
		}

		public void InvokeSelected()
		{
			if (_buttons.Count == 0) return;
			_buttons[_selectedIndex].Button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
		}

		public void RequestCancel()
		{
			(Button Button, CatalystMessageBoxResult Result) cancel = _buttons.FirstOrDefault(b => b.Result == CatalystMessageBoxResult.Cancel);
			if (cancel.Button != null)
			{
				Complete(CatalystMessageBoxResult.Cancel);
				return;
			}

			(Button Button, CatalystMessageBoxResult Result) no = _buttons.FirstOrDefault(b => b.Result == CatalystMessageBoxResult.No);
			if (no.Button != null)
			{
				Complete(CatalystMessageBoxResult.No);
			}
		}

		private void Complete(CatalystMessageBoxResult result)
		{
			if (_tcs is null) return;

			TaskCompletionSource<CatalystMessageBoxResult>? tcs = _tcs;
			_tcs = null;
			IsOpen = false;

			AnimateOut();
			tcs.TrySetResult(result);
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

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Left:
					MoveSelection(-1);
					e.Handled = true;
					break;

				case Key.Right:
					MoveSelection(1);
					e.Handled = true;
					break;

				case Key.Enter:
				case Key.Space:
					InvokeSelected();
					e.Handled = true;
					break;

				case Key.Escape:
					RequestCancel();
					e.Handled = true;
					break;
			}
		}
	}
}
