using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.OptionalDependencyExample
{
	/// <summary>
	/// Core service - always registered and available.
	/// </summary>
	[Service(typeof(ICoreService))]
	public class CoreService : ServiceKitBehaviour, ICoreService
	{
		[SerializeField] private string _gameVersion = "1.0.0";

		public string GameVersion => _gameVersion;
		public bool IsInitialized { get; private set; }

		protected override void InitializeService()
		{
			IsInitialized = true;
			Debug.Log($"[CoreService] Initialized - Game Version: {_gameVersion}");
		}
	}
}
