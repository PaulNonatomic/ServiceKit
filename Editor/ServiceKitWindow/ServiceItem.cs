using System;
using System.Collections.Generic;
using System.Linq;
using Nonatomic.ServiceKit.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nonatomic.ServiceKit.Editor.ServiceKitWindow
{
	public class ServiceItem : VisualElement
	{
		// Static counter to track item position for alternating colors
		private static int _itemCounter;
		private readonly Label _serviceLabel;
		private readonly Texture2D _hoverIcon;

		// References to the icons
		private readonly Image _icon;
		private readonly Image _infiniteIcon;
		private readonly Color _iconColor;
		private readonly Texture2D _normalIcon;

		public ServiceItem(Type serviceType, object serviceInstance, SceneType sceneType = SceneType.Regular, string state = "Ready", List<ServiceTag> tags = null, ServiceKitLocator locator = null)
		{
			// Store the service type name for searching
			ServiceTypeName = serviceType.Name;
			Tags = tags ?? new List<ServiceTag>();

			// Add the base service-item class
			AddToClassList("service-item");

			// Determine if service is ready
			var isReady = state == "Ready";

			// Circular-dependency state is owned by the locator that holds the service.
			var isExempt = locator != null && locator.IsServiceCircularDependencyExempt(serviceType);
			var hasCircularDependencyError = locator != null && locator.HasCircularDependencyError(serviceType);
			
			// Add state class
			if (isReady)
			{
				AddToClassList("service-item-ready");
			}
			else
			{
				AddToClassList("service-item-not-ready");
			}
			
			// Add exemption class
			if (isExempt)
			{
				AddToClassList("service-item-exempt");
			}
			
			// Add error class
			if (hasCircularDependencyError)
			{
				AddToClassList("service-item-circular-error");
			}
			
			// Add scene type class
			AddSceneTypeClass(sceneType);

			// Add alternating background class (even/odd)
			if (_itemCounter % 2 == 0)
			{
				AddToClassList("service-item-even");
			}
			else
			{
				AddToClassList("service-item-odd");
			}

			// Increment the counter for the next item
			_itemCounter++;

			var container = new VisualElement();
			container.AddToClassList("service-item-container");
			Add(container);

			// Load both icon textures upfront
			_normalIcon = Resources.Load<Texture2D>("ServiceKit/Icons/circle");
			_hoverIcon = Resources.Load<Texture2D>("ServiceKit/Icons/circle-fill");

			// Create the icon image element
			_icon = new();
			_icon.AddToClassList("service-icon");
			_icon.image = _normalIcon; // Start with normal icon

			// Store the color to use for both states
			_iconColor = GetColorForSceneType(sceneType);

			// Set initial tint color only if ready (for code compatibility)
			// USS will handle the actual styling
			if (isReady)
			{
				_icon.tintColor = _iconColor;
			}
			else
			{
				_icon.tintColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
			}

			container.Add(_icon);

			_serviceLabel = new(ServiceTypeName);
			_serviceLabel.AddToClassList("service-label");
			container.Add(_serviceLabel);


			var buttonsContainer = new VisualElement();
			buttonsContainer.AddToClassList("service-edit-btn-container");
			container.Add(buttonsContainer);

			// Add tags container if there are tags (positioned right-aligned before infinity icon)
			if (Tags != null && Tags.Count > 0)
			{
				var tagsContainer = new VisualElement();
				tagsContainer.AddToClassList("service-tags-container");
				
				foreach (var tag in Tags)
				{
					var tagBadge = new Label(tag.name);
					tagBadge.AddToClassList("service-tag-badge");
					
					// Check if tag has custom styling
					if (tag.HasCustomStyle)
					{
						// Apply custom colors from the tag
						tagBadge.style.backgroundColor = new StyleColor(tag.backgroundColor.Value);
						
						// Use custom text color if specified, otherwise auto-calculate
						if (tag.textColor.HasValue)
						{
							tagBadge.style.color = new StyleColor(tag.textColor.Value);
						}
						else
						{
							// Auto-calculate text color based on background brightness
							var bg = tag.backgroundColor.Value;
							float brightness = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
							var textColor = brightness > 0.5f ? Color.black : Color.white;
							tagBadge.style.color = new StyleColor(textColor);
						}
					}
					else
					{
						// Fall back to CSS class-based styling for predefined tags
						var tagClass = tag.name.Replace(" ", "-").ToLower();
						tagBadge.AddToClassList($"tag-{tagClass}");
					}
					
					tagsContainer.Add(tagBadge);
				}
				
				buttonsContainer.Add(tagsContainer);
			}

			// Hint: this service is registered under its concrete type but implements one or more
			// user-defined interfaces that are not themselves registered. The author may have meant
			// to register it as the interface (e.g. forgot [Service(typeof(IFoo))], or placed it on
			// an abstract base, where it has no effect because [Service] is not inherited).
			var interfaceHints = GetUnregisteredInterfaceHints(serviceType, locator);
			if (interfaceHints.Count > 0)
			{
				var hintBadge = new Label("i");
				hintBadge.AddToClassList("service-interface-hint");
				hintBadge.tooltip = BuildInterfaceHintTooltip(serviceType, interfaceHints);
				hintBadge.style.width = 14f;
				hintBadge.style.height = 14f;
				hintBadge.style.marginRight = 5f;
				hintBadge.style.fontSize = 10f;
				hintBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
				hintBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
				hintBadge.style.color = new StyleColor(Color.white);
				hintBadge.style.backgroundColor = new StyleColor(new Color(1.0f, 0.76f, 0.03f)); // #FFC107 amber
				hintBadge.style.borderTopLeftRadius = 7f;
				hintBadge.style.borderTopRightRadius = 7f;
				hintBadge.style.borderBottomLeftRadius = 7f;
				hintBadge.style.borderBottomRightRadius = 7f;
				buttonsContainer.Add(hintBadge);
			}

			// Create the infinite icon image element (positioned before the pencil button)
			_infiniteIcon = new();
			_infiniteIcon.AddToClassList("infinite-icon");
			
			// Set icon, color and tooltip based on error status first, then exemption status
			if (hasCircularDependencyError)
			{
				// Error icon for services with circular dependency errors
				_infiniteIcon.image = Resources.Load<Texture2D>("ServiceKit/Icons/error");
				_infiniteIcon.tintColor = Color.white;
				_infiniteIcon.tooltip = "This service has a circular dependency error";
			}
			else if (isExempt)
			{
				// Infinite icon, grey for exempt services
				_infiniteIcon.image = Resources.Load<Texture2D>("ServiceKit/Icons/infinite");
				_infiniteIcon.tintColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
				_infiniteIcon.tooltip = "Circular dependency detection is disabled for this service";
			}
			else
			{
				// Infinite icon, colored for non-exempt services (matches service color)
				_infiniteIcon.image = Resources.Load<Texture2D>("ServiceKit/Icons/infinite");
				_infiniteIcon.tintColor = _iconColor;
				_infiniteIcon.tooltip = "Circular dependency detection is enabled for this service";
			}
			
			buttonsContainer.Add(_infiniteIcon);

			// Add Open Script button
			var openButton = new Button(() => OpenScriptInIDE(serviceType));
			openButton.AddToClassList("open-script-button");
			buttonsContainer.Add(openButton);

			var openIcon = new Image();
			openIcon.AddToClassList("open-script-icon");
			openIcon.image = Resources.Load<Texture2D>("ServiceKit/Icons/pencil");
			openButton.Add(openIcon);

			// Register mouse hover events at the container level for better UX
			RegisterCallbacks(container, serviceInstance, isReady);
		}

		// The type name for search purposes
		public string ServiceTypeName { get; }
		
		// Tags associated with this service
		public List<ServiceTag> Tags { get; }

		private void RegisterCallbacks(VisualElement container, object serviceInstance, bool isReady)
		{
			// Handle mouse enter (hover start)
			container.RegisterCallback<MouseEnterEvent>(evt =>
			{
				// Swap to the hover icon and maintain color
				_icon.image = _hoverIcon;
				if (isReady)
				{
					_icon.tintColor = _iconColor;
				}
				else
				{
					_icon.tintColor = new Color(0.5f, 0.5f, 0.5f, 1.0f); // Brighter grey on hover
				}
			});

			// Handle mouse leave (hover end)
			container.RegisterCallback<MouseLeaveEvent>(evt =>
			{
				// Switch back to normal icon and maintain color
				_icon.image = _normalIcon;
				if (isReady)
				{
					_icon.tintColor = _iconColor;
				}
				else
				{
					_icon.tintColor = new Color(0.5f, 0.5f, 0.5f, 0.8f); // Back to normal grey
				}
			});

			container.RegisterCallback<ClickEvent>(evt =>
			{
				if (serviceInstance is MonoBehaviour monoBehaviour)
				{
					PingGameObject(monoBehaviour);
				}
			});
		}

		/// <summary>
		///     Gets a color matching the scene type.
		/// </summary>
		private Color GetColorForSceneType(SceneType sceneType)
		{
			return sceneType switch
			{
				SceneType.NoScene           => new(0.129f, 0.588f, 0.953f), // #2196F3 Blue
				SceneType.DontDestroyOnLoad => new(0.298f, 0.686f, 0.314f), // #4CAF50 Green
				SceneType.Unloaded          => new(1.0f, 0.42f, 0.42f), // #FF6B6B Red
				SceneType.Regular           => new(1.0f, 0.596f, 0.0f), // #FF9800 Orange
				_                           => Color.white
			};
		}
		
		/// <summary>
		///     Adds CSS class based on scene type for styling.
		/// </summary>
		private void AddSceneTypeClass(SceneType sceneType)
		{
			var className = sceneType switch
			{
				SceneType.NoScene           => "service-item-no-scene",
				SceneType.DontDestroyOnLoad => "service-item-dont-destroy",
				SceneType.Unloaded          => "service-item-unloaded",
				SceneType.Regular           => "service-item-regular",
				_                           => "service-item-regular"
			};
			AddToClassList(className);
		}

		// Method to reset the counter when refreshing the UI
		public static void ResetItemCounter()
		{
			_itemCounter = 0;
		}

		/// <summary>
		///     Returns the user-defined interfaces implemented by a service that is registered under its
		///     concrete type, excluding any interface that is itself registered in the locator. A non-empty
		///     result usually means a missing <c>[Service(typeof(IFoo))]</c> (or one placed on an abstract
		///     base, where it has no effect because the attribute is not inherited).
		/// </summary>
		private static List<Type> GetUnregisteredInterfaceHints(Type serviceType, ServiceKitLocator locator)
		{
			var result = new List<Type>();
			if (serviceType == null || serviceType.IsInterface)
			{
				return result;
			}

			var interfaces = serviceType.GetInterfaces();
			if (interfaces.Length == 0)
			{
				return result;
			}

			// Interfaces already registered (under their own key) in this locator are intentional.
			var registered = new HashSet<Type>();
			if (locator != null)
			{
				foreach (var info in locator.GetAllServices())
				{
					if (info?.ServiceType != null)
					{
						registered.Add(info.ServiceType);
					}
				}
			}

			foreach (var iface in interfaces)
			{
				if (IsUserInterface(iface) && !registered.Contains(iface))
				{
					result.Add(iface);
				}
			}

			// Keep only the most-derived interfaces (drop a base that another listed interface extends).
			return result.Where(i => !result.Any(other => other != i && i.IsAssignableFrom(other))).ToList();
		}

		/// <summary>
		///     Heuristic: treat interfaces outside framework namespaces as user code worth hinting about.
		/// </summary>
		private static bool IsUserInterface(Type iface)
		{
			var ns = iface.Namespace ?? string.Empty;
			if (ns.Length == 0)
			{
				return true; // global namespace — assume user code
			}

			return !(ns == "System" || ns.StartsWith("System.")
				|| ns == "UnityEngine" || ns.StartsWith("UnityEngine.")
				|| ns == "UnityEditor" || ns.StartsWith("UnityEditor.")
				|| ns.StartsWith("Unity.")
				|| ns.StartsWith("Cysharp.")
				|| ns.StartsWith("Mono.")
				|| ns.StartsWith("Microsoft."));
		}

		private static string BuildInterfaceHintTooltip(Type serviceType, List<Type> interfaceHints)
		{
			if (interfaceHints.Count == 1)
			{
				var iface = interfaceHints[0].Name;
				return $"Registered as concrete type '{serviceType.Name}', which implements '{iface}'.\n" +
				       $"If consumers inject '{iface}', register it with [Service(typeof({iface}))] on the concrete class.";
			}

			var names = string.Join(", ", interfaceHints.Select(t => t.Name));
			return $"Registered as concrete type '{serviceType.Name}', which implements {names}.\n" +
			       "If consumers inject any of these interfaces, register it with [Service(typeof(...))] on the concrete class.";
		}

		private static void PingGameObject(MonoBehaviour monoBehaviour)
		{
			Selection.activeGameObject = monoBehaviour.gameObject;
			EditorGUIUtility.PingObject(monoBehaviour.gameObject);
		}

		private void OpenScriptInIDE(Type type)
		{
			var script = ScriptFindingUtils.FindScriptForType(type);

			if (script != null)
			{
				AssetDatabase.OpenAsset(script);
				return;
			}

			// If not found, try to show a selection dialog
			var potentialScripts = ScriptFindingUtils.FindPotentialScriptsForType(type);

			if (potentialScripts.Count > 0)
			{
				// If there's only one potential script, just open it
				if (potentialScripts.Count == 1)
				{
					AssetDatabase.OpenAsset(potentialScripts[0]);
					return;
				}

				// Otherwise show a selection menu
				var menu = new GenericMenu();
				foreach (var potentialScript in potentialScripts)
				{
					var path = AssetDatabase.GetAssetPath(potentialScript);
					var displayPath = path.Replace("Assets/", "");

					menu.AddItem(new(displayPath), false, () => { AssetDatabase.OpenAsset(potentialScript); });
				}

				menu.ShowAsContext();
				return;
			}

			// No scripts found
			EditorUtility.DisplayDialog("Script Not Found",
				$"Could not find the script file for type {type.Name}.", "OK");
		}

		/// <summary>
		///     Checks if this service matches the search text using fuzzy matching.
		///     Searches both service name and tags.
		/// </summary>
		public bool MatchesSearch(string searchText)
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return true;
			}

			// First check service type name
			if (FuzzyMatch(ServiceTypeName, searchText))
			{
				return true;
			}

			// Then check all tags
			foreach (var tag in Tags)
			{
				if (FuzzyMatch(tag.name, searchText))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		///     Performs a fuzzy match between the service name and search text.
		/// </summary>
		private bool FuzzyMatch(string serviceName, string searchText)
		{
			// Convert both strings to lowercase for case-insensitive matching
			var lowerServiceName = serviceName.ToLowerInvariant();
			var lowerSearchText = searchText.ToLowerInvariant();

			// Simple contains check first (most common case)
			if (lowerServiceName.Contains(lowerSearchText))
			{
				return true;
			}

			// More sophisticated fuzzy matching - check if the characters appear in order
			var serviceIndex = 0;
			var searchIndex = 0;

			while (serviceIndex < lowerServiceName.Length && searchIndex < lowerSearchText.Length)
			{
				if (lowerServiceName[serviceIndex] == lowerSearchText[searchIndex])
				{
					searchIndex++;
				}

				serviceIndex++;
			}

			// If we matched all characters in the search text
			return searchIndex == lowerSearchText.Length;
		}
	}
}