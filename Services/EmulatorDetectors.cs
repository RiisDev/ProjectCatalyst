using System.IO;

namespace ProjectCatalyst.Services
{
	internal static class DetectorHelpers
	{
		public static string? FindNearExecutable(string executablePath, params string[] candidateRelativePaths)
		{
			string? exeDir = Path.GetDirectoryName(executablePath);
			return string.IsNullOrEmpty(exeDir)
				? null
				: candidateRelativePaths.Select(
						relative => Path.GetFullPath(
							Path.Combine(
								exeDir,
								relative
							)
						)
					)
					.FirstOrDefault(Directory.Exists);
		}
	}

	public sealed class Rpcs3Detector : IEmulatorDetector
	{
		public string? TryDetectUsersDirectory(string executablePath)
		{
			if (string.Equals(Path.GetExtension(executablePath), ".AppImage", StringComparison.OrdinalIgnoreCase))
				return LinuxRpcs3ConfigDirectory();

			return DetectorHelpers.FindNearExecutable(executablePath, @"dev_hdd0\home");
		}

		public string? TryDetectGamesDirectory(string executablePath) => DetectorHelpers.FindNearExecutable(executablePath, "games");

		private static string? LinuxRpcs3ConfigDirectory()
		{
			string? home = Environment.GetEnvironmentVariable("HOME");
			if (!string.IsNullOrWhiteSpace(home))
				return "Z:" + Path.Combine(home, ".config", "rpcs3").Replace('/', '\\');

			string? userName = Environment.UserName;
			return string.IsNullOrWhiteSpace(userName)
				? null
				: $"Z:\\home\\{userName}\\.config\\rpcs3";
		}
	}

	public sealed class DolphinDetector : IEmulatorDetector
	{
		public string? TryDetectUsersDirectory(string executablePath)
		{
			string? portable = DetectorHelpers.FindNearExecutable(executablePath, "User");
			if (portable is not null) return portable;

			string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dolphin Emulator");
			return Directory.Exists(appData) ? appData : null;
		}

		public string? TryDetectGamesDirectory(string executablePath) => DetectorHelpers.FindNearExecutable(executablePath, "Games", "ISOs", "Roms");
	}

	public sealed class XeniaDetector : IEmulatorDetector
	{
		public string? TryDetectUsersDirectory(string executablePath)
			=> DetectorHelpers.FindNearExecutable(executablePath, "content");

		public string? TryDetectGamesDirectory(string executablePath)
			=> DetectorHelpers.FindNearExecutable(executablePath, "games", "Games");
	}
}
