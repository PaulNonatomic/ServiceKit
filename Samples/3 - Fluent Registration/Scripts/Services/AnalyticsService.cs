using System.Collections.Generic;
using UnityEngine;

namespace ServiceKitSamples.FluentRegistrationExample
{
	/// <summary>
	/// Analytics service implementation.
	/// Demonstrates a service that depends on multiple other services.
	/// </summary>
	public class AnalyticsService : IAnalyticsService
	{
		private readonly IConfigService _configService;
		private readonly ILogService _logService;
		private int _totalEventsTracked;

		public int TotalEventsTracked => _totalEventsTracked;

		public AnalyticsService(IConfigService configService, ILogService logService)
		{
			_configService = configService;
			_logService = logService;

			_logService.Log("[AnalyticsService] Initialized");

			if (_configService.DebugMode)
			{
				_logService.Log("[AnalyticsService] Running in debug mode - events will be logged locally only");
			}
		}

		public void TrackEvent(string eventName)
		{
			TrackEvent(eventName, null);
		}

		public void TrackEvent(string eventName, Dictionary<string, object> properties)
		{
			_totalEventsTracked++;

			if (_configService.DebugMode)
			{
				var propsStr = properties != null
					? string.Join(", ", FormatProperties(properties))
					: "none";
				_logService.Log($"[Analytics] Event: {eventName} | Properties: {propsStr}");
			}
			else
			{
				// In a real implementation, this would send to an analytics backend
				Debug.Log($"[AnalyticsService] Would send event '{eventName}' to backend");
			}
		}

		private IEnumerable<string> FormatProperties(Dictionary<string, object> properties)
		{
			foreach (var kvp in properties)
			{
				yield return $"{kvp.Key}={kvp.Value}";
			}
		}
	}
}
