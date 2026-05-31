using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.ErrorHandlingExample
{
	/// <summary>
	/// Part of a circular dependency pair (A depends on B, B depends on A).
	///
	/// Demonstrates:
	/// - Standard [Service] registration (not exempt from circular dependency detection)
	/// - [InjectService] dependency on ICircularB
	/// - How ServiceKit detects circular dependencies at injection time
	///
	/// CircularServiceB uses CircularDependencyExempt = true to break the cycle.
	/// Without that exemption, both services would deadlock waiting for each other.
	/// </summary>
	[Service(typeof(ICircularA))]
	public class CircularServiceA : ServiceKitBehaviour, ICircularA
	{
		[InjectService] private ICircularB _circularB;

		public string Name => "CircularServiceA";

		protected override void InitializeService()
		{
			Debug.Log($"[CircularServiceA] Initialized. Partner: {_circularB?.Name ?? "null"}");
		}

		protected override void HandleDependencyInjectionFailure(Exception exception)
		{
			Debug.LogError($"[CircularServiceA] Injection failed: {exception.Message}");
		}
	}
}
