using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Centralized dependency graph management and circular detection.
	/// </summary>
	internal static class ServiceDependencyGraph
	{
		internal sealed class DependencyNode
		{
			public Type ServiceType { get; set; }
			public HashSet<Type> Dependencies { get; } = new HashSet<Type>();
			public Dictionary<Type, string> DependencyFields { get; } = new Dictionary<Type, string>();
			public bool IsResolving { get; set; }
		}

		internal sealed class CircularDependencyInfo
		{
			public string Path { get; set; }
			public Type FromType { get; set; }
			public Type ToType { get; set; }
			public string FieldName { get; set; }
		}

		public static void UpdateForTarget(Type serviceType, List<FieldInfo> fieldsToInject)
		{
			lock (_graphLock)
			{
				var node = GetOrCreate(serviceType);
				node.Dependencies.Clear();
				node.DependencyFields.Clear();
				foreach (var field in fieldsToInject)
				{
					node.Dependencies.Add(field.FieldType);
					node.DependencyFields[field.FieldType] = field.Name;
				}
			}
		}

		public static void UpdateForRegistration(Type serviceType, List<FieldInfo> fieldsToInject)
		{
			UpdateForTarget(serviceType, fieldsToInject);
		}

		public static CircularDependencyInfo DetectCircularDependency(Type root)
		{
			lock (_graphLock)
			{
				var path = new List<Type>();
				var info = new CircularDependencyInfo();
				if (!HasCircular(root, path, info))
				{
					return null;
				}
				info.Path = string.Join(" → ", path.Select(t => t.Name));
				return info;
			}
		}

		public static string DetectCircularDependencyAtRegistration(Type serviceType)
		{
			var info = DetectCircularDependency(serviceType);
			return info?.Path;
		}

		public static void RegisterResolving(Type serviceType, CancellationTokenSource cts)
		{
			lock (_graphLock)
			{
				_resolvingCancellations[serviceType] = cts;
			}
		}

		public static void UnregisterResolving(Type serviceType)
		{
			lock (_graphLock)
			{
				_resolvingCancellations.Remove(serviceType);
			}
		}

		public static void SetResolving(Type serviceType, bool isResolving)
		{
			lock (_graphLock)
			{
				var node = GetOrCreate(serviceType);
				node.IsResolving = isResolving;
			}
		}

		public static void CancelCircularChain(string circularPath)
		{
			var firstLine = circularPath.Split('\n')[0];
			var typeNames = firstLine.Split(new[] { " → " }, StringSplitOptions.None);
			lock (_graphLock)
			{
				foreach (var kvp in _resolvingCancellations.ToList())
				{
					if (!typeNames.Contains(kvp.Key.Name))
					{
						continue;
					}
					try { kvp.Value.Cancel(); } catch { /* ignore */ }
				}
			}
		}

		public static void MarkAllInPathAsError(string circularPath)
		{
			var names = circularPath.Split(new[] { " → " }, StringSplitOptions.None).Select(n => n.Trim()).ToList();
			lock (_graphLock)
			{
				foreach (var type in _dependencyGraph.Keys)
				{
					if (names.Contains(type.Name))
					{
						_servicesWithCircularErrors.Add(type);
					}
				}
			}
		}

		public static void AddCircularDependencyError(Type serviceType)
		{
			lock (_graphLock)
			{
				_servicesWithCircularErrors.Add(serviceType);
			}
		}

		public static bool HasCircularDependencyError(Type serviceType)
		{
			lock (_graphLock)
			{
				return _servicesWithCircularErrors.Contains(serviceType);
			}
		}

		public static void AddCircularDependencyExemption(Type serviceType)
		{
			lock (_graphLock)
			{
				_circularExempt.Add(serviceType);
			}
		}

		public static void RemoveCircularDependencyExemption(Type serviceType)
		{
			lock (_graphLock)
			{
				_circularExempt.Remove(serviceType);
			}
		}

		public static bool IsCircularDependencyExempt(Type serviceType)
		{
			lock (_graphLock)
			{
				return _circularExempt.Contains(serviceType);
			}
		}

		public static string GetDependencyReport()
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

		public static void ClearAll()
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

		private static bool HasCircular(Type serviceType, List<Type> path, CircularDependencyInfo info)
		{
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
					info.FromType = path[lastIndex];
					info.ToType = serviceType;
					info.FieldName = completingField;
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
					if (HasCircular(dep, path, info))
					{
						return true;
					}
				}
			}
			path.RemoveAt(path.Count - 1);
			return false;
		}

		private static DependencyNode GetOrCreate(Type type)
		{
			if (!_dependencyGraph.TryGetValue(type, out var node))
			{
				node = new DependencyNode { ServiceType = type };
				_dependencyGraph[type] = node;
			}
			return node;
		}

		private static readonly object _graphLock = new object();
		private static readonly Dictionary<Type, DependencyNode> _dependencyGraph = new Dictionary<Type, DependencyNode>();
		private static readonly Dictionary<Type, CancellationTokenSource> _resolvingCancellations = new Dictionary<Type, CancellationTokenSource>();
		private static readonly HashSet<Type> _circularExempt = new HashSet<Type>();
		private static readonly HashSet<Type> _servicesWithCircularErrors = new HashSet<Type>();
	}
}