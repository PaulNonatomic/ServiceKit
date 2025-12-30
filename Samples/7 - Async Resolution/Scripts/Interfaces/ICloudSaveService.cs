using System.Threading.Tasks;

namespace ServiceKitSamples.AsyncResolutionExample
{
    /// <summary>
    /// Cloud save service with slow async operations.
    /// </summary>
    public interface ICloudSaveService
    {
        bool IsReady { get; }
        Task<bool> SaveAsync(string key, string data);
        Task<string> LoadAsync(string key);
    }
}
