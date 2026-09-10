using ProjectCatalyst.Util;
using System.IO;
using System.IO.Pipelines;

namespace ProjectCatalyst.Wrappers
{
	internal class RPCS3(string path)
	{
		public record Ps3Game(Ps3GameMetadata MetData, string IconLocation, string InstallLocation);
		public record Ps3User(int UserId, string Username);

		public enum RPS3FailedReason
		{
			Valid = 0,
			MissingExecutable = 1,
			MissingInitDirectory = 2,
			MissingFirstLaunchDirectories = 3
		}

		private static readonly string[] RequiredFirstLaunchDirectories = ["config", "GuiConfigs", "Icons", "qt6"];
		private static readonly string[] RequiredDirectories = ["dev_bdvd", "dev_flash", "dev_flash2", "dev_flash3", "dev_hdd0", "dev_hdd1", "dev_usb000"];
		private static readonly string[] RequiredFirmwareDirectories = ["ps2emu", "ps1emu", "pspemu", "bdplayer", "data", "sys", "vsh"];

		public (bool, RPS3FailedReason) ValidateInstall()
		{
			if (!File.Exists(Path.Combine(path, "rpcs3.exe")))
				return (false, RPS3FailedReason.MissingExecutable);

			if (RequiredFirstLaunchDirectories.Any(directory => !Directory.Exists(Path.Combine(path, directory))))
				return (false, RPS3FailedReason.MissingInitDirectory);

			if (RequiredDirectories.Any(directory => !Directory.Exists(Path.Combine(path, directory))))
				return (false, RPS3FailedReason.MissingFirstLaunchDirectories);

			return (true, RPS3FailedReason.Valid);
		}

		public bool IsFirmwareInstalled() => RequiredFirmwareDirectories.All(x => Directory.Exists(Path.Combine(path, "dev_flash", x)));

		public List<Ps3User> GetPlayers()
		{
			(bool, RPS3FailedReason) valid = ValidateInstall();
			if (!valid.Item1) throw new Exception(valid.Item2.ToString());

			string usersPath = Path.Combine(path, "dev_hdd0", "home");
			if (!Directory.Exists(usersPath)) throw new InvalidOperationException("dev_hdd0/home missing.");

			string[] usersData = Directory.GetDirectories(usersPath);
			List<Ps3User> users = [];
			users.AddRange(from userDir in usersData let userId = int.Parse(Path.GetFileName(userDir)) let username = File.ReadAllText(Path.Combine(userDir, "localusername")) select new Ps3User(userId, username));
			return users;

		}

		public async Task LaunchGameAsUser(Ps3Game game, int userId)
		{
			if (!IsFirmwareInstalled())
				throw new InvalidOperationException("Missing firmware");

			OverwriteWelcomeBox();
			ExecutableRunner.KillClient("rpcs3.exe");

			string user = userId.ToString().PadLeft(8, '0');

			await ExecutableRunner.RunExecutable(Path.Combine(path, "rpcs3.exe"), ["--no-gui", "--fullscreen", "--user-id", user, game.InstallLocation]);
		}

		public async Task LaunchGame(Ps3Game game)
		{
			if (!IsFirmwareInstalled())
				throw new InvalidOperationException("Missing firmware");

			OverwriteWelcomeBox();
			ExecutableRunner.KillClient("rpcs3.exe");
			await ExecutableRunner.RunExecutable(Path.Combine(path, "rpcs3.exe"), ["--no-gui", "--fullscreen", game.InstallLocation]);
		}

		public void InstallFirmware(string firmware, bool forceInstall = false) => InstallFirmwareAsync(firmware, forceInstall).Wait();

		public async Task InstallFirmwareAsync(string firmware, bool forceInstall = false)
		{
			if (IsFirmwareInstalled() && !forceInstall) return;
			await ExecutableRunner.RunExecutable(Path.Combine(path, "rpcs3.exe"), ["--headless", "--installfw", firmware]);
		}

		public List<string> GetScreenshots()
		{
			(bool, RPS3FailedReason) valid = ValidateInstall();
			if (!valid.Item1) throw new Exception(valid.Item2.ToString());

			string usersPath = Path.Combine(path, "captures");
			if (!Directory.Exists(usersPath)) throw new InvalidOperationException("captures missing.");

			return Directory.GetFiles(usersPath, "*.*").ToList();
		}


		public List<Ps3Game> GetAllGames()
		{
			List<Ps3Game> gamesData = [];
			string iconOut = Path.Combine(path, "Icons", "ProjectCatalyst");
			string gamesDir = Path.Combine(path, "games");
			if (!Directory.Exists(gamesDir))
				throw new InvalidOperationException("Failed to find games folder");

			Directory.CreateDirectory(iconOut);

			foreach (string iso in Directory.GetFiles(gamesDir, "*.iso"))
			{
				Ps3GameMetadata meta = Ps3SfoReader.ReadMetadata(iso);
				string iconExtract = Path.Combine(iconOut, $"{meta.TitleId}.png");
				if (!File.Exists(iconExtract))
					IsoReader.ExtractFile(iso, "PS3_GAME/ICON0.png", iconExtract);
				gamesData.Add(new Ps3Game(meta, iconExtract, iso));
			}

			return gamesData;
		}


		private void OverwriteWelcomeBox(bool enabled = false)
		{
			string settingsIni = Path.Combine(path, "GuiConfigs", "CurrentSettings.ini");
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
					enabled ? fileText.Replace("infoBoxEnabledWelcome=false", "infoBoxEnabledWelcome=true")
						: fileText.Replace("infoBoxEnabledWelcome=true", "infoBoxEnabledWelcome=false"));
			}
			else if (fileText.Contains("[main_window]"))
			{
				File.WriteAllText(settingsIni,
					fileText.Replace("[main_window]",
						$"[main_window]{Environment.NewLine}infoBoxEnabledWelcome={(enabled ? "true" : "false")}{Environment.NewLine}"));
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
