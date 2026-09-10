using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectCatalyst.Models
{
	public sealed class PlatformItem : INotifyPropertyChanged
	{
		public required string Name { get; init; }

		public string Subtitle { get; init; } = string.Empty;

		public string Glyph { get; init; } = "??";

		public string? IconImagePath { get; init; }

		public required Color AccentColor { get; init; }

		public string? ExecutablePath { get; init; }
		
		public string? WorkingDirectory { get; init; }

		public Func<UserControl>? ConsoleViewFactory { get; init; }

		public bool IsSettingsTile { get; init; }

		public bool IsAddEmulatorTile { get; init; }
		
		public bool IsLaunching
		{
			get;
			set
			{
				if (field == value) return;
				field = value;
				OnPropertyChanged();
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
