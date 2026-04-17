using System.Threading.Tasks;

namespace ServiceKitSamples.AsyncResolutionExample
{
	/// <summary>
	/// Authentication service with async operations.
	/// </summary>
	public interface IAuthService
	{
		bool IsAuthenticated { get; }
		string UserId { get; }
		Task<bool> LoginAsync(string username, string password);
		void Logout();
	}
}
