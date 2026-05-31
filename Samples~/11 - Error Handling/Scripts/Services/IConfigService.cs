namespace ServiceKitSamples.ErrorHandlingExample
{
	public interface IConfigService
	{
		string AppName { get; }
		int MaxRetries { get; }
	}
}
