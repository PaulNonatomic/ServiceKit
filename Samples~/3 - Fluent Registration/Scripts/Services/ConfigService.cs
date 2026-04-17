using System.Collections.Generic;
using UnityEngine;

namespace ServiceKitSamples.FluentRegistrationExample
{
	/// <summary>
	/// Simple configuration service implementation.
	/// This is a plain C# class (non-MonoBehaviour) registered via the fluent API.
	/// </summary>
	public class ConfigService : IConfigService
	{
		private readonly Dictionary<string, object> _config = new();

		public string AppName { get; }
		public string Version { get; }
		public bool DebugMode { get; }

		public ConfigService(string appName, string version, bool debugMode)
		{
			AppName = appName;
			Version = version;
			DebugMode = debugMode;

			Debug.Log($"[ConfigService] Initialized: {AppName} v{Version} (Debug: {DebugMode})");
		}

		public T GetValue<T>(string key, T defaultValue = default)
		{
			if (_config.TryGetValue(key, out var value) && value is T typedValue)
			{
				return typedValue;
			}
			return defaultValue;
		}

		public void SetValue<T>(string key, T value)
		{
			_config[key] = value;
			Debug.Log($"[ConfigService] Set '{key}' = {value}");
		}
	}
}
