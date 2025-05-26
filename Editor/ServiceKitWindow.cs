using System;
using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit;
using UnityEditor;
using UnityEngine;
using ServiceLocator;

namespace ServiceLocator.Editor
{
	public class ServiceKitWindow : EditorWindow
	{
		private ServiceKitLocator _targetServiceKitLocator;
		private Vector2 _scrollPosition;
		private string _searchFilter = "";
		private bool _showOnlyMonoBehaviours;
		private bool _showOnlyDontDestroyOnLoad;
		private string _filterScene = "All";
		private bool _autoRefresh = true;
		private float _lastRefreshTime;
		private const float AUTO_REFRESH_INTERVAL = 1f;

		[MenuItem("Window/ServiceKit/Service Inspector")]
		public static void ShowWindow()
		{
			var window = GetWindow<ServiceKitWindow>();
			window.titleContent = new GUIContent("ServiceKit Inspector", EditorGUIUtility.IconContent("d_ViewToolZoom").image);
			window.minSize = new Vector2(400, 300);
		}

		private void OnEnable()
		{
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private void OnDisable()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
		}

		private void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
			{
				Repaint();
			}
		}

		private void Update()
		{
			if (_autoRefresh && EditorApplication.isPlaying && Time.realtimeSinceStartup - _lastRefreshTime > AUTO_REFRESH_INTERVAL)
			{
				_lastRefreshTime = Time.realtimeSinceStartup;
				Repaint();
			}
		}

		private void OnGUI()
		{
			DrawToolbar();

			if (_targetServiceKitLocator == null)
			{
				DrawNoServiceKitSelected();
				return;
			}

			DrawServiceList();
		}

		private void DrawToolbar()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				// ServiceKit selector
				using (var changeCheck = new EditorGUI.ChangeCheckScope())
				{
					_targetServiceKitLocator = (ServiceKitLocator)EditorGUILayout.ObjectField(
						_targetServiceKitLocator,
						typeof(ServiceKitLocator),
						false,
						GUILayout.Width(200));

					if (changeCheck.changed)
					{
						_lastRefreshTime = Time.realtimeSinceStartup;
					}
				}

				GUILayout.FlexibleSpace();

				// Auto-refresh toggle
				_autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", EditorStyles.toolbarButton, GUILayout.Width(80));

				// Manual refresh button
				if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
				{
					if (_targetServiceKitLocator != null && EditorApplication.isPlaying)
					{
						_targetServiceKitLocator.CleanupDestroyedServices();
					}
					Repaint();
				}

				// Clear all services button
				using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
				{
					if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(60)))
					{
						if (EditorUtility.DisplayDialog("Clear All Services",
							"Are you sure you want to unregister all services?",
							"Yes", "No"))
						{
							_targetServiceKitLocator?.ClearServices();
						}
					}
				}
			}
		}

		private void DrawNoServiceKitSelected()
		{
			using (new GUILayout.VerticalScope())
			{
				GUILayout.FlexibleSpace();

				var style = new GUIStyle(EditorStyles.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 14
				};

				GUILayout.Label("Select a ServiceKit ScriptableObject", style);

				GUILayout.Space(10);

				using (new GUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Create New ServiceKit", GUILayout.Width(150), GUILayout.Height(30)))
					{
						CreateNewServiceKit();
					}
					GUILayout.FlexibleSpace();
				}

				GUILayout.FlexibleSpace();
			}
		}

		private void CreateNewServiceKit()
		{
			string path = EditorUtility.SaveFilePanelInProject(
				"Create ServiceKit",
				"ServiceKit",
				"asset",
				"Create a new ServiceKit ScriptableObject");

			if (!string.IsNullOrEmpty(path))
			{
				var serviceKit = CreateInstance<ServiceKitLocator>();
				AssetDatabase.CreateAsset(serviceKit, path);
				AssetDatabase.SaveAssets();
				_targetServiceKitLocator = serviceKit;
				Selection.activeObject = serviceKit;
			}
		}

		private void DrawServiceList()
		{
			var services = GetFilteredServices();

			// Filter controls
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.Label("Search:", GUILayout.Width(50));
					_searchFilter = EditorGUILayout.TextField(_searchFilter);

					if (GUILayout.Button("Clear", GUILayout.Width(50)))
					{
						_searchFilter = "";
					}
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					_showOnlyMonoBehaviours = EditorGUILayout.Toggle("MonoBehaviours Only", _showOnlyMonoBehaviours);
					_showOnlyDontDestroyOnLoad = EditorGUILayout.Toggle("DontDestroyOnLoad Only", _showOnlyDontDestroyOnLoad);
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.Label("Scene Filter:", GUILayout.Width(80));

					var scenes = GetAvailableScenes(services);
					int selectedIndex = Mathf.Max(0, scenes.IndexOf(_filterScene));
					int newIndex = EditorGUILayout.Popup(selectedIndex, scenes.ToArray());
					_filterScene = scenes[newIndex];
				}
			}

			// Service count
			EditorGUILayout.LabelField($"Services: {services.Count}", EditorStyles.boldLabel);

			// Service list
			using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPosition))
			{
				_scrollPosition = scrollView.scrollPosition;

				foreach (var serviceInfo in services)
				{
					DrawServiceInfo(serviceInfo);
				}
			}
		}

		private void DrawServiceInfo(ServiceInfo serviceInfo)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// Service type header
				using (new EditorGUILayout.HorizontalScope())
				{
					bool isMonoBehaviour = serviceInfo.Service is MonoBehaviour;
					var icon = isMonoBehaviour ? EditorGUIUtility.IconContent("d_cs Script Icon") : EditorGUIUtility.IconContent("d_ScriptableObject Icon");

					GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

					string typeName = serviceInfo.ServiceType.Name;
					if (serviceInfo.ServiceType.IsGenericType)
					{
						typeName = serviceInfo.ServiceType.GetGenericTypeDefinition().Name;
						typeName = typeName.Substring(0, typeName.IndexOf('`'));
					}

					GUILayout.Label(typeName, EditorStyles.boldLabel);

					GUILayout.FlexibleSpace();

					// Status indicators
					if (serviceInfo.IsDontDestroyOnLoad)
					{
						GUILayout.Label(new GUIContent("DDoL", "Don't Destroy On Load"), EditorStyles.miniLabel);
					}

					if (serviceInfo.Service is MonoBehaviour mb && mb == null)
					{
						GUILayout.Label(new GUIContent("DESTROYED", "This service has been destroyed"),
							new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.red } });
					}

					// Unregister button
					if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(18)))
					{
						UnregisterService(serviceInfo);
					}
				}

				// Service details
				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUILayout.LabelField("Type:", serviceInfo.ServiceType.FullName);
					EditorGUILayout.LabelField("Scene:", serviceInfo.SceneName);
					EditorGUILayout.LabelField("Registered:", serviceInfo.RegisteredAt.ToString("HH:mm:ss"));
					EditorGUILayout.LabelField("Registered By:", serviceInfo.RegisteredBy);

					// Show GameObject reference if MonoBehaviour
					if (serviceInfo.Service is MonoBehaviour monoBehaviour && monoBehaviour != null)
					{
						using (new EditorGUI.DisabledScope(true))
						{
							EditorGUILayout.ObjectField("GameObject:", monoBehaviour.gameObject, typeof(GameObject), true);
						}
					}
				}
			}
		}

		private void UnregisterService(ServiceInfo serviceInfo)
		{
			if (!EditorApplication.isPlaying)
			{
				EditorUtility.DisplayDialog("Cannot Unregister",
					"Services can only be unregistered during Play Mode.", "OK");
				return;
			}

			// Use the non-generic UnregisterService method
			_targetServiceKitLocator.UnregisterService(serviceInfo.ServiceType);
		}

		private List<ServiceInfo> GetFilteredServices()
		{
			if (!_targetServiceKitLocator) return new List<ServiceInfo>();

			var services = _targetServiceKitLocator.GetAllServices();

			// Apply search filter
			if (!string.IsNullOrEmpty(_searchFilter))
			{
				services = services.Where(s =>
					s.ServiceType.FullName != null && (s.ServiceType.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
													   s.ServiceType.FullName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
				).ToList();
			}

			// Apply MonoBehaviour filter
			if (_showOnlyMonoBehaviours)
			{
				services = services.Where(s => s.Service is MonoBehaviour).ToList();
			}

			// Apply DontDestroyOnLoad filter
			if (_showOnlyDontDestroyOnLoad)
			{
				services = services.Where(s => s.IsDontDestroyOnLoad).ToList();
			}

			// Apply scene filter
			if (_filterScene != "All")
			{
				services = services.Where(s => s.SceneName == _filterScene).ToList();
			}

			return services.OrderBy(s => s.ServiceType.Name).ToList();
		}

		private List<string> GetAvailableScenes(List<ServiceInfo> services)
		{
			var scenes = new List<string> { "All" };
			scenes.AddRange(services
				.Select(s => s.SceneName)
				.Distinct()
				.OrderBy(s => s));
			return scenes;
		}
	}
}
