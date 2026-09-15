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

		public static async Task EnsureAppImageDataLink()
		{
			try
			{
				Process process = new()
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = @"Z:\bin\sh",
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true
					}
				};

				process.StartInfo.ArgumentList.Add(AppImageLinkScript);

				process.Start();

				Task<string> stderrTask = process.StandardError.ReadToEndAsync();

				try
				{
					await process.WaitForExitAsync();
				}
				catch (InvalidOperationException)
				{
					LogInfo("rpcs3-link-data.sh exit status could not be tracked, the symlink step likely still ran to completion.");
				}

				string stderr = await stderrTask;
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

				Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
				Task<string> stderrTask = process.StandardError.ReadToEndAsync();

				try
				{
					TimeSpan timeout = TimeSpan.FromHours(1);
					Task processTask = process.WaitForExitAsync();
					Task timeoutTask = Task.Delay(timeout);
					Task finishedTask = await Task.WhenAny(processTask, timeoutTask);

					if (finishedTask == timeoutTask)
					{
						LogError($"{executable} exceeded 1 hour, killing process...");
						process.Kill(entireProcessTree: true);
					}
					else
					{
						await processTask;
						LogInfo($"[{executable}] completed normally, exit code {process.ExitCode}.");
					}
				}
				catch (InvalidOperationException)
				{
					LogInfo($"[{executable}] exit status could not be tracked.");
				}
				catch (Exception ex)
				{
					LogError($"Error while waiting for {executable} to exit: {ex}");
				}

				string stdout = await stdoutTask;
				string stderr = await stderrTask;

				LogInfo($"[{executable}] STDOUT: {stdout}");
				LogInfo($"[{executable}] STDERR: {stderr}");
			}
			catch (Exception ex)
			{
				LogError(ex.ToString());
			}

		}
	}
}
