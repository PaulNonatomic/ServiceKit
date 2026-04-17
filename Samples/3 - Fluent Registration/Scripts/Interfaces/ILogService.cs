namespace ServiceKitSamples.FluentRegistrationExample
{
	/// <summary>
	/// Logging service interface for application-wide logging.
	/// </summary>
	public interface ILogService
	{
		void Log(string message);
		void LogWarning(string message);
		void LogError(string message);
	}
}
