using UnityEngine;

namespace ServiceKitSamples.BasicUsage
{
	/// <summary>
	/// A simple implementation of IGreetingService.
	/// This is a plain C# class (not a MonoBehaviour) that can be registered as a service.
	/// </summary>
	public class GreetingService : IGreetingService
	{
		private int _greetingCount;

		public int GreetingCount => _greetingCount;

		public string GetGreeting(string name)
		{
			_greetingCount++;
			var greeting = $"Hello, {name}! Welcome to ServiceKit. (Greeting #{_greetingCount})";
			Debug.Log($"[GreetingService] Generated greeting for '{name}'");
			return greeting;
		}
	}
}
