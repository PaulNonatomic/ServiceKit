using UnityEngine;

namespace ServiceKitSamples.FluentRegistrationExample
{
	/// <summary>
	/// Console-based logging service implementation.
	/// Demonstrates a service that depends on another service (IConfigService).
	/// </summary>
	public class ConsoleLogService : ILogService
	{
		private readonly IConfigService _configService;
		private readonly string _prefix;

		public ConsoleLogService(IConfigService configService)
		{
			_configService = configService;
			_prefix = $"[{_configService.AppName}]";

			Debug.Log($"[ConsoleLogService] Initialized with prefix: {_prefix}");
		}

		public void Log(string message)
		{
			Debug.Log($"{_prefix} {message}");
		}

		public void LogWarning(string message)
		{
			Debug.LogWarning($"{_prefix} WARNING: {message}");
		}

		public void LogError(string message)
		{
			Debug.LogError($"{_prefix} ERROR: {message}");
		}
	}
}
