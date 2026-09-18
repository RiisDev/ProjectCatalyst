using ProjectCatalyst.Services;

namespace ProjectCatalyst.Models
{
	public static class EmulatorCatalog
	{
		public static readonly IReadOnlyList<EmulatorDefinition> All =
		[
			new() { Name = "RPCS3", SystemType = SystemType.Ps3, Detector = new Rpcs3Detector() },

			new() { Name = "Xenia", SystemType = SystemType.Xbox, Detector = new XeniaDetector() },
			new() { Name = "Cxbx-Reloaded", SystemType = SystemType.Xbox },

			new() { Name = "Dolphin", SystemType = SystemType.Wii, Detector = new DolphinDetector() },

			new() { Name = "shadPS4", SystemType = SystemType.Ps4 },
		];

		public static IEnumerable<EmulatorDefinition> ForSystem(SystemType systemType) => All.Where(e => e.SystemType == systemType);
	}
}
