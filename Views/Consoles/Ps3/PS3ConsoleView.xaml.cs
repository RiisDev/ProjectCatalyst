using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ProjectCatalyst.Models;
using ProjectCatalyst.Services;
using ProjectCatalyst.Views.Common;
using ProjectCatalyst.Views.Consoles.Internals;
using ProjectCatalyst.Wrappers;

namespace ProjectCatalyst.Views.Consoles.Ps3
{
	public partial class Ps3ConsoleView : IConsoleView
	{
		private const double ItemRowHeight = 70;
		private const double FocusOffsetFromTop = 24;
		private const string AudioFileName = "ps3-xmb-audio.m4a";

		private static readonly Dictionary<string, string> VideoVariantFileNames = new()
		{
			["black"] = "ps3-xmb-black.mp4",
			["brown"] = "ps3-xmb-brown.mp4",
			["deep-blue"] = "ps3-xmb-deep-blue.mp4",
			["green"] = "ps3-xmb-green.mp4",
			["orange"] = "ps3-xmb-orange.mp4",
			["purple"] = "ps3-xmb-purple.mp4",
			["red"] = "ps3-xmb-red.mp4",
			["turquoise"] = "ps3-xmb-turquoise.mp4"
		};

		private int _currentSelectedUserId;
		private string _videoVariant;

		private readonly MainWindow _mainWindow;

		private readonly EmulatorConfig _emulatorConfig;
		private readonly List<XmbCategory> _categories;
		private readonly DispatcherTimer _clockTimer;
		private readonly int[] _lastItemIndexPerCategory;
		private readonly ICloseableOverlay[] _backCloseableOverlays;
		private int _categoryIndex;
		private int _itemIndex;
		private double _scrollY;

		public ObservableCollection<XmbItem> CurrentItems { get; } = [];

		public event EventHandler? BackRequested;
		public readonly RPCS3 RPCS3;

		public Ps3ConsoleView(EmulatorConfig config, MainWindow main)
		{
			_emulatorConfig = config;
			_currentSelectedUserId = config.LastSelectedUserId ?? 1;
			_videoVariant = config.BackgroundVideoName ?? "deep-blue";

			_mainWindow = main;

			Log($"Starting RPCS3 wrapper with: {_emulatorConfig.ExecutablePath}");
			RPCS3 = new RPCS3(_emulatorConfig.ExecutablePath);

			Log("Building interface");
			InitializeComponent();

			_backCloseableOverlays = [ScreenshotViewer, FirmwareInstaller, CreateUserOverlay, BackgroundVideoSelector, VolumeAdjuster];

			Log("Building categories");
			_categories = BuildCategories();
			_lastItemIndexPerCategory = new int[_categories.Count];
			CategoryList.ItemsSource = _categories;
			ItemsListControl.ItemsSource = CurrentItems;

			Log("Setting active user ");
			UpdateActiveUserIndicator();

			Log("Starting clock timer");
			_clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_clockTimer.Tick += (_, _) => UpdateClock();
			_clockTimer.Start();
			UpdateClock();

			Unloaded += (_, _) => _clockTimer.Stop();
			Unloaded += (_, _) => StopBackgroundVideo();
			Unloaded += (_, _) => StopBackgroundAudio();

			Log("Starting media streams");
			StartBackgroundVideo();
			StartBackgroundAudio();

			BackgroundAudio.Volume = _emulatorConfig.AmbientVolume;
			InputAudio.Volume = _emulatorConfig.MovementVolume;

			KeyDown += OnKeyDown;

			ItemsViewport.SizeChanged += (_, _) => UpdateItemsScrollPosition(instant: true);

			SelectCategory(0);
		}

		private void UpdateActiveUserIndicator()
		{
			XmbCategory? usersCategory = _categories.FirstOrDefault(c => c.Name == "Users");
			if (usersCategory is null) return;

			XmbItem? activeUser = null;
			foreach (XmbItem item in usersCategory.Items)
			{
				bool isActive = item.Subtitle == _currentSelectedUserId.ToString();
				item.IsActiveUser = isActive;
				if (isActive) activeUser = item;
			}

			CurrentUserText.Text = activeUser?.Name ?? "Guest";
		}

		private void UpdateClock()
		{
			DateTime now = DateTime.Now;
			DateText.Text = now.ToString("D");
			ClockText.Text = now.ToString("h:mm:ss tt");
		}
		
		public string GetResource(string category, string name)
		{
			string path = Path.Combine(BaseDirectory, "Resources", "ps3", category, name);
			LogInfo($"Grabbing resource: {path}");
			return path;
		}

		private string GetUserImage(int userId)
		{
			int userPictureIndex = userId - 1;
			if (userPictureIndex > 26) userPictureIndex -= 26;
			return GetResource("UserIcons", userPictureIndex.ToString().PadLeft(3, '0') + ".png");
		}

		private List<XmbCategory> BuildCategories()
		{
			try
			{
				string executableName = Path.GetFileName(_emulatorConfig.ExecutablePath);

				switch (executableName)
				{
					case "rpcs3.exe":
						XmbCategory userCategory = new()
							{ Name = "Users", IconPath = GetResource("Icons", "menu_quickmenu.png") };
						XmbCategory gamesCategory = new()
							{ Name = "Games", IconPath = GetResource("Icons", "default.png") };
						XmbCategory screenshotsCategory = new()
							{ Name = "Pictures", IconPath = GetResource("Icons", "images.png") };
						XmbCategory settingsCategory = new()
							{ Name = "Settings", IconPath = GetResource("Icons", "settings.png") };
						foreach (RPCS3.Ps3User user in RPCS3.GetPlayers())
						{
							userCategory.Items.Add(new XmbItem
							{
								Name = user.Username,
								CoverLabel = user.Username[0].ToString().ToUpperInvariant(),
								CoverAccentColor = Color.FromRgb(0x5A, 0x6A, 0x8A),
								CoverImagePath = GetUserImage(user.UserId),
								Subtitle = user.UserId.ToString()
							});
						}

						userCategory.Items.Add(new XmbItem { Name = "Add User", CoverImagePath = GetResource("Icons", "add.png"), Subtitle = "SYS" });
						foreach (RPCS3.Ps3Game game in RPCS3.GetAllGames(_emulatorConfig.GamesDirectory))
						{
							gamesCategory.Items.Add(new XmbItem
							{
								Name = game.MetData.Title,
								Subtitle = $"{game.MetData.TitleId}",
								CoverImagePath = game.IconLocation,
								CoverAccentColor = Color.FromRgb(0x2E, 0x7D, 0x5A),
								IsGameItem = true
							});
						}

						foreach (string imagePath in RPCS3.GetScreenshots())
						{
							screenshotsCategory.Items.Add(new XmbItem
							{
								Name = Path.GetFileName(imagePath),
								Subtitle = File.GetCreationTime(imagePath).ToString("D"),
								CoverImagePath = imagePath,
								IsGameItem = true // Keep it true to make it full size
							});
						}

						settingsCategory.Items.AddRange([
							new XmbItem
							{
								Name = "Background Video",
								CoverImagePath = GetResource("Icons", "database.png")
							},
							new XmbItem
							{
								Name = "Reload Data",
								CoverImagePath = GetResource("Icons", "reload.png")
							},
							new XmbItem
							{
								Name = "Install Firmware",
								CoverImagePath = GetResource("Icons", "core-disk-options.png")
							},
							new XmbItem
							{
								Name = "Ambient Volume",
								CoverImagePath = GetResource("Icons", "database.png")
							},
							new XmbItem
							{
								Name = "Movement Volume",
								CoverImagePath = GetResource("Icons", "database.png")
							}
						]);

						return [userCategory, gamesCategory, screenshotsCategory, settingsCategory];
				}
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
				Environment.Exit(1);
			}

			throw new InvalidOperationException("Invalid emu setup");
		}

		private void SelectCategory(int index)
		{
			if (_categories.Count == 0) return;
			index = Math.Clamp(index, 0, _categories.Count - 1);
			if (index == _categoryIndex && _categories[index].IsSelected) return;

			if (CurrentItems.Count > 0 && _itemIndex >= 0 && _itemIndex < CurrentItems.Count)
			{
				CurrentItems[_itemIndex].IsSelected = false;
			}
			_lastItemIndexPerCategory[_categoryIndex] = _itemIndex;

			_categories[_categoryIndex].IsSelected = false;
			_categoryIndex = index;
			_categories[_categoryIndex].IsSelected = true;

			RefreshItemsForCurrentCategory();
		}
		
		private void RefreshItemsForCurrentCategory()
		{
			CurrentItems.Clear();

			foreach (XmbItem item in _categories[_categoryIndex].Items) 
				CurrentItems.Add(item);

			_itemIndex = CurrentItems.Count == 0 ? 0 : Math.Clamp(_lastItemIndexPerCategory[_categoryIndex], 0, CurrentItems.Count - 1);

			if (CurrentItems.Count > 0) 
				CurrentItems[_itemIndex].IsSelected = true;

			_scrollY = 0;
			UpdateItemsScrollPosition(instant: true);
		}

		private void SelectItem(int index)
		{
			if (CurrentItems.Count == 0) return;
			index = Math.Clamp(index, 0, CurrentItems.Count - 1);

			if (_itemIndex >= 0 && _itemIndex < CurrentItems.Count) 
				CurrentItems[_itemIndex].IsSelected = false;

			_itemIndex = index;
			CurrentItems[_itemIndex].IsSelected = true;

			UpdateItemsScrollPosition();
		}

		private void UpdateItemsScrollPosition(bool instant = false)
		{
			double itemTop = _itemIndex * ItemRowHeight;
			double availableHeight = Math.Max(0, ItemsViewport.ActualHeight - FocusOffsetFromTop);

			double screenTop = itemTop + _scrollY;
			double screenBottom = screenTop + ItemRowHeight;

			if (screenTop < 0)
				_scrollY -= screenTop;
			else if (availableHeight > 0 && screenBottom > availableHeight) 
				_scrollY -= screenBottom - availableHeight;

			_scrollY = Math.Min(_scrollY, 0);

			double targetY = FocusOffsetFromTop + _scrollY;

			if (instant)
			{
				ItemsListTransform.BeginAnimation(TranslateTransform.YProperty, null);
				ItemsListTransform.Y = targetY;
				return;
			}

			DoubleAnimation animation = new(targetY, TimeSpan.FromMilliseconds(260))
			{
				EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut, Power = 3 }
			};

			ItemsListTransform.BeginAnimation(TranslateTransform.YProperty, animation);
		}
		
		public void MoveHorizontal(int delta)
		{
			if (ScreenshotViewer.IsOpen)
			{
				ScreenshotViewer.MoveHorizontal(delta);
				return;
			}

			if (BackgroundVideoSelector.IsOpen) return; // vertical list - Left/Right don't apply

			if (VolumeAdjuster.IsOpen)
			{
				VolumeAdjuster.Adjust(delta * 0.05);
				return;
			}

			SelectCategory(_categoryIndex + delta);
		}

		public void MoveVertical(int delta)
		{
			if (ScreenshotViewer.IsOpen) return; // nothing to browse vertically in the photo viewer

			if (BackgroundVideoSelector.IsOpen)
			{
				BackgroundVideoSelector.MoveVertical(delta);
				return;
			}

			if (VolumeAdjuster.IsOpen) return; // a slider - Up/Down don't apply

			SelectItem(_itemIndex + delta);
		}

		public void ReloadCategories()
		{
			List<XmbCategory> categoryData = BuildCategories();

			foreach (XmbCategory category in _categories)
			{
				category.Items.Clear();
				category.Items.AddRange(categoryData.First(x => x.Name == category.Name).Items);
			}

			RefreshItemsForCurrentCategory();
		}

		private enum XmbCategoryState
		{
			Users = 0,
			Games = 1,
			Pictures = 2,
			System = 3
		}

		private enum XmbSubCategoryState
		{
			ChangeVideo = 0,
			ReloadData = 1,
			InstallFirmware = 2,
			ChangeAmbientVolume = 3,
			ChangeMovementVolume = 4
		}

		public async void ActivateSelected()
		{
			Log("Activating selection");
			if (ScreenshotViewer.IsOpen) return;
			if (_mainWindow.CatalystMessageBoxControl.IsOpen) return;

			if (BackgroundVideoSelector.IsOpen)
			{
				BackgroundVideoSelector.ActivateSelected();
				return;
			}

			if (VolumeAdjuster.IsOpen)
			{
				VolumeAdjuster.ConfirmSelected();
				return;
			}

			if (CurrentItems.Count == 0) return;

			XmbItem? currentItem = CurrentItems.FirstOrDefault(x => x.IsSelected);
			if (currentItem is null) return;

			switch ((XmbCategoryState)_categoryIndex)
			{
				case XmbCategoryState.Users:
					await ActivateUserAsync(currentItem);
					break;
				case XmbCategoryState.Games:
					await ActivateGameAsync(currentItem);
					break;
				case XmbCategoryState.Pictures:
					ActivatePicture(currentItem);
					break;
				case XmbCategoryState.System:
					await ActivateSystemItemAsync();
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			FocusDefault();
		}

		private async Task ActivateUserAsync(XmbItem currentItem)
		{
			if (currentItem.Subtitle == "SYS") await CreateUserOverlay.ShowAsync();
			else if (!int.TryParse(currentItem.Subtitle, out _currentSelectedUserId))
			{
				CatalystMessageBoxResult result = await _mainWindow.CatalystMessageBoxControl.ShowAsync("ERROR", "Failed to parse user_id, please contact support.", icon: CatalystMessageBoxIcon.Error);
				_ = result;
			}
			else
			{
				UpdateActiveUserIndicator();
				_emulatorConfig.LastSelectedUserId = _currentSelectedUserId;
				PersistConfig();
			}
		}

		private async Task ActivateGameAsync(XmbItem currentItem)
		{
			try
			{
				(bool valid, RPCS3.RPS3FailedReason reason) = RPCS3.ValidateInstall();
				if (!valid)
				{
					LogError(reason.ToString());
					await _mainWindow.CatalystMessageBoxControl.ShowAsync(
						"ERROR", $"Failed to validate install: {reason}", icon: CatalystMessageBoxIcon.Error);
					return;
				}

				if (!RPCS3.IsFirmwareInstalled())
				{
					LogError("Missing required firmware");
					await _mainWindow.CatalystMessageBoxControl.ShowAsync(
						"ERROR", "RPCS3 does not have any valid firmware installed, please go to the settings tab.",
						icon: CatalystMessageBoxIcon.Error);
					return;
				}

				_ = LaunchGameAndWaitAsync(currentItem);
			}
			catch (Exception ex)
			{
				await _mainWindow.CatalystMessageBoxControl.ShowAsync("ERROR", ex.ToString(), icon: CatalystMessageBoxIcon.Error);
			}
		}

		private async Task LaunchGameAndWaitAsync(XmbItem currentItem)
		{
			Task launchTask = RPCS3.LaunchGameAsUser(
				RPCS3.GetAllGames().First(x => x.MetData.TitleId == currentItem.Subtitle),
				_currentSelectedUserId);

			await Task.Delay(2000);

			StopBackgroundAudio();
			StopBackgroundVideo();
			_mainWindow.Hide();

			await Task.WhenAny(WaitForExitConnection(), launchTask);

			StartBackgroundAudio();
			StartBackgroundVideo();
			_mainWindow.Show();
			FocusDefault();
		}

		private async Task WaitForExitConnection()
		{
			string path = Path.Combine(Path.GetDirectoryName(_emulatorConfig.ExecutablePath)!, "log", "RPCS3.log");
			while (true)
			{
				try
				{
					string text = await RPCS3.ReadLogTextAsync(path);
					if (text.Contains("SYS: Requesting game to exit") ) { break; }
				}
				catch { /**/ }
				await Task.Delay(250);
			}
		}

		private async Task ActivateSystemItemAsync()
		{
			switch ((XmbSubCategoryState)_itemIndex)
			{
				case XmbSubCategoryState.ChangeVideo:
					string videosFolder = Path.Combine(BaseDirectory, "Resources", "ps3", "Backgrounds");
					string? chosenVariant = await BackgroundVideoSelector.ShowAsync(VideoVariantFileNames, videosFolder, _videoVariant);
					if (chosenVariant is null) break; // cancelled

					_videoVariant = chosenVariant;
					StopBackgroundVideo();
					StartBackgroundVideo();

					_emulatorConfig.BackgroundVideoName = _videoVariant;
					PersistConfig();
					break;

				case XmbSubCategoryState.ReloadData:
					ReloadCategories();
					break;

				case XmbSubCategoryState.ChangeAmbientVolume:
					double? ambientResult = await VolumeAdjuster.ShowAsync("Ambient Volume", BackgroundAudio.Volume);
					if (ambientResult is null) break; // cancelled

					BackgroundAudio.Volume = ambientResult.Value;
					_emulatorConfig.AmbientVolume = ambientResult.Value;
					PersistConfig();
					break;

				case XmbSubCategoryState.ChangeMovementVolume:
					double? movementResult = await VolumeAdjuster.ShowAsync("Movement Volume", InputAudio.Volume);
					if (movementResult is null) break; // cancelled

					InputAudio.Volume = movementResult.Value;
					_emulatorConfig.MovementVolume = movementResult.Value;
					PersistConfig();
					break;

				case XmbSubCategoryState.InstallFirmware:
					CatalystMessageBoxResult confirm = await _mainWindow.CatalystMessageBoxControl.ShowAsync("Install Firmware", "Installing a firmware requires keyboard input, do you wish to proceed.", icon: CatalystMessageBoxIcon.Question, buttons: CatalystMessageBoxButtons.YesNo);
					if (confirm != CatalystMessageBoxResult.Yes) return;

					string? firmwareLocation = await FirmwareInstaller.ShowAsync();
					if (!File.Exists(firmwareLocation)) return;

					confirm = await _mainWindow.CatalystMessageBoxControl.ShowAsync("Install Firmware", $"Install firmware from {Path.GetFileName(firmwareLocation)}?", icon: CatalystMessageBoxIcon.Question, buttons: CatalystMessageBoxButtons.YesNo);
					if (confirm != CatalystMessageBoxResult.Yes) return;

					DownloadOverlay.Open();
					await Task.Delay(500);
					DownloadOverlay.UpdateProgress(Path.GetFileName(firmwareLocation), .5);

					if (RPCS3.IsFirmwareInstalled())
					{
						confirm = await _mainWindow.CatalystMessageBoxControl.ShowAsync("Install Firmware", $"RPCS3 has detected an existing firmware, do you wish to proceed?", icon: CatalystMessageBoxIcon.Question, buttons: CatalystMessageBoxButtons.YesNo);

						if (confirm == CatalystMessageBoxResult.Yes)
							await RPCS3.InstallFirmwareAsync(firmwareLocation, true);
					}

					DownloadOverlay.UpdateProgress(Path.GetFileName(firmwareLocation), 1.0, status: "Complete");
					await Task.Delay(500);
					DownloadOverlay.Close();
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void ActivatePicture(XmbItem currentItem)
		{
			List<XmbItem> picturesWithImages = CurrentItems.Where(i => !string.IsNullOrEmpty(i.CoverImagePath)).ToList();
			int startIndex = picturesWithImages.IndexOf(currentItem);
			if (startIndex < 0) return;

			ScreenshotViewer.Show(picturesWithImages.Select(i => i.CoverImagePath!).ToList(), startIndex);
		}

		public bool TryHandleBack()
		{
			ICloseableOverlay? openOverlay = Array.Find(_backCloseableOverlays, o => o.IsOpen);
			if (openOverlay is not null)
			{
				openOverlay.Close();
				return true;
			}

			if (_mainWindow.CatalystMessageBoxControl.IsOpen)
			{
				_mainWindow.CatalystMessageBoxControl.RequestCancel();
				return true;
			}

			return false;
		}

		public void FocusDefault()
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
			{
				Focus();
				Keyboard.Focus(this);
			}));
		}

		/// <summary>
		/// Saves _emulatorConfig's current field values back to
		/// %AppData%\ProjectCatalyst\emulators.json, replacing whichever
		/// entry matches its Id. Call this any time a setting on
		/// _emulatorConfig changes (background video, volumes, last user).
		/// </summary>
		private void PersistConfig()
		{
			List<EmulatorConfig> configs = EmulatorConfigStore.Load();
			int index = configs.FindIndex(c => c.Id == _emulatorConfig.Id);

			if (index >= 0) configs[index] = _emulatorConfig;
			else configs.Add(_emulatorConfig);

			EmulatorConfigStore.Save(configs);
		}

		private void StartBackgroundVideo()
		{
			try
			{
				if (!VideoVariantFileNames.TryGetValue(_videoVariant, out string? fileName))
				{
					LogError("Unknown video variant.");
					return;
				}

				string videoPath = Path.Combine(BaseDirectory, "Resources", "ps3", "Backgrounds", fileName);
				
				BackgroundVideo.Source = new Uri(videoPath, UriKind.Absolute);
				BackgroundVideo.Play();
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				BackgroundVideo.Visibility = Visibility.Collapsed;
			}
		}

		private void StopBackgroundVideo()
		{
			BackgroundVideo.Stop();
			BackgroundVideo.Close();
		}

		private void OnBackgroundVideoEnded(object sender, RoutedEventArgs e)
		{
			BackgroundVideo.Position = TimeSpan.Zero;
			BackgroundVideo.Play();
		}

		private void OnBackgroundVideoFailed(object sender, ExceptionRoutedEventArgs e)
		{
			BackgroundVideo.Visibility = Visibility.Collapsed;
		}

		private void StartBackgroundAudio()
		{
			try
			{
				string audioPath = Path.Combine(BaseDirectory, "Resources", "ps3", "Sounds", AudioFileName);

				if (!File.Exists(audioPath))
				{
					LogError($"Missing audio file: {audioPath}");
					return;
				}
				
				BackgroundAudio.Source = new Uri(audioPath, UriKind.Absolute);
				BackgroundAudio.Play();
			}
			catch (Exception) { /**/ }
		}

		private void StopBackgroundAudio()
		{
			BackgroundAudio.Stop();
			BackgroundAudio.Close();
		}

		private void OnBackgroundAudioEnded(object sender, RoutedEventArgs e)
		{
			BackgroundAudio.Position = TimeSpan.Zero;
			BackgroundAudio.Play();
		}

		private void OnBackgroundAudioFailed(object sender, ExceptionRoutedEventArgs e) => Debug.WriteLine($"Failed to load audio: {e.ErrorException}");


		public void PlayDirectionalAudio(Key key = default, GamepadButton gKey = default)
		{
			if (gKey != default && gKey != GamepadButton.None)
			{
				InputAudio.Source = gKey switch
				{
					GamepadButton.DPadLeft => new Uri(GetResource("Sounds", "cancel.wav"), UriKind.Absolute),
					GamepadButton.DPadRight => new Uri(GetResource("Sounds", "cancel.wav"), UriKind.Absolute),
					GamepadButton.DPadUp => new Uri(GetResource("Sounds", "up.wav"), UriKind.Absolute),
					GamepadButton.DPadDown => new Uri(GetResource("Sounds", "down.wav"), UriKind.Absolute),
					GamepadButton.Accept => new Uri(GetResource("Sounds", "ok.wav"), UriKind.Absolute),
					GamepadButton.Back => new Uri(GetResource("Sounds", "cancel.wav"), UriKind.Absolute),
					_ => InputAudio.Source
				};
			}
			else if (key != default && key != Key.None)
			{
				InputAudio.Source = key switch
				{
					Key.Left or Key.Right => new Uri(GetResource("Sounds", "cancel.wav"), UriKind.Absolute),
					Key.Up => new Uri(GetResource("Sounds", "up.wav"), UriKind.Absolute),
					Key.Down => new Uri(GetResource("Sounds", "down.wav"), UriKind.Absolute),
					Key.Enter or Key.Space => new Uri(GetResource("Sounds", "ok.wav"), UriKind.Absolute),
					Key.Escape => new Uri(GetResource("Sounds", "cancel.wav"), UriKind.Absolute),
					_ => InputAudio.Source
				};
			}
			else throw new InvalidOperationException($"Somehow directional was called {key} | {gKey}");

			InputAudio.Play();
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Left:
					MoveHorizontal(-1);
					e.Handled = true;
					break;

				case Key.Right:
					MoveHorizontal(1);
					e.Handled = true;
					break;

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
					if (!TryHandleBack())
					{
						BackRequested?.Invoke(this, EventArgs.Empty);
					}
					e.Handled = true;
					break;
			}

			if (e.Handled)
				PlayDirectionalAudio(e.Key);
		}
	}
}
