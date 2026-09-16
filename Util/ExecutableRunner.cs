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

		private static async Task WaitForExitBestEffort(Process process, string label)
		{
			try { await process.WaitForExitAsync(); }
			catch (Exception ex) { LogInfo($"{label} exit status could not be tracked: {ex.Message}"); }
		}

		private static async Task<string> ResolveUnixPathAsync(string windowsPath)
		{
			try
			{
				Process process = new()
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = @"Z:\usr\bin\winepath",
						RedirectStandardOutput = true,
						UseShellExecute = false,
						CreateNoWindow = true
					}
				};

				process.StartInfo.ArgumentList.Add("-u");
				process.StartInfo.ArgumentList.Add(windowsPath);

				process.Start();
				string output = (await process.StandardOutput.ReadToEndAsync()).Trim();

				await WaitForExitBestEffort(process, "winepath -u");

				if (!string.IsNullOrWhiteSpace(output)) return output;
			}
			catch (Exception ex)
			{
				LogError($"winepath -u failed for {windowsPath}: {ex}");
			}

			string normalized = windowsPath.Replace('\\', '/');
			return normalized is [_, ':', ..] ? normalized[2..] : normalized;
		}

		public static async Task EnsureAppImageDataLink()
		{
			try
			{
				string scriptPath = await ResolveUnixPathAsync(AppImageLinkScript);

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

				process.StartInfo.ArgumentList.Add(scriptPath);

				process.Start();

				Task<string> stderrTask = process.StandardError.ReadToEndAsync();

				await WaitForExitBestEffort(process, "rpcs3-link-data.sh");

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
				string fileName;
				string[] launchArguments;

				if (string.Equals(Path.GetExtension(executable), ".AppImage", StringComparison.OrdinalIgnoreCase))
				{
					fileName = @"Z:\bin\sh";
					launchArguments = [await ResolveUnixPathAsync(AppImageLaunchScript), executable, .. arguments];
				}
				else
				{
					fileName = executable;
					launchArguments = arguments;
				}

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

				Task timeoutTask = Task.Delay(TimeSpan.FromHours(1));
				Task finishedTask = await Task.WhenAny(WaitForExitBestEffort(process, executable), timeoutTask);

				if (finishedTask == timeoutTask)
				{
					LogError($"{executable} exceeded 1 hour, killing process...");
					try { process.Kill(entireProcessTree: true); }
					catch (Exception ex) { LogError($"Failed to kill {executable}: {ex}"); }
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
