using UnityEngine;
using UnityEngine.Serialization;
namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Runtime debugging component for ServiceKit
	/// </summary>
	public class ServiceKitDebugger : MonoBehaviour
	{
		[FormerlySerializedAs("_serviceKitLocatorLocator")]
        [FormerlySerializedAs("_serviceLocator")]
		[FormerlySerializedAs("_serviceKit")]
		[SerializeField] private ServiceKitLocator _serviceKitLocator;
		[SerializeField] private bool _showDebugInfo = true;
		[SerializeField] private KeyCode _toggleKey = KeyCode.F12;

		private GUIStyle _boxStyle;
		private GUIStyle _labelStyle;
		private Vector2 _scrollPosition;
		private Rect _windowRect = new Rect(10, 10, 400, 500);
		private bool _isDragging;

		private void Awake()
		{
			if (_serviceKitLocator == null)
			{
				Debug.LogWarning("[ServiceKitDebugger] No ServiceKit assigned!");
			}
		}

		private void Update()
		{
			if (Input.GetKeyDown(_toggleKey))
			{
				_showDebugInfo = !_showDebugInfo;
			}
		}

		private void OnGUI()
		{
			if (!_showDebugInfo || _serviceKitLocator == null) return;

			InitializeStyles();

			_windowRect = GUI.Window(0, _windowRect, DrawDebugWindow, "ServiceKit Debug");
		}

		private void InitializeStyles()
		{
			if (_boxStyle == null)
			{
				_boxStyle = new GUIStyle(GUI.skin.box)
				{
					padding = new RectOffset(10, 10, 10, 10)
				};
			}

			if (_labelStyle == null)
			{
				_labelStyle = new GUIStyle(GUI.skin.label)
				{
					wordWrap = true
				};
			}
		}

		private void DrawDebugWindow(int windowID)
		{
			// Make window draggable
			GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));

			GUILayout.Label($"ServiceKit: {_serviceKitLocator.name}", GUI.skin.box);
			GUILayout.Label($"Total Services: {_serviceKitLocator.GetAllServices().Count}");

			GUILayout.Space(10);

			// Service list with scroll view
			GUILayout.Label("Registered Services:", GUI.skin.box);

			using (var scrollView = new GUILayout.ScrollViewScope(_scrollPosition, GUILayout.Height(350)))
			{
				_scrollPosition = scrollView.scrollPosition;

				var services = _serviceKitLocator.GetAllServices();
				foreach (var serviceInfo in services)
				{
					DrawServiceDebugInfo(serviceInfo);
				}

				if (services.Count == 0)
				{
					GUILayout.Label("No services registered", _labelStyle);
				}
			}

			GUILayout.Space(10);

			// Actions
			if (GUILayout.Button("Cleanup Destroyed Services"))
			{
				_serviceKitLocator.CleanupDestroyedServices();
			}

			if (GUILayout.Button("Close (F12)"))
			{
				_showDebugInfo = false;
			}
		}

		private void DrawServiceDebugInfo(ServiceInfo serviceInfo)
		{
			using (new GUILayout.VerticalScope(_boxStyle))
			{
				GUILayout.Label($"<b>{serviceInfo.ServiceType.Name}</b>", _labelStyle);
				GUILayout.Label($"Scene: {serviceInfo.SceneName}", _labelStyle);

				if (serviceInfo.IsDontDestroyOnLoad)
				{
					GUILayout.Label("<color=yellow>DontDestroyOnLoad</color>", _labelStyle);
				}

				if (serviceInfo.Service is MonoBehaviour mb && mb == null)
				{
					GUILayout.Label("<color=red>DESTROYED</color>", _labelStyle);
				}

				GUILayout.Label($"Registered: {serviceInfo.RegisteredAt:HH:mm:ss}", _labelStyle);
			}
		}
	}
}
