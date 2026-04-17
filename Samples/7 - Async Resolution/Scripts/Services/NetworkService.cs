using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace ServiceKitSamples.AsyncResolutionExample
{
	/// <summary>
	/// Simulates a network service with slow initialization.
	/// Demonstrates async initialization using InitializeServiceAsync().
	/// </summary>
	[Service(typeof(INetworkService))]
	public class NetworkService : ServiceKitBehaviour, INetworkService
	{
		[SerializeField] private string _serverUrl = "https://api.example.com";
		[SerializeField] private float _connectionDelay = 1.5f;

		public bool IsConnected { get; private set; }
		public string ServerUrl => _serverUrl;

		/// <summary>
		/// Async initialization - waits for "connection" to establish.
		/// Service won't be ready until this completes.
		/// </summary>
#if SERVICEKIT_UNITASK
		protected override async UniTask InitializeServiceAsync()
#else
		protected override async Task InitializeServiceAsync()
#endif
		{
			Debug.Log($"[NetworkService] Connecting to {_serverUrl}...");

			// Simulate connection delay
			await Task.Delay((int)(_connectionDelay * 1000));

			IsConnected = true;
			Debug.Log("[NetworkService] Connected!");
		}

		protected override void InitializeService()
		{
			Debug.Log("[NetworkService] Ready for requests");
		}

		public async Task<string> FetchDataAsync(string endpoint)
		{
			Debug.Log($"[NetworkService] Fetching: {endpoint}");
			await Task.Delay(100); // Simulate network latency
			return $"{{\"data\": \"Response from {endpoint}\"}}";
		}
	}
}
