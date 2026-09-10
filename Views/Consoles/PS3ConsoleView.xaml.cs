using ProjectCatalyst.Models;
using ProjectCatalyst.Services;
using ProjectCatalyst.Views.Consoles.Internals;
using ProjectCatalyst.Wrappers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProjectCatalyst.Views.Consoles
{
	public partial class Ps3ConsoleView : IConsoleView
	{
		private const double ItemRowHeight = 70;
		private const double FocusOffsetFromTop = 24;

		private int _currentSelectedUserId = 1;

		private readonly EmulatorConfig _emulatorConfig;
		private readonly List<XmbCategory> _categories;
		private readonly DispatcherTimer _clockTimer;
		private readonly int[] _lastItemIndexPerCategory;
		private int _categoryIndex;
		private int _itemIndex;
		private double _scrollY;

		public ObservableCollection<XmbItem> CurrentItems { get; } = [];

		public event EventHandler? BackRequested;
		private readonly RPCS3 _rpcs3;
		public Ps3ConsoleView(EmulatorConfig config)
		{
			_emulatorConfig = config;

			_rpcs3 = new RPCS3(Path.GetDirectoryName(_emulatorConfig.ExecutablePath)!);

			InitializeComponent();
			
			_categories = BuildCategories();
			_lastItemIndexPerCategory = new int[_categories.Count];
			CategoryList.ItemsSource = _categories;
			ItemsListControl.ItemsSource = CurrentItems;

			UpdateActiveUserIndicator();

			_clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_clockTimer.Tick += (_, _) => UpdateClock();
			_clockTimer.Start();
			UpdateClock();

			Unloaded += (_, _) => _clockTimer.Stop();
			Unloaded += (_, _) => StopBackgroundVideo();
			Unloaded += (_, _) => StopBackgroundAudio();

			StartBackgroundVideo();
			StartBackgroundAudio();

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
			return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ps3", category, name);
		}

		private string GetUserImage(int userId)
		{
			int userPictureIndex = userId - 1;
			if (userPictureIndex > 26) userPictureIndex -= 26;
			return GetResource("UserIcons", userPictureIndex.ToString().PadLeft(3, '0') + ".png");
		}

		private List<XmbCategory> BuildCategories()
		{
			
			string executableName = Path.GetFileName(_emulatorConfig.ExecutablePath);

			switch (executableName)
			{
				case "rpcs3.exe":
					XmbCategory userCategory = new() { Name = "Users", IconPath = GetResource("Icons", "menu_quickmenu.png") };
					XmbCategory gamesCategory = new() { Name = "Games", IconPath = GetResource("Icons", "default.png") };
					XmbCategory screenshotsCategory = new() { Name = "Pictures", IconPath = GetResource("Icons", "images.png") };
					XmbCategory settingsCategory = new() { Name = "Settings", IconPath = GetResource("Icons", "settings.png") };
					foreach (RPCS3.Ps3User user in _rpcs3.GetPlayers())
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
					foreach (RPCS3.Ps3Game game in _rpcs3.GetAllGames())
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
					foreach (string imagePath in _rpcs3.GetScreenshots())
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
						}
					]);

					return [userCategory, gamesCategory, screenshotsCategory, settingsCategory];
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
		
		public void MoveHorizontal(int delta) => SelectCategory(_categoryIndex + delta);

		public void MoveVertical(int delta) => SelectItem(_itemIndex + delta);

		public void ActivateSelected()
		{
			XmbItem currentItem = CurrentItems.First(x => x.IsSelected);
			if (DataContext is not MainWindow mainWindow) return;

			Task.Run( () =>
			{
				Dispatcher.Invoke(async() =>
				{
					if (CurrentItems.Count == 0) return;

					// Users Category
					if (_categoryIndex == 0)
					{
						if (currentItem.Subtitle == "SYS")
						{
							// Create User
						}
						else if (!int.TryParse(currentItem.Subtitle, out _currentSelectedUserId))
						{
							CatalystMessageBoxResult result = await mainWindow.CatalystMessageBoxControl.ShowAsync("ERROR", "Failed to parse user_id, please contact support.", icon: CatalystMessageBoxIcon.Error);
							_ = result;
						}
						else UpdateActiveUserIndicator();
					}
					// Game Category
					else if (_categoryIndex == 1) 
					{
						try
						{
							(bool valid, RPCS3.RPS3FailedReason reason) = _rpcs3.ValidateInstall();
							if (!valid)
							{
								CatalystMessageBoxResult result = await mainWindow.CatalystMessageBoxControl.ShowAsync("ERROR", $"Failed to validate install: {reason}", icon: CatalystMessageBoxIcon.Error);
								_ = result;
								return;
							}

							if (!_rpcs3.IsFirmwareInstalled())
							{
								CatalystMessageBoxResult result = await mainWindow.CatalystMessageBoxControl.ShowAsync("ERROR", $"RPCS does not have any valid firmware installed, please go to the settings tab.", icon: CatalystMessageBoxIcon.Error);
								_ = result;
								return;
							}

							_ = Task.Run(async () =>
							{
								Task launchTask = _rpcs3.LaunchGameAsUser(_rpcs3.GetAllGames().First(x => x.MetData.TitleId == currentItem.Subtitle), _currentSelectedUserId);

								await Task.Delay(2000);

								await Dispatcher.InvokeAsync(() =>
								{
									StopBackgroundAudio();
									StopBackgroundVideo();
									mainWindow.Hide();
								});


								await Task.WhenAny(WaitForExitConnection(), launchTask);

								await Dispatcher.InvokeAsync(() =>
								{
									StartBackgroundAudio();
									StartBackgroundVideo();
									mainWindow.Show();
									FocusDefault();
								});
							});
							
						}
						catch (Exception ex)
						{
							CatalystMessageBoxResult result = await mainWindow.CatalystMessageBoxControl.ShowAsync("ERROR", ex.ToString(), icon: CatalystMessageBoxIcon.Error);
							_ = result;
						}
					}

					FocusDefault();
				});
			});
		}

		public void FocusDefault()
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
			{
				Focus();
				Keyboard.Focus(this);
			}));
		}

		private const string VideoVariant = "deep-blue";

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

		private const string AudioFileName = "ps3-xmb-audio.m4a";

		private void StartBackgroundVideo()
		{
			try
			{
				if (!VideoVariantFileNames.TryGetValue(VideoVariant, out string? fileName))
				{
					Debug.WriteLine("Unknown video variant.");
					return;
				}

				string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ps3", "Backgrounds", fileName);
				
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
				string audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ps3", "Sounds", AudioFileName);

				if (!File.Exists(audioPath)) return;
				
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
					BackRequested?.Invoke(this, EventArgs.Empty);
					e.Handled = true;
					break;
			}

			if (e.Handled)
				PlayDirectionalAudio(e.Key);
		}

		private async Task WaitForExitConnection()
		{
			string path = Path.Combine(Path.GetDirectoryName(_emulatorConfig.ExecutablePath)!, "log", "RPCS3.log");
			while (true)
			{
				try
				{
					string text = await _rpcs3.ReadLogTextAsync(path);
					if (text.Contains("SYS: Requesting game to exit") ) { break; }
				}
				catch { /**/ }
				await Task.Delay(250);
			}
		}
	}
}
