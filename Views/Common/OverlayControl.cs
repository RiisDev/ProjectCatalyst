using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProjectCatalyst.Views.Common
{
	/// <summary>Implemented by overlays that a hosting view can dismiss generically (e.g. routing a
	/// Back/Escape press to whichever one is currently open) without knowing their specific type.</summary>
	public interface ICloseableOverlay
	{
		bool IsOpen { get; }
		void Close();
	}

	/// <summary>
	/// Shared chrome for the app's modal card overlays: every overlay fades/scales a "Card" in over a
	/// "Backdrop" and starts life collapsed. Concrete overlays point BackdropElement/CardElement/CardScaleTransform
	/// at their own x:Name="Backdrop"/"Card"/"CardScale" elements (kept as separate names so the generated
	/// partial-class fields don't collide with these properties).
	/// </summary>
	public abstract class OverlayControl : UserControl
	{
		protected abstract UIElement BackdropElement { get; }
		protected abstract UIElement CardElement { get; }
		protected abstract ScaleTransform CardScaleTransform { get; }

		public bool IsOpen { get; protected set; }

		protected OverlayControl() => Visibility = Visibility.Collapsed;

		protected virtual void AnimateIn(double cardStartScale = 0.94, int fadeMs = 200, int cardMs = 220)
		{
			BackdropElement.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeMs)));

			DoubleAnimation cardFade = new(0, 1, TimeSpan.FromMilliseconds(cardMs));
			DoubleAnimation cardScale = new(cardStartScale, 1.0, TimeSpan.FromMilliseconds(cardMs))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};

			CardElement.BeginAnimation(OpacityProperty, cardFade);
			CardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, cardScale);
			CardScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, cardScale);
		}

		protected virtual void AnimateOut(int fadeMs = 160)
		{
			DoubleAnimation fadeOut = new(1, 0, TimeSpan.FromMilliseconds(fadeMs));
			fadeOut.Completed += (_, _) => Visibility = Visibility.Collapsed;
			BackdropElement.BeginAnimation(OpacityProperty, fadeOut);
			CardElement.BeginAnimation(OpacityProperty, fadeOut);
		}

		public void FocusDefault()
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
			{
				Focus();
				Keyboard.Focus(this);
			}));
		}

		protected static void ShowValidationMessage(TextBlock target, string message)
		{
			target.Text = message;
			target.Visibility = Visibility.Visible;
		}

		protected static void FocusInput(UIElement element)
		{
			element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
			{
				element.Focus();
				Keyboard.Focus(element);
			}));
		}
	}

	public sealed class OverlayResult<T>
	{
		private TaskCompletionSource<T>? _tcs;

		public Task<T> Begin(T abandonedResult = default!)
		{
			_tcs?.TrySetResult(abandonedResult);
			_tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
			return _tcs.Task;
		}

		public bool TryComplete(T result)
		{
			if (_tcs is null) return false;

			TaskCompletionSource<T> tcs = _tcs;
			_tcs = null;
			tcs.TrySetResult(result);
			return true;
		}
	}
}
