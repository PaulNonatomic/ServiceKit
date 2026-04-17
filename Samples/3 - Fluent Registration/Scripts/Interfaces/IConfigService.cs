namespace ServiceKitSamples.FluentRegistrationExample
{
	/// <summary>
	/// Configuration service interface for managing application settings.
	/// </summary>
	public interface IConfigService
	{
		string AppName { get; }
		string Version { get; }
		bool DebugMode { get; }

		T GetValue<T>(string key, T defaultValue = default);
		void SetValue<T>(string key, T value);
	}
}
