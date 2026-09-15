using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectCatalyst.Models;
using ProjectCatalyst.Services;
using ProjectCatalyst.Views.Consoles.Internals;
using ProjectCatalyst.Views.Consoles.Ps3;

namespace ProjectCatalyst
{
	public partial class MainWindow
	{
		public ObservableCollection<PlatformItem> Platforms { get; } = [];

		private readonly GamepadService _gamepad = new();
		private readonly DispatcherTimer _clockTimer;

		private bool _isConsoleViewOpen;
		public IConsoleView? ActiveConsoleView;

		public MainWindow()
		{
			Log("Building MainWindow");
			InitializeComponent();
			DataContext = this;

			Log("Loading platforms");
			try { LoadPlatforms();} catch (Exception ex) { LogError(ex.ToString()); }

			Log("Starting clock timers");
			_clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("dddd, HH:mm");
			_clockTimer.Start();
			ClockText.Text = DateTime.Now.ToString("dddd, HH:mm");

			Log("Registering gamepad event handler");
			_gamepad.ButtonPressed += OnGamepadButtonPressed;
		}

		private void LoadPlatforms() => RebuildPlatformTiles(EmulatorConfigStore.Load());

		private void RebuildPlatformTiles(List<EmulatorConfig> configs)
		{
			Platforms.Clear();

			foreach (EmulatorConfig config in configs) 
				Platforms.Add(BuildPlatformItem(config));

			Platforms.Add(new PlatformItem
			{
				Name = "Add Emulator",
				Glyph = "+",
				AccentColor = (Color)ColorConverter.ConvertFromString("#8792A6"),
				IsAddEmulatorTile = true
			});
		}

		private static readonly Dictionary<SystemType, Func<EmulatorConfig, MainWindow, UserControl>> ConsoleViewFactories = new()
		{
			[SystemType.Ps3] = (config, main) => new Ps3ConsoleView(config, main)
		};

		private PlatformItem BuildPlatformItem(EmulatorConfig config)
		{
			SystemTypePresentation presentation = config.SystemType.GetPresentation();
			Func<EmulatorConfig, MainWindow, UserControl>? factory = ConsoleViewFactories.GetValueOrDefault(config.SystemType);

			return new PlatformItem
			{
				Name = presentation.DisplayName,
				Subtitle = config.EmulatorName,
				Glyph = presentation.Glyph,
				AccentColor = presentation.AccentColor,
				ConsoleViewFactory = factory is null ? null : () => factory(config, this),
				ExecutablePath = config.ExecutablePath,
				WorkingDirectory = Path.GetDirectoryName(config.ExecutablePath)
			};
		}

		private void OnWindowLoaded(object sender, RoutedEventArgs e)
		{
			if (Platforms.Count > 0) 
				PlatformSelector.SelectedIndex = 0;

			PlatformSelector.Focus();
			Keyboard.Focus(PlatformSelector);
			_gamepad.Start();
		}

		private void OnPlatformSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PlatformSelector.SelectedItem is not PlatformItem item) return;

			SelectedNameText.Text = item.Name;
			SelectedSubtitleText.Text = item.Subtitle;

			PlatformSelector.ScrollIntoView(item);
		}

		private void OnPlatformSelectorPreviewKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Enter:
				case Key.Space:
					_ = LaunchSelectedAsync();
					e.Handled = true;
					break;

				case Key.Up:
				case Key.Down:
					e.Handled = true;
					break;
			}
		}

		private void OnWindowKeyDown(object sender, KeyEventArgs e)
		{
			if (CatalystMessageBoxControl.IsOpen || EmulatorSetupControl.IsOpen )
				return;

			if (ActiveConsoleView?.TryHandleBack() ?? false)
				return;

			switch (e.Key)
			{
				case Key.Escape:
					Application.Current.Shutdown();
					break;
			}
		}

		private async Task LaunchSelectedAsync()
		{
			Log("Attempt launch sequence");
			try
			{
				if (PlatformSelector.SelectedItem is not PlatformItem item) return;

				LogInfo($"{item.Name} -> IsAddEmulatorTile = {item.IsAddEmulatorTile}");
				LogInfo($"{item.Name} -> ConsoleViewFactory is null = {item.ConsoleViewFactory is null}");

				if (item.IsAddEmulatorTile)
				{
					await OpenEmulatorSetupAsync();
					return;
				}

				if (item.ConsoleViewFactory is not null)
				{
					NavigateToConsole(item);
					return;
				}

				if (string.IsNullOrWhiteSpace(item.ExecutablePath))
				{
					await CatalystMessageBoxControl.ShowAsync(
						"Nothing to Launch",
						$"\"{item.Name}\" isn't fully configured yet. Try removing and re-adding it from the \"+\" tile.");

				}
			}
			catch (Exception ex)
			{
				LogError($"Launch sequence failed: {ex}");
			}
		}

		private async Task OpenEmulatorSetupAsync()
		{
			LogInfo("Launching emulator setup");
			try
			{
				EmulatorConfig? config = await EmulatorSetupControl.ShowAsync();

				PlatformSelector.Focus();
				Keyboard.Focus(PlatformSelector);

				if (config is null) return;

				List<EmulatorConfig> configs = EmulatorConfigStore.Load();
				configs.Add(config);
				EmulatorConfigStore.Save(configs);

				RebuildPlatformTiles(configs);

				int newIndex = configs.Count - 1;
				if (newIndex >= 0 && newIndex < Platforms.Count)
					PlatformSelector.SelectedIndex = newIndex;
			}
			catch (Exception ex)
			{
				LogError($"Emulator setup failed: {ex}");
			}
		}

		private async void OnClearConfigClick(object sender, RoutedEventArgs e)
		{
			CatalystMessageBoxResult first = await CatalystMessageBoxControl.ShowAsync(
				"Clear Config?",
				"This removes every emulator you've configured in Project Catalyst. Your emulator installs and games themselves aren't touched.",
				CatalystMessageBoxButtons.YesNo,
				CatalystMessageBoxIcon.Warning);

			if (first != CatalystMessageBoxResult.Yes) return;

			await Task.Delay(250);

			CatalystMessageBoxResult second = await CatalystMessageBoxControl.ShowAsync(
				"Are You Absolutely Sure?",
				"This cannot be undone. Every configured emulator will be removed from Project Catalyst.",
				CatalystMessageBoxButtons.YesNo,
				CatalystMessageBoxIcon.Error);

			if (second != CatalystMessageBoxResult.Yes) return;

			ClearConfig();
		}

		private void ClearConfig()
		{
			try
			{
				EmulatorConfigStore.Save([]);
				RebuildPlatformTiles([]);
				PlatformSelector.SelectedIndex = Platforms.Count > 0 ? 0 : -1;
			}
			catch (Exception ex)
			{
				LogError($"Failed to clear config: {ex}");
			}
		}

		private void NavigateToConsole(PlatformItem item)
		{
			LogInfo("Navigating to console view");
			try
			{
				if (item.ConsoleViewFactory is null) return;

				UserControl view = item.ConsoleViewFactory();
				ConsoleHost.Content = view;
				ConsoleHost.Visibility = Visibility.Visible;
				LauncherRoot.Visibility = Visibility.Collapsed;
				_isConsoleViewOpen = true;

				if (view is not IConsoleView consoleView) return;

				ActiveConsoleView = consoleView;
				consoleView.BackRequested += OnConsoleViewBackRequested;
				consoleView.FocusDefault();
			}
			catch (Exception ex)
			{
				LogError($"Failed to launch UI: {ex}");
			}
		}

		private void OnConsoleViewBackRequested(object? sender, EventArgs e) => ReturnToLauncher();

		private void ReturnToLauncher()
		{
			Log("Returning to main menu");
			if (ActiveConsoleView is not null)
			{
				ActiveConsoleView.BackRequested -= OnConsoleViewBackRequested;
				ActiveConsoleView = null;
			}

			ConsoleHost.Visibility = Visibility.Collapsed;
			ConsoleHost.Content = null;
			LauncherRoot.Visibility = Visibility.Visible;
			_isConsoleViewOpen = false;

			PlatformSelector.Focus();
			Keyboard.Focus(PlatformSelector);
		}

		private async void OnTestDownloadClick(object sender, RoutedEventArgs e)
		{
			(string FileName, int Step, int TotalSteps)[] steps =
			[
				("BaseGame.pkg", 1, 2),
				("Update_1.52.pkg", 2, 2)
			];

			DownloadOverlayControl.Open();

			foreach ((string fileName, int step, int totalSteps) in steps)
			{
				for (double progress = 0; progress < 1.0; progress += 0.04)
				{
					DownloadOverlayControl.UpdateProgress(fileName, progress, step, totalSteps);
					await Task.Delay(60);
				}

				DownloadOverlayControl.UpdateProgress(fileName, 1.0, step, totalSteps, status: "Complete");
				await Task.Delay(300);
			}

			await Task.Delay(300);
			DownloadOverlayControl.Close();
		}

		private async void OnTestAlertClick(object sender, RoutedEventArgs e)
		{
			CatalystMessageBoxResult result = await CatalystMessageBoxControl.ShowAsync(
				"Delete Save Data?",
				"This will permanently remove your saved progress for this title. This can't be undone.",
				CatalystMessageBoxButtons.YesNo,
				CatalystMessageBoxIcon.Warning);

			_ = result;
		}

		private void OnGamepadButtonPressed(object? sender, GamepadButton button)
		{
			try
			{
				if (EmulatorSetupControl.IsOpen)
					return;

				if (CatalystMessageBoxControl.IsOpen)
				{
					ActiveConsoleView?.PlayDirectionalAudio(default, button);
					switch (button)
					{
						case GamepadButton.DPadLeft:
							CatalystMessageBoxControl.MoveSelection(-1);
							break;
						case GamepadButton.DPadRight:
							CatalystMessageBoxControl.MoveSelection(1);
							break;
						case GamepadButton.Accept:
							CatalystMessageBoxControl.InvokeSelected();
							break;
						case GamepadButton.Back:
							CatalystMessageBoxControl.RequestCancel();
							break;
					}

					return;
				}
				
				if (_isConsoleViewOpen && ActiveConsoleView is not null)
				{
					ActiveConsoleView.PlayDirectionalAudio(default, button);

					switch (button)
					{
						case GamepadButton.DPadLeft:
							ActiveConsoleView.MoveHorizontal(-1);
							break;
						case GamepadButton.DPadRight:
							ActiveConsoleView.MoveHorizontal(1);
							break;
						case GamepadButton.DPadUp:
							ActiveConsoleView.MoveVertical(-1);
							break;
						case GamepadButton.DPadDown:
							ActiveConsoleView.MoveVertical(1);
							break;
						case GamepadButton.Accept:
							ActiveConsoleView.ActivateSelected();
							break;
						case GamepadButton.Back:
							if (!ActiveConsoleView.TryHandleBack())
							{
								ReturnToLauncher();
							}

							break;
					}

					return;
				}

				switch (button)
				{
					case GamepadButton.DPadLeft:
						MoveSelection(-1);
						break;
					case GamepadButton.DPadRight:
						MoveSelection(1);
						break;
					case GamepadButton.Accept:
						_ = LaunchSelectedAsync();
						break;
				}
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}
		}

		private void MoveSelection(int delta)
		{
			if (Platforms.Count == 0) return;

			int next = PlatformSelector.SelectedIndex + delta;
			next = Math.Clamp(next, 0, Platforms.Count - 1);
			PlatformSelector.SelectedIndex = next;
		}

		protected override void OnClosed(EventArgs e)
		{
			_gamepad.Dispose();
			_clockTimer.Stop();
			base.OnClosed(e);
		}
	}
}
