namespace ServiceKitSamples.OptionalDependencyExample
{
	/// <summary>
	/// Core service that is always available.
	/// This represents a required dependency.
	/// </summary>
	public interface ICoreService
	{
		string GameVersion { get; }
		bool IsInitialized { get; }
	}
}
