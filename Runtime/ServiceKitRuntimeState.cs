using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Thread-safe snapshot of whether the player is running. <see cref="Application.isPlaying"/> may
	/// only be read on the main thread, but async injection continuations can resume on a worker thread
	/// (e.g. when a consumer awaits the injection with <c>ConfigureAwait(false)</c>). This caches the
	/// state on the main thread via Unity lifecycle hooks so injection code can read it from anywhere
	/// without throwing.
	/// </summary>
	internal static class ServiceKitRuntimeState
	{
		// volatile so writes from the main thread are visible to a reader on a worker thread.
		private static volatile bool _isPlaying;

		/// <summary>True in a player build and in editor play mode; false in editor edit mode.</summary>
		public static bool IsPlaying => _isPlaying;

		// Runs when the player loop starts (both in builds and on entering editor play mode), and before
		// any scene loads. In edit mode it never runs, so the field stays false there.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnRuntimeLoad() => _isPlaying = true;

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
