namespace ProjectCatalyst.Services
{
	public interface IEmulatorDetector
	{
		string? TryDetectUsersDirectory(string executablePath);

		string? TryDetectGamesDirectory(string executablePath);
	}
}
