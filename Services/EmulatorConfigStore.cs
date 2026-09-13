using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCatalyst.Models;

namespace ProjectCatalyst.Services
{
	public static class EmulatorConfigStore
	{
		private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProjectCatalyst", "emulators.json");

		private static readonly JsonSerializerOptions Options = new()
		{
			WriteIndented = true,
			Converters = { new JsonStringEnumConverter() }
		};

		public static List<EmulatorConfig> Load()
		{
			try
			{
				if (!File.Exists(FilePath)) return [];

				string json = File.ReadAllText(FilePath);
				return JsonSerializer.Deserialize<List<EmulatorConfig>>(json, Options) ?? [];
			}
			catch (Exception ex)
			{
				LogError($"Emu config failed to load: {ex}");
				return [];
			}
		}

		public static void Save(IEnumerable<EmulatorConfig> configs)
		{
			try
			{
				string? directory = Path.GetDirectoryName(FilePath);

				if (!string.IsNullOrEmpty(directory))
					Directory.CreateDirectory(directory);

				string json = JsonSerializer.Serialize(configs, Options);
				File.WriteAllText(FilePath, json);
			}
			catch (Exception ex)
			{
				LogError($"Emu config failed to save: {ex}");
			}
		}
	}
}
