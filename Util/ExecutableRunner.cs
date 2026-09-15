using System.Diagnostics;
using System.IO;

namespace ProjectCatalyst.Util
{
	public static class ExecutableRunner
	{
		private static readonly string AppImageLaunchScript = Path.Combine(BaseDirectory, "Resources", "linux", "rpcs3-launch.sh");
		private static readonly string AppImageLinkScript = Path.Combine(BaseDirectory, "Resources", "linux", "rpcs3-link-data.sh");

		public static void KillClient(string processName)
		{
			try { Process.GetProcessesByName(processName).ToList().ForEach(x => x.Kill(true)); }
			catch {/**/}
		}

		public static async Task EnsureAppImageDataLink(string appImagePath)
		{
			try
			{
				Process process = new()
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = @"Z:\bin\sh",
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					}
				};

				process.StartInfo.ArgumentList.Add(AppImageLinkScript);
				process.StartInfo.ArgumentList.Add(appImagePath);

				process.Start();

				string stderr = await process.StandardError.ReadToEndAsync();
				await process.WaitForExitAsync();

				if (!string.IsNullOrWhiteSpace(stderr))
					LogInfo($"[rpcs3-link-data.sh] STDERR: {stderr}");
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}
		}

		public static async Task RunExecutable(string executable, string[] arguments)
		{
			try
			{
				(string fileName, string[] launchArguments) = string.Equals(Path.GetExtension(executable), ".AppImage", StringComparison.OrdinalIgnoreCase)
					? (@"Z:\bin\sh", [AppImageLaunchScript, executable, .. arguments])
					: (executable, arguments);

				Process process = new()
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = fileName,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true,
						WorkingDirectory = Path.GetDirectoryName(executable)
					}
				};

				foreach (string arg in launchArguments)
					process.StartInfo.ArgumentList.Add(arg);

				process.Start();

				string stdout = await process.StandardOutput.ReadToEndAsync();
				string stderr = await process.StandardError.ReadToEndAsync();

				TimeSpan timeout = TimeSpan.FromHours(1);
				Task processTask = process.WaitForExitAsync();
				Task timeoutTask = Task.Delay(timeout);
				Task finishedTask = await Task.WhenAny(processTask, timeoutTask);

				if (finishedTask == timeoutTask)
				{
					try
					{
						LogError($"{executable} exceeded 1 hour, killing process...");
						process.Kill(entireProcessTree: true);
					}
					catch (Exception ex)
					{
						LogError(ex.ToString(), $"Error while trying to kill {executable} process.");
					}
				}
				else
				{
					LogInfo($"[{executable}] completed normally.");
				}

				LogInfo($"[{executable}] STDOUT: {stdout}");
				LogInfo($"[{executable}] STDERR: {stderr}");
				LogInfo($"[{executable}] Process exited with code {process.ExitCode}");
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}

		}
	}
}
