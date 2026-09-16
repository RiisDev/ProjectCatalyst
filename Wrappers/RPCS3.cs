using ProjectCatalyst.Util;
using System.IO;

namespace ProjectCatalyst.Wrappers
{
	public class RPCS3
	{
		public record Ps3Game(Ps3GameMetadata MetData, string IconLocation, string InstallLocation);
		public record Ps3User(int UserId, string Username);

		public enum RPS3FailedReason
		{
			Valid = 0,
			MissingExecutable = 1,
			MissingInitDirectory = 2, // Old logic, keep for backwards compat
			MissingFirstLaunchDirectories = 3
		}

		private static readonly string[] RequiredDirectories = ["dev_bdvd", "dev_flash", "dev_flash2", "dev_flash3", "dev_hdd0", "dev_hdd1", "dev_usb000"];
		private static readonly string[] RequiredFirmwareDirectories = ["ps2emu", "ps1emu", "pspemu", "bdplayer", "data", "sys", "vsh"];

		private readonly string _path;

		public readonly string Executable;
		public readonly string DevFlash;
		public readonly string DevHdd;
		public readonly string DevHome;
		public readonly string Captures;
		public readonly string GameIcons;
		public readonly string Games;

		private const string ProcessName = "rpcs3";

		public RPCS3(string executablePath)
		{
			_path = string.Equals(Path.GetExtension(executablePath), ".AppImage", StringComparison.OrdinalIgnoreCase)
				? Path.Combine(BaseDirectory, "Resources", "linux", "rpcs3")
				: Path.GetDirectoryName(executablePath) ?? throw new ArgumentException("Executable path has no parent directory.", nameof(executablePath));

			Executable = executablePath;
			DevFlash = Path.Combine(_path, "dev_flash");
			DevHdd = Path.Combine(_path, "dev_hdd0");
			DevHome = Path.Combine(_path, "dev_hdd0", "home");
			Captures = Path.Combine(_path, "captures");
			GameIcons = Path.Combine(_path, "Icons", "ProjectCatalyst");
			Games = Path.Combine(_path, "games");

			LogInfo("RPCS SETUP:");
			Log(_path);
			Log(Executable);
			Log(DevFlash);
			Log(DevHdd);
			Log(DevHome);
			Log(Captures);
			Log(GameIcons);
			Log(Games);
			Log(new string('─', 10));
		}

		public (bool, RPS3FailedReason) ValidateInstall()
		{
			try
			{
				Log("Checking for executable");
				if (!File.Exists(Executable))
				{
					LogError($"Executable not found at: {Executable}");
					return (false, RPS3FailedReason.MissingExecutable);
				}
				
				Log("Checking for required directories");
				string[] missingDirectories = RequiredDirectories.Where(directory => !Directory.Exists(Path.Combine(_path, directory))).ToArray();
				if (missingDirectories.Length > 0)
				{
					LogError($"Missing directories under {_path}: {string.Join(", ", missingDirectories)}");
					return (false, RPS3FailedReason.MissingFirstLaunchDirectories);
				}

				Log("Valid Launch");
				return (true, RPS3FailedReason.Valid);
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
				return (false, RPS3FailedReason.MissingExecutable);
			}
		}

		public bool IsFirmwareInstalled() => RequiredFirmwareDirectories.All(x => Directory.Exists(Path.Combine(DevFlash, x)));

		public List<Ps3User> GetPlayers()
		{
			try
			{
				(bool, RPS3FailedReason) valid = ValidateInstall();
				if (!valid.Item1) throw new Exception(valid.Item2.ToString());

				if (!Directory.Exists(DevHome)) throw new InvalidOperationException("dev_hdd0/home missing.");

				string[] usersData = Directory.GetDirectories(DevHome);
				List<Ps3User> users = [];
				users.AddRange(from userDir in usersData
					let userId = int.Parse(Path.GetFileName(userDir))
					let username = File.ReadAllText(Path.Combine(userDir, "localusername"))
					select new Ps3User(userId, username));
				return users;

			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
				return [];
			}
		}

		public async Task LaunchGameAsUser(Ps3Game game, int userId)
		{
			try
			{
				if (!IsFirmwareInstalled())
					throw new InvalidOperationException("Missing firmware");

				OverwriteWelcomeBox();
				ExecutableRunner.KillClient(ProcessName);

				string user = userId.ToString().PadLeft(8, '0');

				await ExecutableRunner.RunExecutable(Executable, ["--no-gui", "--fullscreen", "--user-id", user, game.InstallLocation]);
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}
		}

		public async Task LaunchGame(Ps3Game game)
		{
			try
			{
				if (!IsFirmwareInstalled())
					throw new InvalidOperationException("Missing firmware");

				OverwriteWelcomeBox();
				ExecutableRunner.KillClient(ProcessName);
				await ExecutableRunner.RunExecutable(Executable, ["--no-gui", "--fullscreen", game.InstallLocation]);
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}
		}

		public void InstallFirmware(string firmware, bool forceInstall = false) => InstallFirmwareAsync(firmware, forceInstall).Wait();

		public async Task InstallFirmwareAsync(string firmware, bool forceInstall = false)
		{
			Log("Installing firmware...");
			try
			{
				if (IsFirmwareInstalled() && !forceInstall) return;
				ExecutableRunner.KillClient(ProcessName);
				await ExecutableRunner.RunExecutable(Executable, ["--headless", "--installfw", firmware]);
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}
		}

		public List<string> GetScreenshots()
		{
			try
			{
				(bool, RPS3FailedReason) valid = ValidateInstall();
				if (!valid.Item1) throw new Exception(valid.Item2.ToString());

				if (!Directory.Exists(Captures)) throw new InvalidOperationException("captures missing.");

				return Directory.GetFiles(Captures, "*.*").ToList();
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
				return [];
			}
		}


		public List<Ps3Game> GetAllGames(string? gamesPath = null)
		{
			try
			{
				List<Ps3Game> gamesData = [];

				string gamesDir = gamesPath ?? Games;
				if (!Directory.Exists(gamesDir))
					throw new InvalidOperationException("Failed to find games folder");

				Directory.CreateDirectory(GameIcons);

				foreach (string iso in Directory.GetFiles(gamesDir, "*.iso"))
				{
					Ps3GameMetadata meta = Ps3SfoReader.ReadMetadata(iso);
					string iconExtract = Path.Combine(GameIcons, $"{meta.TitleId}.png");
					if (!File.Exists(iconExtract))
						IsoReader.ExtractFile(iso, "PS3_GAME/ICON0.png", iconExtract);
					gamesData.Add(new Ps3Game(meta, iconExtract, iso));
				}

				return gamesData;
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
				return [];
			}
		}

		public void CreateUser(string username)
		{
			int userIndex = Directory.GetDirectories(DevHome, "*").Length + 1;
			string user = userIndex.ToString().PadLeft(8, '0');
			
			if (Directory.Exists(Path.Combine(DevHome, user)))
			{
				for (int fileIndex = 1; fileIndex <= userIndex; fileIndex++)
				{
					user = fileIndex.ToString().PadLeft(8, '0');
					if (Directory.Exists(user)) continue;
					break;
				}
			}

			string newUserDirectory = Path.Combine(DevHome, user);
			Directory.CreateDirectory(Path.Combine(newUserDirectory, "exdata"));
			Directory.CreateDirectory(Path.Combine(newUserDirectory, "savedata"));
			Directory.CreateDirectory(Path.Combine(newUserDirectory, "trophy"));
			File.WriteAllText(Path.Combine(newUserDirectory, "localusername"), username);
		}

		private void OverwriteWelcomeBox(bool enabled = false)
		{
			try
			{
				string settingsIni = Path.Combine(_path, "GuiConfigs", "CurrentSettings.ini");
				if (!File.Exists(settingsIni))
				{
					File.WriteAllText(settingsIni, $"""
					                                [main_window]
					                                infoBoxEnabledWelcome={enabled}
					                                """);
					return;
				}

				string fileText = File.ReadAllText(settingsIni);
				if (fileText.Contains("infoBoxEnabledWelcome=true") || fileText.Contains("infoBoxEnabledWelcome=false"))
				{
					File.WriteAllText(settingsIni,
						enabled
							? fileText.Replace("infoBoxEnabledWelcome=false", "infoBoxEnabledWelcome=true")
							: fileText.Replace("infoBoxEnabledWelcome=true", "infoBoxEnabledWelcome=false"));
				}
				else if (fileText.Contains("[main_window]"))
				{
					File.WriteAllText(settingsIni,
						fileText.Replace("[main_window]",
							$"[main_window]{Environment.NewLine}infoBoxEnabledWelcome={(enabled ? "true" : "false")}{Environment.NewLine}"));
				}
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}
		}

		public async Task<string> ReadLogTextAsync(string path)
		{
			const int maxAttempts = 5;
			const int delayMilliseconds = 150;

			for (int attempt = 0; attempt < maxAttempts; attempt++)
			{
				try
				{
					return await FileReader.ReadFileAsync(path).ConfigureAwait(false);
				}
				catch (IOException) { await Task.Delay(delayMilliseconds).ConfigureAwait(false); }
			}

			return string.Empty;
		}
	}
}
