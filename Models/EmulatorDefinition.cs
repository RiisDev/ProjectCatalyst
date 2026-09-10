using ProjectCatalyst.Services;

namespace ProjectCatalyst.Models
{
	public sealed class EmulatorDefinition
	{
		public required string Name { get; init; }
		public required SystemType SystemType { get; init; }

		public IEmulatorDetector? Detector { get; init; }

		public override string ToString() => Name;
	}
}
