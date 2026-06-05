using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Per-type reflection memoization. The set of <c>[InjectService]</c> fields on a type, a field's
	/// <c>[InjectService]</c> attribute, and a type's <c>[Service]</c> attribute are all immutable for
	/// the lifetime of the process, so they are computed once and cached. This removes the inheritance
	/// walk + <c>GetFields</c> + attribute lookups that previously ran on every injection and every
	/// registration (e.g. once per instance when spawning many copies of the same prefab).
	/// </summary>
	internal static class ServiceKitReflectionCache
	{
		private const BindingFlags FieldFlags =
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

		private static readonly ConcurrentDictionary<Type, FieldInfo[]> InjectableFields = new();
		private static readonly ConcurrentDictionary<FieldInfo, InjectServiceAttribute> InjectAttributes = new();
		private static readonly ConcurrentDictionary<Type, ServiceAttribute> ServiceAttributes = new();

		// The cached data is locator-independent and immutable per type, so surviving a domain reload
		// is harmless. This reset exists purely so the cache behaves like fresh static state when
		// "Enter Play Mode Without Domain Reload" is enabled - it runs before any scene loads.
		[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetOnEnterPlayMode()
		{
			InjectableFields.Clear();
			InjectAttributes.Clear();
			ServiceAttributes.Clear();
		}

		/// <summary>The cached, inheritance-flattened set of fields marked with <c>[InjectService]</c>.</summary>
		public static FieldInfo[] GetInjectableFields(Type type) => InjectableFields.GetOrAdd(type, BuildInjectableFields);

		/// <summary>The cached <c>[InjectService]</c> attribute for a field (never null for injectable fields).</summary>
		public static InjectServiceAttribute GetInjectAttribute(FieldInfo field) =>
			InjectAttributes.GetOrAdd(field, f => f.GetCustomAttribute<InjectServiceAttribute>());

		/// <summary>The cached <c>[Service]</c> attribute for a type, or null if it has none.</summary>
		public static ServiceAttribute GetServiceAttribute(Type type) =>
			ServiceAttributes.GetOrAdd(type, t => t.GetCustomAttribute<ServiceAttribute>());

		private static FieldInfo[] BuildInjectableFields(Type type)
		{
			var fields = new List<FieldInfo>();
			for (var current = type; current != null && current != typeof(object); current = current.BaseType)
			{
				foreach (var field in current.GetFields(FieldFlags))
				{
					var attribute = field.GetCustomAttribute<InjectServiceAttribute>();
					if (attribute == null)
					{
						continue;
					}

					fields.Add(field);
					InjectAttributes.TryAdd(field, attribute); // warm the attribute cache while we're here
				}
			}

			return fields.ToArray();
		}
	}
}
