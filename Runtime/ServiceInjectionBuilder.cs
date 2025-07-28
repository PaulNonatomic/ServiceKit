using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	public class ServiceInjectionBuilder : IServiceInjectionBuilder
	{
		private readonly IServiceKitLocator _serviceKitLocator;
		private readonly object _target;
		private readonly Type _targetServiceType;
		private CancellationToken _cancellationToken = CancellationToken.None;
		private float _timeout = -1f;
		private Action<Exception> _errorHandler;

		// Static dependency tracking for circular dependency detection
		private static readonly Dictionary<Type, DependencyNode> _dependencyGraph = new Dictionary<Type, DependencyNode>();
		private static readonly Dictionary<Type, CancellationTokenSource> _resolvingCancellations = new Dictionary<Type, CancellationTokenSource>();
		private static readonly HashSet<Type> _circularExemptServices = new HashSet<Type>();
		private static readonly HashSet<Type> _servicesWithCircularDependencyErrors = new HashSet<Type>();
		private static readonly object _graphLock = new object();

		private class DependencyNode
		{
			public Type ServiceType { get; set; }
			public HashSet<Type> Dependencies { get; set; } = new HashSet<Type>();
			public Dictionary<Type, string> DependencyFields { get; set; } = new Dictionary<Type, string>();
			public bool IsResolving { get; set; }
			public List<Type> ResolutionPath { get; set; } = new List<Type>();
		}

		private class CircularDependencyInfo
		{
			public string Path { get; set; }
			public string DetailedPath { get; set; }
			public Type FromType { get; set; }
			public Type ToType { get; set; }
			public string FieldName { get; set; }
		}

		internal ServiceInjectionBuilder(IServiceKitLocator serviceKitLocator, object target)
		{
			_serviceKitLocator = serviceKitLocator;
			_target = target;
			
			// Try to determine the service type this target provides
			_targetServiceType = DetermineServiceType(target);
		}

		private Type DetermineServiceType(object target)
		{
			// For ServiceKitBehaviour<T>, extract T
			var targetType = target.GetType();
			var baseType = targetType.BaseType;
			
			while (baseType != null)
			{
				if (baseType.IsGenericType && 
					baseType.GetGenericTypeDefinition().Name.StartsWith("ServiceKitBehaviour"))
				{
					return baseType.GetGenericArguments()[0];
				}
				baseType = baseType.BaseType;
			}
			
			// Fallback to the target's type
			return targetType;
		}

		public IServiceInjectionBuilder WithCancellation(CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
			return this;
		}

		public IServiceInjectionBuilder WithTimeout()
		{
			_timeout = ServiceKitSettings.Instance.DefaultTimeout;
			return this;
		}

		public IServiceInjectionBuilder WithTimeout(float timeoutSeconds)
		{
			_timeout = timeoutSeconds;
			return this;
		}

		public IServiceInjectionBuilder WithErrorHandling(Action<Exception> errorHandler)
		{
			_errorHandler = errorHandler;
			return this;
		}

		public IServiceInjectionBuilder WithErrorHandling()
		{
			_errorHandler = DefaultErrorHandler;
			return this;
		}

		public async void Execute()
		{
			try
			{
				await ExecuteAsync();
			}
			catch (Exception ex)
			{
				_errorHandler?.Invoke(ex);
			}
		}

		public async Task ExecuteAsync()
		{
			var targetType = _target.GetType();
			var fieldsToInject = GetFieldsToInject(targetType);

			if (fieldsToInject.Count == 0)
			{
				return;
			}

			// Create a cancellation token source for this resolution
			CancellationTokenSource resolutionCts = new CancellationTokenSource();
			
			lock (_graphLock)
			{
				_resolvingCancellations[_targetServiceType] = resolutionCts;
			}

			try
			{
				// Update dependency graph
				UpdateDependencyGraph(fieldsToInject);

				// Mark as resolving early
				lock (_graphLock)
				{
					if (_dependencyGraph.TryGetValue(_targetServiceType, out var node))
					{
						node.IsResolving = true;
					}
				}

				// Check for circular dependencies before starting any async operations
				var circularDependency = DetectCircularDependency();
				if (circularDependency != null)
				{
					// Cancel all services in the circular dependency chain
					CancelCircularDependencyChain(circularDependency.Path);
					
					// Build complete error message with field details
					var errorMessage = $"Circular dependency detected: {circularDependency.Path}";
					
					// Extract types from the path
					var types = circularDependency.Path.Split(new[] { " → " }, StringSplitOptions.None);
					if (types.Length > 1)
					{
						errorMessage += "\n\nCircular dependency chain:";
						
						lock (_graphLock)
						{
							for (int i = 0; i < types.Length - 1; i++)
							{
								var fromTypeName = types[i].Trim();
								var toTypeName = types[i + 1].Trim();
								
								// Find the field that creates this dependency
								var fieldFound = false;
								foreach (var graphEntry in _dependencyGraph)
								{
									if (graphEntry.Key.Name == fromTypeName)
									{
										foreach (var dependency in graphEntry.Value.Dependencies)
										{
											if (dependency.Name == toTypeName)
											{
												if (graphEntry.Value.DependencyFields.TryGetValue(dependency, out var fieldName))
												{
													errorMessage += $"\n  → {fromTypeName} has field '{fieldName}' that requires {toTypeName}";
													fieldFound = true;
													break;
												}
											}
										}
										if (fieldFound) break;
									}
								}
								
								if (!fieldFound)
								{
									errorMessage += $"\n  → {fromTypeName} requires {toTypeName}";
								}
							}
						}
					}
					
					// Mark all services in the circular dependency as having errors
					var typesInPath = circularDependency.Path.Split(new[] { " → " }, StringSplitOptions.None);
					foreach (var typeName in typesInPath)
					{
						var trimmedTypeName = typeName.Trim();
						// Find the type in the dependency graph and mark it as having an error
						foreach (var graphEntry in _dependencyGraph)
						{
							if (graphEntry.Key.Name == trimmedTypeName)
							{
								AddCircularDependencyError(graphEntry.Key);
								break;
							}
						}
					}
					
					throw new ServiceInjectionException(errorMessage);
				}

				// Create timeout cancellation if needed
				CancellationTokenSource timeoutCts = null;
				IDisposable timeoutRegistration = null;
				if (_timeout > 0)
				{
					timeoutCts = new CancellationTokenSource();
					timeoutRegistration = ServiceKitTimeoutManager.Instance.RegisterTimeout(timeoutCts, _timeout);
				}

				using (timeoutRegistration)
				using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
					_cancellationToken, 
					resolutionCts.Token,
					timeoutCts?.Token ?? CancellationToken.None))
				{
					var finalToken = linkedCts.Token;

					// Capture Unity thread context
					var unityContext = SynchronizationContext.Current;

					try
					{
						// Create tasks for all service retrievals
						var serviceTasks = fieldsToInject.Select<FieldInfo, Task<(FieldInfo fieldInfo, object service, bool Required)>>(async fieldInfo =>
						{
							var attribute = fieldInfo.GetCustomAttribute<InjectServiceAttribute>();
							var serviceType = fieldInfo.FieldType;

							// Check if we've been cancelled due to circular dependency
							finalToken.ThrowIfCancellationRequested();

							// For optional services, try to get them immediately without waiting
							if (!attribute.Required)
							{
								var locator = _serviceKitLocator as ServiceKitLocator;
								if (locator != null && locator.IsServiceReady(serviceType))
								{
									object optionalService = _serviceKitLocator.GetService(serviceType);
									return (fieldInfo, optionalService, attribute.Required);
								}
								return (fieldInfo, null, attribute.Required);
							}

							// For required services, wait for them to become available
							try
							{
								object requiredService = await _serviceKitLocator.GetServiceAsync(serviceType, finalToken);
								return (fieldInfo, requiredService, attribute.Required);
							}
							catch (OperationCanceledException) when (resolutionCts.IsCancellationRequested)
							{
								// This was cancelled due to circular dependency
								// Mark the service as having a circular dependency error
								AddCircularDependencyError(serviceType);
								AddCircularDependencyError(_targetServiceType);
								
								throw new ServiceInjectionException($"Injection cancelled due to circular dependency involving {serviceType.Name}");
							}
						}).ToList();

						// Wait for all services
						var results = await Task.WhenAll(serviceTasks);

						// Check for missing required services
						var missingRequiredServices = results
							.Where(r => r.service == null && r.Required)
							.Select(r => r.fieldInfo.FieldType.Name)
							.ToList();

						if (missingRequiredServices.Any())
						{
							throw new ServiceInjectionException(
								$"Required services not available: {string.Join(", ", missingRequiredServices)}");
						}

						// Switch back to Unity thread for injection
						await SwitchToUnityThread(unityContext);

						// Inject all services (including null for optional services)
						foreach ((var field, object service, bool _) in results)
						{
							field.SetValue(_target, service);
						}
					}
					catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
					{
						var requiredFields = fieldsToInject.Where(f => f.GetCustomAttribute<InjectServiceAttribute>().Required);
						var missingServices = requiredFields
							.Where(f => _serviceKitLocator.GetService(f.FieldType) == null)
							.Select(f => f.FieldType.Name)
							.ToList();

						string message = $"Service injection timed out after {_timeout} seconds for target '{_target.GetType().Name}'.";
						if (missingServices.Any())
						{
							message += $" Missing required services: {string.Join(", ", missingServices)}.";
						}

						// Check if timeout is due to circular dependency
						var circularDep = DetectCircularDependency();
						if (circularDep != null)
						{
							message += $"\n\nCircular dependency detected: {circularDep.Path}";
						}

						throw new TimeoutException(message);
					}
				}
			}
			finally
			{
				// Clean up
				lock (_graphLock)
				{
					if (_dependencyGraph.TryGetValue(_targetServiceType, out var node))
					{
						node.IsResolving = false;
					}
					
					_resolvingCancellations.Remove(_targetServiceType);
				}
				
				resolutionCts?.Dispose();
			}
		}

		private void CancelCircularDependencyChain(string circularDependencyPath)
		{
			// Extract just the type names from the path (before any field details)
			var pathParts = circularDependencyPath.Split('\n')[0]; // Get first line only
			var typeNames = pathParts.Split(new[] { " → " }, StringSplitOptions.None);
			
			lock (_graphLock)
			{
				// Cancel all resolving services in the chain
				foreach (var kvp in _resolvingCancellations.ToList())
				{
					if (typeNames.Contains(kvp.Key.Name))
					{
						try
						{
							kvp.Value.Cancel();
						}
						catch { }
					}
				}
			}
		}

		private void UpdateDependencyGraph(List<FieldInfo> fieldsToInject)
		{
			lock (_graphLock)
			{
				// Use the service type (interface), not the concrete type
				var serviceType = _targetServiceType;
				
				if (!_dependencyGraph.ContainsKey(serviceType))
				{
					_dependencyGraph[serviceType] = new DependencyNode
					{
						ServiceType = serviceType
					};
				}

				var node = _dependencyGraph[serviceType];
				node.Dependencies.Clear();
				node.DependencyFields.Clear();

				foreach (var field in fieldsToInject)
				{
					var attribute = field.GetCustomAttribute<InjectServiceAttribute>();
					// Track ALL dependencies, not just required ones, for circular dependency detection
					node.Dependencies.Add(field.FieldType);
					node.DependencyFields[field.FieldType] = field.Name;
				}
			}
		}

		private CircularDependencyInfo DetectCircularDependency()
		{
			lock (_graphLock)
			{
				var path = new List<Type>();
				var circularInfo = new CircularDependencyInfo();
				
				if (HasCircularDependency(_targetServiceType, path, circularInfo))
				{
					// Format the circular dependency path
					circularInfo.Path = string.Join(" → ", path.Select(t => t.Name));
					return circularInfo;
				}
				
				return null;
			}
		}

		private bool HasCircularDependency(Type serviceType, List<Type> path, CircularDependencyInfo circularInfo)
		{
			// Check if this service is exempt from circular dependency checks
			if (_circularExemptServices.Contains(serviceType))
			{
				return false;
			}
			
			// Check if this type is already in our path (circular dependency)
			if (path.Contains(serviceType))
			{
				// Found a cycle! Add it once more to complete the circle
				path.Add(serviceType);
				
				// Find the last occurrence that completes the cycle
				var lastIndex = path.Count - 2; // The one before we just added
				if (lastIndex >= 0 && _dependencyGraph.TryGetValue(path[lastIndex], out var lastNode) &&
					lastNode.DependencyFields.TryGetValue(serviceType, out var completingField))
				{
					circularInfo.FromType = path[lastIndex];
					circularInfo.ToType = serviceType;
					circularInfo.FieldName = completingField;
				}
				
				return true;
			}
			
			// Add to path
			path.Add(serviceType);

			// Check if this service has dependencies
			if (_dependencyGraph.TryGetValue(serviceType, out var node))
			{
				foreach (var dependency in node.Dependencies)
				{
					// Skip if the dependency is exempt
					if (_circularExemptServices.Contains(dependency))
					{
						continue;
					}
					
					// Recursively check this dependency
					if (HasCircularDependency(dependency, path, circularInfo))
					{
						return true;
					}
				}
			}

			// Backtrack
			path.RemoveAt(path.Count - 1);
			return false;
		}

		private List<FieldInfo> GetFieldsToInject(Type targetType)
		{
			var fields = new List<FieldInfo>();
			var currentType = targetType;
	
			// Walk up the inheritance hierarchy
			while (currentType != null && currentType != typeof(object))
			{
				var typeFields = currentType
					.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
					.Where(f => f.GetCustomAttribute<InjectServiceAttribute>() != null);
			
				fields.AddRange(typeFields);
				currentType = currentType.BaseType;
			}
	
			return fields;
		}

		private async Task SwitchToUnityThread(SynchronizationContext unityContext)
		{
			if (SynchronizationContext.Current == unityContext) return;

			var tcs = new TaskCompletionSource<bool>();
			unityContext.Post(_ => tcs.SetResult(true), null);
			await tcs.Task;
		}

		private void DefaultErrorHandler(Exception exception)
		{
			Debug.LogError($"Failed to inject required services: {exception.Message}");
			
			// Check for circular dependencies and log them
			var circularDep = DetectCircularDependency();
			if (circularDep != null)
			{
				Debug.LogError($"Circular dependency detected: {circularDep.Path}");
				
				// Mark the service types involved in the circular dependency as having errors
				if (_targetServiceType != null)
				{
					AddCircularDependencyError(_targetServiceType);
				}
				
				// Also mark the "to" type in the circular dependency
				if (circularDep.ToType != null)
				{
					AddCircularDependencyError(circularDep.ToType);
				}
			}
		}

		/// <summary>
		/// Mark a service type as exempt from circular dependency checks
		/// </summary>
		public static void AddCircularDependencyExemption(Type serviceType)
		{
			lock (_graphLock)
			{
				_circularExemptServices.Add(serviceType);
			}
		}
		
		/// <summary>
		/// Check if a service type is exempt from circular dependency checks
		/// </summary>
		public static bool IsExemptFromCircularDependencyCheck(Type serviceType)
		{
			lock (_graphLock)
			{
				return _circularExemptServices.Contains(serviceType);
			}
		}
		
		/// <summary>
		/// Check if a service type has circular dependency errors
		/// </summary>
		public static bool HasCircularDependencyError(Type serviceType)
		{
			lock (_graphLock)
			{
				return _servicesWithCircularDependencyErrors.Contains(serviceType);
			}
		}
		
		/// <summary>
		/// Mark a service type as having circular dependency errors
		/// </summary>
		public static void AddCircularDependencyError(Type serviceType)
		{
			lock (_graphLock)
			{
				_servicesWithCircularDependencyErrors.Add(serviceType);
			}
		}

		/// <summary>
		/// Remove circular dependency exemption for a service type
		/// </summary>
		public static void RemoveCircularDependencyExemption(Type serviceType)
		{
			lock (_graphLock)
			{
				_circularExemptServices.Remove(serviceType);
			}
		}

		/// <summary>
		/// Check if a service type is exempt from circular dependency checks
		/// </summary>
		public static bool IsCircularDependencyExempt(Type serviceType)
		{
			lock (_graphLock)
			{
				return _circularExemptServices.Contains(serviceType);
			}
		}

		/// <summary>
		/// Get a report of all current dependencies in the system
		/// </summary>
		public static string GetDependencyReport()
		{
			lock (_graphLock)
			{
				var report = "Dependency Graph:\n";
				
				foreach (var kvp in _dependencyGraph)
				{
					report += $"\n{kvp.Key.Name}";
					
					// Check if exempt
					if (_circularExemptServices.Contains(kvp.Key))
					{
						report += " [CIRCULAR EXEMPT]";
					}
					
					if (kvp.Value.Dependencies.Count > 0)
					{
						report += " depends on:";
						foreach (var dep in kvp.Value.Dependencies)
						{
							var fieldName = kvp.Value.DependencyFields.TryGetValue(dep, out var field) ? field : "unknown";
							report += $"\n  - {dep.Name} (field: {fieldName})";
							
							if (_circularExemptServices.Contains(dep))
							{
								report += " [EXEMPT]";
							}
						}
					}
					else
					{
						report += " (no dependencies)";
					}
					
					if (kvp.Value.IsResolving)
					{
						report += " [RESOLVING]";
					}
				}
				
				return report;
			}
		}

		/// <summary>
		/// Clear the dependency graph (useful for testing)
		/// </summary>
		public static void ClearDependencyGraph()
		{
			lock (_graphLock)
			{
				_dependencyGraph.Clear();
				_circularExemptServices.Clear();
				_servicesWithCircularDependencyErrors.Clear();
				
				// Cancel any pending resolutions
				foreach (var cts in _resolvingCancellations.Values)
				{
					try { cts.Cancel(); } catch { }
				}
				_resolvingCancellations.Clear();
			}
		}
	}
}