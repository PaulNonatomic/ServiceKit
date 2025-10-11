using System;
using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit.Editor.Utils;
using UnityEngine.UIElements;

namespace Nonatomic.ServiceKit.Editor.ServiceKitWindow
{
	public class LocatorItem : VisualElement
	{
		private readonly ServiceKitLocator _locator;
		private readonly List<SceneItem> _sceneItems = new();
		private readonly VisualElement _servicesContainer;
		private readonly Label _headerLabel;
		private string _currentSearchText = string.Empty;
		private readonly Dictionary<string, bool> _sceneFoldoutStates = new();

		public LocatorItem(ServiceKitLocator locator, bool showHeader = true)
		{
			_locator = locator;
			AddToClassList("locator-item");

			_headerLabel = new Label(locator.name);
			_headerLabel.AddToClassList("locator-header");
			
			if (showHeader)
			{
				Add(_headerLabel);
			}

			_servicesContainer = new();
			_servicesContainer.AddToClassList("services-container");
			Add(_servicesContainer);

			// Refresh services initially
			RefreshServices();
		}

		public void SetHeaderVisibility(bool visible)
		{
			_headerLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void RefreshServices()
		{
			// Save the current foldout states before clearing
			SaveFoldoutStates();

			_servicesContainer.Clear();
			_sceneItems.Clear();
			var services = _locator.GetAllServices();

			if (services.Count == 0)
			{
				var emptyLabel = new Label("No services registered");
				emptyLabel.AddToClassList("no-services-message");
				_servicesContainer.Add(emptyLabel);
				return;
			}

			// Reset the item counter to ensure proper alternating pattern within each scene
			ServiceItem.ResetItemCounter();

			var servicesByScene = GroupServicesByScene(services);
			foreach (var sceneGroup in servicesByScene)
			{
				// Generate the same scene key used for tracking state
				var sceneKey = GetSceneKey(sceneGroup.SceneName, sceneGroup.IsUnloaded, sceneGroup.IsDontDestroyOnLoad);

				var sceneItem = new SceneItem(
					sceneGroup.SceneName,
					sceneGroup.IsUnloaded,
					sceneGroup.IsDontDestroyOnLoad);

				_sceneItems.Add(sceneItem);
				_servicesContainer.Add(sceneItem);

				// Restore the foldout state if it was previously saved
				RestoreFoldoutState(sceneItem, sceneKey);

				// Reset counter before each scene to ensure the pattern is consistent within each scene
				ServiceItem.ResetItemCounter();

				// Add services for this scene - pass the scene type for color consistency
				foreach (var serviceInfo in sceneGroup.Services)
				{
					var serviceItem = new ServiceItem(serviceInfo.ServiceType, serviceInfo.Service, sceneItem.GetSceneType(), serviceInfo.DebugData.State, serviceInfo.Tags);
					sceneItem.AddService(serviceItem);
				}
			}

			// Re-apply the current search if there is one
			if (!string.IsNullOrWhiteSpace(_currentSearchText))
			{
				ApplySearchFilter(_currentSearchText);
			}
		}

		/// <summary>
		///     Shows all scene items and service items.
		/// </summary>
		public void ShowAllItems()
		{
			_currentSearchText = string.Empty;

			foreach (var sceneItem in _sceneItems)
			{
				sceneItem.style.display = DisplayStyle.Flex;
				sceneItem.ShowAllServices();
			}
		}

		/// <summary>
		///     Applies a search filter to this locator's services.
		/// </summary>
		/// <param name="searchText">Text to search for</param>
		/// <returns>Number of matching services found</returns>
		public int ApplySearchFilter(string searchText)
		{
			_currentSearchText = searchText;

			if (string.IsNullOrWhiteSpace(searchText))
			{
				ShowAllItems();
				return _sceneItems.Sum(sceneItem => sceneItem.GetServiceCount());
			}

			var totalMatches = 0;

			// Apply filter to each scene
			foreach (var sceneItem in _sceneItems)
			{
				var matchesInScene = sceneItem.ApplySearchFilter(searchText);
				totalMatches += matchesInScene;

				// Hide scenes with no matches
				sceneItem.style.display = matchesInScene > 0
					? DisplayStyle.Flex
					: DisplayStyle.None;
			}

			return totalMatches;
		}

		private List<SceneGroupData> GroupServicesByScene(IReadOnlyList<ServiceInfo> services)
		{
			var result = new Dictionary<string, SceneGroupData>();

			foreach (var serviceInfo in services)
			{
				// Generate a key for the dictionary that differentiates between scene types
				string sceneKey;
				if (serviceInfo.DebugData.IsDontDestroyOnLoad)
				{
					// Special category for DontDestroyOnLoad
					sceneKey = "DONTDESTROY";
				}
				else if (IsServiceFromUnloadedScene(serviceInfo))
				{
					// Use the unloaded scene prefix to differentiate 
					sceneKey = $"{ServiceUtils.UnloadedScenePrefix}{serviceInfo.DebugData.SceneName}";
				}
				else
				{
					sceneKey = serviceInfo.DebugData.SceneName;
				}

				// Add to appropriate scene group
				if (!result.TryGetValue(sceneKey, out var sceneGroup))
				{
					sceneGroup = new()
					{
						SceneName = serviceInfo.DebugData.SceneName,
						IsUnloaded = IsServiceFromUnloadedScene(serviceInfo),
						IsDontDestroyOnLoad = serviceInfo.DebugData.IsDontDestroyOnLoad
					};
					result[sceneKey] = sceneGroup;
				}

				sceneGroup.Services.Add(serviceInfo);
			}

			// Ordering:
			// 1. "Non-MonoBehaviour" services first
			// 2. DontDestroyOnLoad services
			// 3. Regular loaded scenes (alphabetically)
			// 4. Unloaded scenes (alphabetically) at the end
			return result.Values
				.OrderBy(group =>
				{
					if (group.SceneName == "Non-MonoBehaviour")
					{
						return 1;
					}

					if (group.IsDontDestroyOnLoad)
					{
						return 2;
					}

					if (group.IsUnloaded)
					{
						return 4;
					}

					return 3; // Regular scenes
				})
				.ThenBy(group => group.SceneName) // Alphabetical ordering within each category
				.ToList();
		}

		private bool IsServiceFromUnloadedScene(ServiceInfo serviceInfo)
		{
			// Check if it's a MonoBehaviour that has been destroyed (likely from unloaded scene)
			if (serviceInfo.Service is UnityEngine.MonoBehaviour mb && mb == null)
			{
				return true;
			}

			// For ServiceKit, we can also check if the scene name doesn't match any currently loaded scene
			// and it's not "Non-MonoBehaviour" or "DontDestroyOnLoad"
			if (serviceInfo.DebugData.SceneName != "Non-MonoBehaviour" && !serviceInfo.DebugData.IsDontDestroyOnLoad)
			{
				for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
				{
					var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
					if (scene.name == serviceInfo.DebugData.SceneName)
					{
						return false; // Scene is loaded
					}
				}
				return true; // Scene not found in loaded scenes
			}

			return false;
		}

		private void SaveFoldoutStates()
		{
			foreach (var sceneItem in _sceneItems)
			{
				var sceneKey = GetSceneKey(sceneItem.SceneName, sceneItem.IsUnloaded, sceneItem.IsDontDestroyOnLoad);
				_sceneFoldoutStates[sceneKey] = sceneItem.GetFoldoutValue();
			}
		}

		private void RestoreFoldoutState(SceneItem sceneItem, string sceneKey)
		{
			if (_sceneFoldoutStates.TryGetValue(sceneKey, out var savedState))
			{
				sceneItem.SetFoldoutValue(savedState);
			}
		}

		private string GetSceneKey(string sceneName, bool isUnloaded, bool isDontDestroyOnLoad)
		{
			if (isDontDestroyOnLoad)
			{
				return "DONTDESTROY";
			}
			else if (isUnloaded)
			{
				return $"{ServiceUtils.UnloadedScenePrefix}{sceneName}";
			}
			else
			{
				return sceneName;
			}
		}
	}
}