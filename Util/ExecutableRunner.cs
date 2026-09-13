using System.Diagnostics;
using System.IO;

namespace ProjectCatalyst.Util
{
	public static class ExecutableRunner
	{
		public static void KillClient(string fileName)
		{
			try { Process.GetProcessesByName(fileName).ToList().ForEach(x => x.Kill(true)); }
			catch {/**/}
		}

		public static async Task RunExecutable(string executable, string[] arguments)
		{
			try
			{
				// RPCS3 Can only run once instance
				if (Path.GetFileName(executable) == "rpcs3.exe") KillClient(Path.GetFileName(executable));

				Process process = new()
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = executable,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true,
						WorkingDirectory = Path.GetDirectoryName(executable)
					}
				};

				foreach (string arg in arguments)
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
