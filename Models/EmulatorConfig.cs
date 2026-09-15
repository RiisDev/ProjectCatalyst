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

		// The fields below are only meaningful for PS3 (Ps3ConsoleView) -
		// left at their defaults and simply unused for every other system.

		/// <summary>Last-selected user id, so the XMB remembers who was signed in across sessions instead of always defaulting.</summary>
		public int? LastSelectedUserId { get; set; }

		/// <summary>Key into Ps3ConsoleView's video-variant dictionary for the chosen background video.</summary>
		public string? BackgroundVideoName { get; set; }

		/// <summary>Ambient background audio volume, 0.0-1.0.</summary>
		public double AmbientVolume { get; set; } = 0.4;

		/// <summary>Navigation/movement click sound volume, 0.0-1.0.</summary>
		public double MovementVolume { get; set; } = 1.0;
	}
}
