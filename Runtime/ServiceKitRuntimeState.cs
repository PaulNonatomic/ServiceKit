using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Thread-safe snapshots of Unity main-thread state. <see cref="Application.isPlaying"/> may only be
	/// read on the main thread, and the Unity <see cref="SynchronizationContext"/> is only discoverable
	/// there - but async injection continuations can resume on a worker thread (e.g. when a consumer
	/// awaits the injection with <c>ConfigureAwait(false)</c>, or starts it off the main thread). This
	/// caches both on the main thread via Unity lifecycle hooks so injection code can read them from
	/// anywhere without throwing, and marshal back onto the main thread when it needs to.
	/// </summary>
	internal static class ServiceKitRuntimeState
	{
		// volatile so writes from the main thread are visible to a reader on a worker thread.
		private static volatile bool _isPlaying;
		private static volatile SynchronizationContext _unityContext;

		/// <summary>True in a player build and in editor play mode; false in editor edit mode.</summary>
		public static bool IsPlaying => _isPlaying;

		/// <summary>
		/// The Unity main-thread <see cref="SynchronizationContext"/>, captured on the main thread. Lets
		/// injection marshal a continuation back onto the main thread even when it was started off the
		/// main thread (where <c>SynchronizationContext.Current</c> is null). Null only before the first
		/// main-thread injection in a runtime that never had a context (e.g. some headless tests).
		/// </summary>
		public static SynchronizationContext UnityContext => _unityContext;

		/// <summary>Records the Unity context the first time a non-null one is seen on the main thread.</summary>
		internal static void CaptureUnityContext(SynchronizationContext context)
		{
			if (_unityContext == null && context != null)
			{
				_unityContext = context;
			}
		}

		// Runs when the player loop starts (both in builds and on entering editor play mode), and before
		// any scene loads. In edit mode it never runs, so the flag stays false there.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnRuntimeLoad()
		{
			_isPlaying = true;
			CaptureUnityContext(SynchronizationContext.Current);
		}

#if UNITY_EDITOR
		// Keeps the flag correct across play-mode transitions in the interactive editor, where the static
		// can survive a domain reload (or "Enter Play Mode Without Domain Reload"). Without this it would
		// stay true after exiting play, so a later edit-mode injection would wrongly try to defer.
		[InitializeOnLoadMethod]
		private static void HookEditor()
		{
			_isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
			EditorApplication.playModeStateChanged += change =>
			{
				switch (change)
				{
					case PlayModeStateChange.EnteredPlayMode:
						_isPlaying = true;
						break;
					case PlayModeStateChange.ExitingPlayMode:
					case PlayModeStateChange.EnteredEditMode:
						_isPlaying = false;
						break;
				}
			};
		}
#endif
	}
}
