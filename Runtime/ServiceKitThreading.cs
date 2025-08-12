using System.Threading;
using Cysharp.Threading.Tasks;

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