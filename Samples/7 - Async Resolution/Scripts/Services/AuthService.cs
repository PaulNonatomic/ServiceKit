using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.AsyncResolutionExample
{
    /// <summary>
    /// Authentication service that depends on NetworkService.
    /// Demonstrates dependency injection with async services.
    /// </summary>
    [Service(typeof(IAuthService))]
    public class AuthService : ServiceBehaviour, IAuthService
    {
        [InjectService] private INetworkService _networkService;

        public bool IsAuthenticated { get; private set; }
        public string UserId { get; private set; }

        protected override void InitializeService()
        {
            // Network service is guaranteed to be ready at this point
            Debug.Log($"[AuthService] Initialized (Network connected: {_networkService.IsConnected})");
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            Debug.Log($"[AuthService] Attempting login for: {username}");

            // Simulate auth request
            await _networkService.FetchDataAsync("/auth/login");
            await Task.Delay(200);

            // Simulate successful login
            IsAuthenticated = true;
            UserId = $"user_{username.GetHashCode():X}";

            Debug.Log($"[AuthService] Login successful! UserId: {UserId}");
            return true;
        }

        public void Logout()
        {
            IsAuthenticated = false;
            UserId = null;
            Debug.Log("[AuthService] Logged out");
        }
    }
}
