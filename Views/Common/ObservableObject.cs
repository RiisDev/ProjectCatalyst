using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectCatalyst.Views.Common
{
	/// <summary>Base for the small view models (XmbItem, XmbCategory, PlatformItem, ...) that just need to
	/// raise PropertyChanged from a field-backed property setter.</summary>
	public abstract class ObservableObject : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string? name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
