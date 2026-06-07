#if UNITY_WEBGL && !UNITY_EDITOR && !SERVICEKIT_UNITASK
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// WebGL has no thread pool, so the System.Threading.Tasks injection path cannot resume an
	/// awaiter that waits for a not-yet-ready service - injection silently hangs. UniTask is
	/// player-loop-based and works. This warns at startup in WebGL player builds that do not have
	/// UniTask, so the failure mode is explained instead of debugged cold. Compiled out everywhere else.
	/// </summary>
	internal static class ServiceKitWebGLGuard
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void WarnUniTaskRequired()
		{
			Debug.LogWarning(
				"[ServiceKit] On WebGL, asynchronous injection that waits for a not-yet-ready service " +
				"cannot complete on the System.Threading.Tasks path (no thread pool to resume continuations). " +
				"Install UniTask (com.cysharp.unitask) so ServiceKit uses its player-loop async path (SERVICEKIT_UNITASK).");
		}
	}
}
#endif
