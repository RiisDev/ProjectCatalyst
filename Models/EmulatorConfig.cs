namespace ProjectCatalyst.Models
{
	public sealed class EmulatorConfig
	{
		public string Id { get; init; } = Guid.NewGuid().ToString("N");

		public required SystemType SystemType { get; set; }
		public required string EmulatorName { get; set; }

		public string ExecutablePath { get; set; } = string.Empty;
		public string UsersDirectory { get; set; } = string.Empty;
		public string GamesDirectory { get; set; } = string.Empty;
	}
}
