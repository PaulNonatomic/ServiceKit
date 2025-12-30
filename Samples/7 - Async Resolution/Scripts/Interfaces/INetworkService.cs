using System.Threading.Tasks;

namespace ServiceKitSamples.AsyncResolutionExample
{
    /// <summary>
    /// Network service with slow initialization (simulates connecting to server).
    /// </summary>
    public interface INetworkService
    {
        bool IsConnected { get; }
        string ServerUrl { get; }
        Task<string> FetchDataAsync(string endpoint);
    }
}
