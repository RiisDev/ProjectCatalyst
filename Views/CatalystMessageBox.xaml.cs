using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ProjectCatalyst.Models;
using ProjectCatalyst.Views.Common;

namespace ProjectCatalyst.Views
{
	public partial class CatalystMessageBox : OverlayControl
	{
		private readonly List<(Button Button, CatalystMessageBoxResult Result)> _buttons = [];
		private readonly OverlayResult<CatalystMessageBoxResult> _result = new();
		private int _selectedIndex;

		protected override UIElement BackdropElement => Backdrop;
		protected override UIElement CardElement => Card;
		protected override ScaleTransform CardScaleTransform => CardScale;

		public CatalystMessageBox() => InitializeComponent();

		public Task<CatalystMessageBoxResult> ShowAsync(string title, string message, CatalystMessageBoxButtons buttons = CatalystMessageBoxButtons.Ok, CatalystMessageBoxIcon icon = CatalystMessageBoxIcon.Info)
		{
			Task<CatalystMessageBoxResult> task = _result.Begin(CatalystMessageBoxResult.None);

			TitleTextBlock.Text = title;
			MessageTextBlock.Text = message;
			ApplyIcon(icon);
			BuildButtons(buttons);

			IsOpen = true;
			Visibility = Visibility.Visible;
			AnimateIn();

			return task;
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
			if (!_result.TryComplete(result)) return;

			IsOpen = false;
			AnimateOut();
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
