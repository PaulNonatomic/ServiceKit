using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Dependency graph management and circular detection for a single ServiceKitLocator.
	/// Each locator owns its own instance so graphs, exemptions and circular-error state
	/// never leak between independent locators.
	/// </summary>
	internal sealed class ServiceDependencyGraph
	{
		internal sealed class DependencyNode
		{
			public Type ServiceType { get; set; }
			public HashSet<Type> Dependencies { get; } = new HashSet<Type>();
			public Dictionary<Type, string> DependencyFields { get; } = new Dictionary<Type, string>();
			public bool IsResolving { get; set; }

			// A type's [InjectService] fields are immutable, so its edges only need building once.
			public bool DependenciesPopulated { get; set; }
		}

		internal sealed class CircularDependencyInfo
		{
			public string Path { get; set; }
			public IReadOnlyList<Type> TypesInPath { get; set; }
			public Type FromType { get; set; }
			public Type ToType { get; set; }
			public string FieldName { get; set; }
		}

		public void UpdateForTarget(Type serviceType, IReadOnlyList<FieldInfo> fieldsToInject)
		{
			lock (_graphLock)
			{
				var node = GetOrCreate(serviceType);
				// Edges are derived from the type's immutable injectable fields, so building them once is
				// enough. This skips the per-injection clear+rebuild churn under the lock when the same
				// type is injected repeatedly (e.g. spawning many copies of a prefab).
				if (node.DependenciesPopulated) return;

				node.Dependencies.Clear();
				node.DependencyFields.Clear();
				for (var i = 0; i < fieldsToInject.Count; i++)
				{
					var field = fieldsToInject[i];
					node.Dependencies.Add(field.FieldType);
					node.DependencyFields[field.FieldType] = field.Name;
				}
				node.DependenciesPopulated = true;
			}
		}

		public void UpdateForRegistration(Type serviceType, IReadOnlyList<FieldInfo> fieldsToInject)
		{
			UpdateForTarget(serviceType, fieldsToInject);
		}

		public CircularDependencyInfo DetectCircularDependency(Type root)
		{
			lock (_graphLock)
			{
				// Runs on every injection. Allocate nothing but the walk's path until a cycle is actually
				// found - the CircularDependencyInfo and its path copy are pure garbage in the common case.
				var path = new List<Type>();
				if (!HasCircular(root, path, out var fromType, out var toType, out var fieldName))
				{
					return null;
				}
				return new CircularDependencyInfo
				{
					TypesInPath = new List<Type>(path),
					Path = string.Join(" → ", path.Select(t => t.Name)),
					FromType = fromType,
					ToType = toType,
					FieldName = fieldName,
				};
			}
		}

		public CircularDependencyInfo DetectCircularDependencyAtRegistration(Type serviceType)
		{
			return DetectCircularDependency(serviceType);
		}

		public void RegisterResolving(Type serviceType, CancellationTokenSource cts)
		{
			lock (_graphLock)
			{
				_resolvingCancellations[serviceType] = cts;
			}
		}

		public void UnregisterResolving(Type serviceType)
		{
			lock (_graphLock)
			{
				_resolvingCancellations.Remove(serviceType);
			}
		}

		public void SetResolving(Type serviceType, bool isResolving)
		{
			lock (_graphLock)
			{
				var node = GetOrCreate(serviceType);
				node.IsResolving = isResolving;
			}
		}

		public void CancelCircularChain(IReadOnlyList<Type> typesInPath)
		{
			lock (_graphLock)
			{
				foreach (var kvp in _resolvingCancellations.ToList())
				{
					if (!typesInPath.Contains(kvp.Key))
					{
						continue;
					}
					try { kvp.Value.Cancel(); } catch { /* ignore */ }
				}
			}
		}

		public void MarkAllInPathAsError(IReadOnlyList<Type> typesInPath)
		{
			lock (_graphLock)
			{
				foreach (var type in typesInPath)
				{
					_servicesWithCircularErrors.Add(type);
				}
			}
		}

		public void AddCircularDependencyError(Type serviceType)
		{
			lock (_graphLock)
			{
				_servicesWithCircularErrors.Add(serviceType);
			}
		}

		public bool HasCircularDependencyError(Type serviceType)
		{
			lock (_graphLock)
			{
				return _servicesWithCircularErrors.Contains(serviceType);
			}
		}

		public void AddCircularDependencyExemption(Type serviceType)
		{
			lock (_graphLock)
			{
				_circularExempt.Add(serviceType);
			}
		}

		public void RemoveCircularDependencyExemption(Type serviceType)
		{
			lock (_graphLock)
			{
				_circularExempt.Remove(serviceType);
			}
		}

		public bool IsCircularDependencyExempt(Type serviceType)
		{
			lock (_graphLock)
			{
				return _circularExempt.Contains(serviceType);
			}
		}

		public string GetDependencyReport()
		{
			lock (_graphLock)
			{
				var report = "Dependency Graph:\n";
				foreach (var kvp in _dependencyGraph)
				{
					report += $"\n{kvp.Key.Name}";
					if (_circularExempt.Contains(kvp.Key))
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
							if (_circularExempt.Contains(dep))
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

		public void ClearAll()
		{
			lock (_graphLock)
			{
				_dependencyGraph.Clear();
				_circularExempt.Clear();
				_servicesWithCircularErrors.Clear();
				foreach (var cts in _resolvingCancellations.Values)
				{
					try { cts.Cancel(); } catch { }
				}
				_resolvingCancellations.Clear();
			}
		}

		private bool HasCircular(Type serviceType, List<Type> path, out Type fromType, out Type toType, out string fieldName)
		{
			fromType = null;
			toType = null;
			fieldName = null;

			if (_circularExempt.Contains(serviceType))
			{
				return false;
			}
			if (path.Contains(serviceType))
			{
				path.Add(serviceType);
				var lastIndex = path.Count - 2;
				if (lastIndex >= 0 &&
					_dependencyGraph.TryGetValue(path[lastIndex], out var lastNode) &&
					lastNode.DependencyFields.TryGetValue(serviceType, out var completingField))
				{
					fromType = path[lastIndex];
					toType = serviceType;
					fieldName = completingField;
				}
				return true;
			}
			path.Add(serviceType);
			if (_dependencyGraph.TryGetValue(serviceType, out var node))
			{
				foreach (var dep in node.Dependencies)
				{
					if (_circularExempt.Contains(dep))
					{
						continue;
					}
					if (HasCircular(dep, path, out fromType, out toType, out fieldName))
					{
						return true;
					}
				}
			}
			path.RemoveAt(path.Count - 1);
			return false;
		}

		private DependencyNode GetOrCreate(Type type)
		{
			if (!_dependencyGraph.TryGetValue(type, out var node))
			{
				node = new DependencyNode { ServiceType = type };
				_dependencyGraph[type] = node;
			}
			return node;
		}

		private readonly object _graphLock = new object();
		private readonly Dictionary<Type, DependencyNode> _dependencyGraph = new Dictionary<Type, DependencyNode>();
		private readonly Dictionary<Type, CancellationTokenSource> _resolvingCancellations = new Dictionary<Type, CancellationTokenSource>();
		private readonly HashSet<Type> _circularExempt = new HashSet<Type>();
		private readonly HashSet<Type> _servicesWithCircularErrors = new HashSet<Type>();
	}
}
