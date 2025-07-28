#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nonatomic.ServiceKit.Editor.ServiceKitWindow
{
	/// <summary>
	///     Represents the Services tab in the ServiceKit Window.
	/// </summary>
	public class ServiceKitServicesTab : VisualElement
	{
		private readonly Button _clearSearchButton;
		private readonly List<LocatorItem> _locatorItems = new();
		private readonly Label _noResultsLabel;
		private readonly Action _refreshCallback;
		private readonly TextField _searchField;
		private readonly ScrollView _servicesScrollView;
		private readonly DropdownField _locatorDropdown;
		private readonly Button _showAllButton;
		private readonly Button _createButton;
		private bool _refreshPending;
		private List<ServiceKitLocator> _serviceKitLocators = new();
		private List<ServiceKitLocator> _orderedLocators = new(); // Ordered list that matches dropdown choices
		private ServiceKitLocator _selectedLocator;
		private List<string> _locatorChoices = new();
		private string _persistedLocatorGuid; // Store GUID instead of name for uniqueness
		private const string SELECTED_LOCATOR_PREF_KEY = "ServiceKit_SelectedLocatorGuid";
		private double _lastStateCheckTime;
		private const float STATE_CHECK_INTERVAL = 1.0f; // Check every 1 second
		private Dictionary<Type, string> _serviceStateCache = new();

		public ServiceKitServicesTab(Action refreshCallback)
		{
			_refreshCallback = refreshCallback;
			AddToClassList("services-tab");

			// Load persisted locator selection using GUID for uniqueness
			_persistedLocatorGuid = EditorPrefs.GetString(SELECTED_LOCATOR_PREF_KEY, "");

			// Title bar
			var titleBar = new VisualElement();
			titleBar.AddToClassList("services-title-bar");
			Add(titleBar);

			// Add header
			var headerLabel = new Label("Services");
			headerLabel.AddToClassList("services-header");
			titleBar.Add(headerLabel);

			// Add refresh button
			var refreshButton = new Button(RefreshServicesManually);
			refreshButton.tooltip = "Refresh service list";
			refreshButton.AddToClassList("refresh-button");
			titleBar.Add(refreshButton);

			var icon = new Image();
			icon.AddToClassList("button-icon");
			icon.image = Resources.Load<Texture2D>("ServiceKit/Icons/refresh");
			refreshButton.Add(icon);

			// Create the locator selection container
			var locatorContainer = new VisualElement();
			locatorContainer.AddToClassList("locator-selection-container");
			Add(locatorContainer);

			// Add locator selection dropdown
			_locatorDropdown = new DropdownField("ServiceKit Locator");
			_locatorDropdown.AddToClassList("locator-selection-dropdown");
			_locatorDropdown.RegisterValueChangedCallback(OnLocatorDropdownChanged);
			locatorContainer.Add(_locatorDropdown);

			// Add "Create New" button for when no locators exist
			var createButton = new Button(CreateNewLocator);
			createButton.text = "Create";
			createButton.tooltip = "Create a new ServiceKit Locator";
			createButton.AddToClassList("create-locator-button");
			createButton.style.display = DisplayStyle.None; // Hidden initially
			locatorContainer.Add(createButton);

			// Store reference to create button for later visibility control
			_createButton = createButton;

			// Create the search container
			var searchContainer = new VisualElement();
			searchContainer.AddToClassList("search-container");
			Add(searchContainer);

			// Add search field
			_searchField = new();
			_searchField.AddToClassList("search-field");
			
			// Set placeholder text based on Unity version
			#if UNITY_2022_3_OR_OLDER
			_searchField.placeholder = "Search services...";
			#elif UNITY_2023_1_OR_NEWER || UNITY_6_0_OR_NEWER
			_searchField.textEdition.placeholder = "Search services...";
			_searchField.textEdition.hidePlaceholderOnFocus = true;
			#endif
			
			_searchField.RegisterValueChangedCallback(OnSearchChanged);
			searchContainer.Add(_searchField);

			// Add clear button
			_clearSearchButton = new(ClearSearch);
			_clearSearchButton.AddToClassList("clear-search-button");
			_clearSearchButton.text = "×";
			_clearSearchButton.tooltip = "Clear search";
			_clearSearchButton.style.display = DisplayStyle.None; // Hidden initially
			searchContainer.Add(_clearSearchButton);

			// "No results" label (hidden initially)
			_noResultsLabel = new("No services match your search");
			_noResultsLabel.AddToClassList("no-results-message");
			_noResultsLabel.style.display = DisplayStyle.None;
			Add(_noResultsLabel);

			// Create and add the scroll view
			_servicesScrollView = new();
			Add(_servicesScrollView);

			RegisterCallback<AttachToPanelEvent>(HandleAttachToPanel);
			RegisterCallback<DetachFromPanelEvent>(HandleDetachFromPanel);

			RefreshServices();
		}

		private void HandleAttachToPanel(AttachToPanelEvent evt)
		{
			EditorApplication.hierarchyChanged += HandleHierarchyChanged;
			EditorApplication.update += CheckServiceStates;
		}

		private void HandleDetachFromPanel(DetachFromPanelEvent evt)
		{
			// Stop listening for editor scene changes
			EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
			// Stop periodic state checking
			EditorApplication.update -= CheckServiceStates;

			// Clean up other callbacks
			UnregisterCallback<AttachToPanelEvent>(HandleAttachToPanel);
			UnregisterCallback<DetachFromPanelEvent>(HandleDetachFromPanel);
		}

		private void HandleHierarchyChanged()
		{
			// This catches scene changes (loading/unloading)
			ScheduleRefresh();
		}
		
		private void CheckServiceStates()
		{
			// Only check periodically to avoid performance impact
			if (EditorApplication.timeSinceStartup - _lastStateCheckTime < STATE_CHECK_INTERVAL)
			{
				return;
			}
			
			_lastStateCheckTime = EditorApplication.timeSinceStartup;
			
			// Check if any service states have changed
			if (HasServiceStateChanged())
			{
				ScheduleRefresh();
			}
		}
		
		private bool HasServiceStateChanged()
		{
			if (_selectedLocator == null)
			{
				return false;
			}
			
			var allServices = _selectedLocator.GetAllServices();
			var stateChanged = false;
			
			// Build current state map
			var currentStates = new Dictionary<Type, string>();
			foreach (var service in allServices)
			{
				currentStates[service.ServiceType] = service.State;
			}
			
			// Check for changes
			foreach (var kvp in currentStates)
			{
				if (_serviceStateCache.TryGetValue(kvp.Key, out var cachedState))
				{
					if (cachedState != kvp.Value)
					{
						stateChanged = true;
						break;
					}
				}
				else
				{
					// New service
					stateChanged = true;
					break;
				}
			}
			
			// Check for removed services
			if (!stateChanged)
			{
				foreach (var cachedType in _serviceStateCache.Keys)
				{
					if (!currentStates.ContainsKey(cachedType))
					{
						stateChanged = true;
						break;
					}
				}
			}
			
			return stateChanged;
		}

		/// <summary>
		///     Schedules a refresh to happen on the next editor update.
		/// </summary>
		private void ScheduleRefresh()
		{
			if (_refreshPending)
			{
				return;
			}

			_refreshPending = true;
			EditorApplication.delayCall += () =>
			{
				// Check if the tab is still attached to a panel
				if (panel == null)
				{
					_refreshPending = false;
					return;
				}

				RefreshServices();
				_refreshPending = false;
			};
		}

		/// <summary>
		///     Manually triggered refresh from the UI button.
		/// </summary>
		private void RefreshServicesManually()
		{
			RefreshServices();

			// Also call the window's refresh callback
			_refreshCallback?.Invoke();
		}

		/// <summary>
		///     Refreshes the displayed services.
		/// </summary>
		public void RefreshServices()
		{
			if (_servicesScrollView == null)
			{
				return;
			}

			_servicesScrollView.Clear();
			_locatorItems.Clear();

			// Force asset database refresh to find any new ServiceKitLocator assets
			AssetDatabase.Refresh();

			// Find all ServiceKitLocator assets
			_serviceKitLocators = FindServiceKitLocatorAssets();

			// Update dropdown choices
			UpdateDropdownChoices();

			// Determine which locators to display
			var locatorsToDisplay = _selectedLocator != null 
				? new List<ServiceKitLocator> { _selectedLocator }
				: _serviceKitLocators; // Show all if no specific selection

			// Create UI for each service locator
			foreach (var locator in locatorsToDisplay)
			{
				// Always show headers when displaying multiple locators, hide when showing single
				var showHeader = locatorsToDisplay.Count > 1;
				var locatorItem = new LocatorItem(locator, showHeader);
				_locatorItems.Add(locatorItem);
				_servicesScrollView.contentContainer.Add(locatorItem);
			}

			// Update UI state
			UpdateUIState();

			// Apply any existing search filter
			if (!string.IsNullOrWhiteSpace(_searchField.value))
			{
				ApplySearchFilter(_searchField.value);
			}
			
			// Update service state cache
			UpdateServiceStateCache();
		}
		
		private void UpdateServiceStateCache()
		{
			_serviceStateCache.Clear();
			
			if (_selectedLocator != null)
			{
				var allServices = _selectedLocator.GetAllServices();
				foreach (var service in allServices)
				{
					_serviceStateCache[service.ServiceType] = service.State;
				}
			}
		}

		private void UpdateDropdownChoices()
		{
			_locatorChoices.Clear();
			_orderedLocators.Clear();

			if (_serviceKitLocators.Count == 0)
			{
				_locatorChoices.Add("No ServiceKit Locators found");
				_locatorDropdown.choices = _locatorChoices;
				_locatorDropdown.value = _locatorChoices[0];
				_locatorDropdown.SetEnabled(false);
				_createButton.style.display = DisplayStyle.Flex;
				_selectedLocator = null;
				return;
			}

			// First, create the ordered list
			_orderedLocators = _serviceKitLocators.OrderBy(l => l.name).ToList();
			
			// Then create display names for the ordered list
			var nameGroups = _orderedLocators.GroupBy(l => l.name).ToList();
			
			for (int i = 0; i < _orderedLocators.Count; i++)
			{
				var locator = _orderedLocators[i];
				var group = nameGroups.First(g => g.Key == locator.name);
				
				if (group.Count() > 1)
				{
					// Multiple locators with same name - show path
					var path = AssetDatabase.GetAssetPath(locator);
					var folderPath = System.IO.Path.GetDirectoryName(path)?.Replace("Assets/", "") ?? "";
					_locatorChoices.Add($"{locator.name} ({folderPath})");
				}
				else
				{
					_locatorChoices.Add(locator.name);
				}
			}

			_locatorDropdown.choices = _locatorChoices;
			_locatorDropdown.SetEnabled(true);
			_createButton.style.display = DisplayStyle.None;

			// Restore selection from persisted name or select first locator
			RestoreOrSelectFirstLocator();
		}

		private void RestoreOrSelectFirstLocator()
		{
			ServiceKitLocator locatorToSelect = null;

			// Try to restore from persisted GUID first
			if (!string.IsNullOrEmpty(_persistedLocatorGuid))
			{
				locatorToSelect = _orderedLocators.FirstOrDefault(l => 
					AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(l)) == _persistedLocatorGuid);
			}

			// If no persisted selection or locator not found, select first available
			if (locatorToSelect == null && _orderedLocators.Count > 0)
			{
				locatorToSelect = _orderedLocators[0];
			}

			if (locatorToSelect != null)
			{
				_selectedLocator = locatorToSelect;
				_persistedLocatorGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(locatorToSelect));

				// Set dropdown value using the ordered list
				var selectedIndex = _orderedLocators.IndexOf(locatorToSelect);
				if (selectedIndex >= 0 && selectedIndex < _locatorChoices.Count)
				{
					_locatorDropdown.value = _locatorChoices[selectedIndex];
				}
			}
			else
			{
				_selectedLocator = null;
				_persistedLocatorGuid = null;
			}
		}

		private void UpdateUIState()
		{
			// No need for separate "Show All" button anymore since it's in the dropdown
		}

		private void OnLocatorDropdownChanged(ChangeEvent<string> evt)
		{
			var selectedValue = evt.newValue;
			if (selectedValue == "No ServiceKit Locators found")
			{
				_selectedLocator = null;
				_persistedLocatorGuid = null;
				EditorPrefs.DeleteKey(SELECTED_LOCATOR_PREF_KEY);
				return;
			}

			// Find the locator using the ordered list that matches dropdown choices
			var selectedIndex = _locatorChoices.IndexOf(selectedValue);
			if (selectedIndex >= 0 && selectedIndex < _orderedLocators.Count)
			{
				_selectedLocator = _orderedLocators[selectedIndex];
				_persistedLocatorGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_selectedLocator));
				EditorPrefs.SetString(SELECTED_LOCATOR_PREF_KEY, _persistedLocatorGuid);
			}
			else
			{
				_selectedLocator = null;
				_persistedLocatorGuid = null;
				EditorPrefs.DeleteKey(SELECTED_LOCATOR_PREF_KEY);
				Debug.LogWarning($"ServiceKit Window: Could not find locator for index {selectedIndex}");
			}

			RefreshServices();
		}

		private void CreateNewLocator()
		{
			var newLocator = ScriptableObjectUtils.CreateInstanceInProject<ServiceKitLocator>(selectInstance: false);
			if (newLocator != null)
			{
				Debug.Log($"Created new ServiceKitLocator: {newLocator.name}");
				RefreshServices();
			}
		}

		private void ShowAllLocators()
		{
			_selectedLocator = null;
			_persistedLocatorGuid = null;
			EditorPrefs.DeleteKey(SELECTED_LOCATOR_PREF_KEY);
			// Update dropdown to show first locator or handle empty state
			UpdateDropdownChoices();
		}

		/// <summary>
		///     Finds all ServiceKitLocator assets in the project.
		/// </summary>
		private static List<ServiceKitLocator> FindServiceKitLocatorAssets()
		{
			return AssetUtils.FindAssetsByType<ServiceKitLocator>();
		}

		/// <summary>
		///     Handles search field value changes.
		/// </summary>
		private void OnSearchChanged(ChangeEvent<string> evt)
		{
			var searchText = evt.newValue;

			// Show/hide clear button based on whether there's search text
			_clearSearchButton.style.display = string.IsNullOrWhiteSpace(searchText)
				? DisplayStyle.None
				: DisplayStyle.Flex;

			ApplySearchFilter(searchText);
		}

		/// <summary>
		///     Clears the search field.
		/// </summary>
		private void ClearSearch()
		{
			_searchField.value = string.Empty;
			// ApplySearchFilter will be called automatically by the value changed callback
		}

		/// <summary>
		///     Applies the search filter to all service items.
		/// </summary>
		private void ApplySearchFilter(string searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				// Show all items when search is empty
				foreach (var locatorItem in _locatorItems)
				{
					locatorItem.ShowAllItems();
				}

				_noResultsLabel.style.display = DisplayStyle.None;
				return;
			}

			var totalMatchCount = 0;

			// Apply filter to each locator item
			foreach (var locatorItem in _locatorItems)
			{
				var matchCount = locatorItem.ApplySearchFilter(searchText);
				totalMatchCount += matchCount;
			}

			// Show "no results" message if needed
			_noResultsLabel.style.display = totalMatchCount > 0
				? DisplayStyle.None
				: DisplayStyle.Flex;
		}
	}
}