using System.Windows.Media;

namespace ProjectCatalyst.Models
{
	public enum SystemType
	{
		Xbox,
		Ps3,
		Ps4,
		Wii
	}

	public readonly record struct SystemTypePresentation(string DisplayName, string Glyph, Color AccentColor);

	public static class SystemTypeExtensions
	{
		private static readonly Dictionary<SystemType, SystemTypePresentation> Presentations = new()
		{
			[SystemType.Ps3] = new SystemTypePresentation("PlayStation 3", "PS3", (Color)ColorConverter.ConvertFromString("#4A90D9")),
			[SystemType.Ps4] = new SystemTypePresentation("PlayStation 4", "PS4", (Color)ColorConverter.ConvertFromString("#00C2FF")),
			[SystemType.Xbox] = new SystemTypePresentation("Xbox", "XB", (Color)ColorConverter.ConvertFromString("#5CB85C")),
			[SystemType.Wii] = new SystemTypePresentation("Wii", "Wii", (Color)ColorConverter.ConvertFromString("#F5F5F5"))
		};

		private static readonly Color FallbackAccentColor = (Color)ColorConverter.ConvertFromString("#8792A6");

		public static SystemTypePresentation GetPresentation(this SystemType systemType) =>
			Presentations.TryGetValue(systemType, out SystemTypePresentation presentation)
				? presentation
				: new SystemTypePresentation(systemType.ToString(), systemType.ToString(), FallbackAccentColor);
	}
}
