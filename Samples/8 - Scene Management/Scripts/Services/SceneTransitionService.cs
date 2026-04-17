using System;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ServiceKitSamples.SceneManagementExample
{
	/// <summary>
	/// Manages scene transitions with proper service lifecycle handling.
	/// This is a global service that persists across scenes.
	/// </summary>
	[Service(typeof(ISceneTransitionService))]
	public class SceneTransitionService : ServiceKitBehaviour, ISceneTransitionService
	{
		private static SceneTransitionService _instance;

		[InjectService] private IGlobalService _globalService;

		public string CurrentScene { get; private set; }
		public bool IsTransitioning { get; private set; }

		public event Action<string> OnSceneLoadStarted;
		public event Action<string> OnSceneLoadCompleted;

		protected override void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return;
			}

			_instance = this;
			DontDestroyOnLoad(gameObject);

			base.Awake();
		}

		protected override void InitializeService()
		{
			CurrentScene = SceneManager.GetActiveScene().name;
			Debug.Log($"[SceneTransitionService] Initialized in scene: {CurrentScene}");
		}

		public async Task LoadSceneAsync(string sceneName)
		{
			if (IsTransitioning)
			{
				Debug.LogWarning($"[SceneTransitionService] Already transitioning, ignoring request for: {sceneName}");
				return;
			}

			Debug.Log($"[SceneTransitionService] Starting transition: {CurrentScene} -> {sceneName}");

			IsTransitioning = true;
			OnSceneLoadStarted?.Invoke(sceneName);

			// Note: Scene-local services in current scene will be destroyed
			// and unregistered automatically when scene unloads

			var asyncOperation = SceneManager.LoadSceneAsync(sceneName);
			asyncOperation.allowSceneActivation = true;

			while (!asyncOperation.isDone)
			{
				await Task.Yield();
			}

			CurrentScene = sceneName;
			IsTransitioning = false;

			OnSceneLoadCompleted?.Invoke(sceneName);

			Debug.Log($"[SceneTransitionService] Transition complete: {sceneName}");
		}

		protected override void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
			base.OnDestroy();
		}
	}
}
