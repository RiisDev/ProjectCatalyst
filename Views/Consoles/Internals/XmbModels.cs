using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ProjectCatalyst.Views.Consoles.Internals
{
	public sealed class XmbItem : INotifyPropertyChanged
	{
		public required string Name { get; init; }
		public string Subtitle { get; init; } = string.Empty;

		public string? CoverImagePath { get; init; }
		
		public string CoverLabel { get; init; } = string.Empty;

		public Color CoverAccentColor { get; init; } = Color.FromRgb(0x4A, 0x5A, 0x7A);

		public bool IsGameItem { get; init; }

		public bool IsActiveUser
		{
			get;
			set
			{
				if (field == value) return;
				field = value;
				OnPropertyChanged();
			}
		}

		public bool IsSelected
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
		private void OnPropertyChanged([CallerMemberName] string? name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public sealed class XmbCategory : INotifyPropertyChanged
	{
		public required string Name { get; init; }
		public string IconPath { get; init; } = string.Empty;
		public List<XmbItem> Items { get; init; } = [];

		public bool IsSelected
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
		private void OnPropertyChanged([CallerMemberName] string? name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
