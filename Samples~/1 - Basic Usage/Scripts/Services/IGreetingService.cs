namespace ServiceKitSamples.BasicUsage
{
	/// <summary>
	/// A simple service interface that provides greeting functionality.
	/// This demonstrates the basic pattern of defining service contracts as interfaces.
	/// </summary>
	public interface IGreetingService
	{
		/// <summary>
		/// Gets a greeting message for the specified name.
		/// </summary>
		string GetGreeting(string name);

		/// <summary>
		/// Gets the total number of greetings that have been generated.
		/// </summary>
		int GreetingCount { get; }
	}
}
