using System.Threading;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	internal static class ServiceKitThreading
	{
#if SERVICEKIT_UNITASK
		public static async UniTask SwitchToUnityThread(SynchronizationContext unityContext)
#else
		public static async Task SwitchToUnityThread(SynchronizationContext unityContext)
#endif
		{
			if (SynchronizationContext.Current == unityContext)
			{
				return;
			}
			
			// If unityContext is null (e.g., when called from background thread in tests),
			// we can't switch to Unity thread. Just return and continue on current thread.
			if (unityContext == null)
			{
				return;
			}
			
#if SERVICEKIT_UNITASK
			var tcs = new UniTaskCompletionSource<bool>();
			unityContext.Post(_ => tcs.TrySetResult(true), null);
			await tcs.Task;
#else
			var tcs = new TaskCompletionSource<bool>();
			unityContext.Post(_ => tcs.SetResult(true), null);
			await tcs.Task;
#endif
		}
	}
}