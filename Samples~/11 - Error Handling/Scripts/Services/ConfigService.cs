using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.ErrorHandlingExample
{
	/// <summary>
	/// A simple configuration service that is always available.
	///
	/// Demonstrates:
	/// - Basic ServiceKitBehaviour with no dependencies
	/// - Immediate readiness (no async initialization)
	/// </summary>
	[Service(typeof(IConfigService))]
	public class ConfigService : ServiceKitBehaviour, IConfigService
	{
		[SerializeField] private string _appName = "ErrorHandlingDemo";
		[SerializeField] private int _maxRetries = 3;

		public string AppName => _appName;
		public int MaxRetries => _maxRetries;

		protected override void InitializeService()
		{
			Debug.Log($"[ConfigService] Initialized: AppName={_appName}, MaxRetries={_maxRetries}");
		}
	}
}
