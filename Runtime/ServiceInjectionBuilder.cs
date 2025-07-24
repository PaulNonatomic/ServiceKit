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
		private static readonly object _graphLock = new object();

		private class DependencyNode
		{
			public Type ServiceType { get; set; }
			public HashSet<Type> Dependencies { get; set; } = new HashSet<Type>();
			public bool IsResolving { get; set; }
			public List<Type> ResolutionPath { get; set; } = new List<Type>();
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
					CancelCircularDependencyChain(circularDependency);
					
					throw new ServiceInjectionException(
						$"Circular dependency detected: {circularDependency}");
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
							message += $" Circular dependency detected: {circularDep}";
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
			// Parse the circular dependency path to get all involved types
			var typeNames = circularDependencyPath.Split(new[] { " → " }, StringSplitOptions.None);
			
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
				if (!_dependencyGraph.ContainsKey(_targetServiceType))
				{
					_dependencyGraph[_targetServiceType] = new DependencyNode
					{
						ServiceType = _targetServiceType
					};
				}

				var node = _dependencyGraph[_targetServiceType];
				node.Dependencies.Clear();

				foreach (var field in fieldsToInject)
				{
					var attribute = field.GetCustomAttribute<InjectServiceAttribute>();
					if (attribute.Required)
					{
						node.Dependencies.Add(field.FieldType);
					}
				}
			}
		}

		private string DetectCircularDependency()
		{
			lock (_graphLock)
			{
				var visited = new HashSet<Type>();
				var path = new List<Type>();
				
				if (HasCircularDependency(_targetServiceType, visited, path))
				{
					// Format the circular dependency path
					var cycle = string.Join(" → ", path.Select(t => t.Name));
					return cycle;
				}
				
				return null;
			}
		}

		private bool HasCircularDependency(Type serviceType, HashSet<Type> visited, List<Type> path)
		{
			if (!visited.Add(serviceType))
			{
				// Found a cycle
				var cycleStart = path.IndexOf(serviceType);
				if (cycleStart >= 0)
				{
					// Add the type again to show the complete cycle
					path.Add(serviceType);
					return true;
				}
				return false;
			}

			path.Add(serviceType);

			// Check if this service has dependencies
			if (_dependencyGraph.TryGetValue(serviceType, out var node))
			{
				foreach (var dependency in node.Dependencies)
				{
					// Check if the dependency is currently being resolved
					if (_dependencyGraph.TryGetValue(dependency, out var depNode) && depNode.IsResolving)
					{
						// This dependency is also trying to resolve, potential circular dependency
						path.Add(dependency);
						return true;
					}

					if (HasCircularDependency(dependency, visited, path))
					{
						return true;
					}
				}
			}

			path.RemoveAt(path.Count - 1);
			visited.Remove(serviceType);
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
				Debug.LogError($"Circular dependency detected: {circularDep}");
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
					if (kvp.Value.Dependencies.Count > 0)
					{
						report += " depends on:";
						foreach (var dep in kvp.Value.Dependencies)
						{
							report += $"\n  - {dep.Name}";
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