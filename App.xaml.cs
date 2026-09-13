global using static ProjectCatalyst.Logging;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace ProjectCatalyst
{
	public partial class App
	{
		// Disabled Explicit since we send the source info manually
		// ReSharper disable twice ExplicitCallerInfoArgument
		protected override void OnStartup(StartupEventArgs e)
		{
			PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;
			
			AppDomain.CurrentDomain.ProcessExit += (_, _) => LogInfo("Application process is exiting.");
			AppDomain.CurrentDomain.UnhandledException += (_, exception) =>
			{
				if (exception.ExceptionObject is not Exception ex) Log(exception.ToString() ?? "UNKNOWN_UNHANDLED_ERROR");
				else LogError(ex.ToString(), ex.TargetSite?.Name ?? "Unknown Method", ex.Source ?? "Unknown Source");
			};

			TaskScheduler.UnobservedTaskException += (_, exception) => LogError(exception.Exception.ToString(), exception.Exception.TargetSite?.Name ?? "Unknown Method", exception.Exception.Source ?? "Unknown Source");
			
			DispatcherUnhandledException += (_, exception) => LogError(exception.Exception.ToString(), exception.Exception.TargetSite?.Name ?? "Unknown Method", exception.Exception.Source ?? "Unknown Source");
			Exit += (_, args) => LogInfo($"WPF application exiting with code {args.ApplicationExitCode}."); 

			base.OnStartup(e);
		}
	}

	public static class Logging
	{
		private static readonly Lock LogLock = new();
		private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "log.txt");
		private static readonly StreamWriter Writer = new(new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8) { AutoFlush = true };

		private static void Log(string message, string type, string caller, string filePath)
		{
			lock (LogLock)
			{
				ConsoleColor oldColor = Console.ForegroundColor;
				try
				{
					Console.ForegroundColor = type switch
					{
						"INFO" => ConsoleColor.DarkCyan,
						"ERROR" => ConsoleColor.Red,
						"LOG" => ConsoleColor.White,
						_ => oldColor
					};

					string fileName = Path.GetFileName(filePath);
					string data = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{fileName}.{caller}] [{type}] {message}";
					Console.WriteLine(data);
					Debug.WriteLine(data);
					Writer.WriteLine(data);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex);
					Console.WriteLine(ex);
				}
				Console.ForegroundColor = oldColor;
			}
		}

		public static void LogInfo(string message, [CallerMemberName] string caller = "", [CallerFilePath] string filePath = "") => Log(message, "INFO", caller, filePath);
		public static void LogError(string message, [CallerMemberName] string caller = "", [CallerFilePath] string filePath = "") => Log(message, "ERROR", caller, filePath);
		public static void Log(string message, [CallerMemberName] string caller = "", [CallerFilePath] string filePath = "") => Log(message, "LOG", caller, filePath);
	}
}
