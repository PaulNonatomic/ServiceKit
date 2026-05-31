namespace ServiceKitSamples.ErrorHandlingExample
{
	public interface ISlowService
	{
		bool IsReady { get; }
		string GetData();
	}
}
