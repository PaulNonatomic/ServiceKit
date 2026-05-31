using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.ErrorHandlingExample
{
	/// <summary>
	/// Part of a circular dependency pair (B depends on A, A depends on B).
	///
	/// Demonstrates:
	/// - CircularDependencyExempt = true to break circular dependency detection
	/// - When this flag is set, ServiceKit skips this service during cycle detection,
	///   allowing both services to initialize without deadlock
	///
	/// Without this exemption on at least one side of the cycle, ServiceKit would
	/// detect the circular dependency and throw an exception.
	/// </summary>
	[Service(typeof(ICircularB), CircularDependencyExempt = true)]
	public class CircularServiceB : ServiceKitBehaviour, ICircularB
	{
		[InjectService] private ICircularA _circularA;

		public string Name => "CircularServiceB";

		protected override void InitializeService()
		{
			Debug.Log($"[CircularServiceB] Initialized. Partner: {_circularA?.Name ?? "null"}");
		}

		protected override void HandleDependencyInjectionFailure(Exception exception)
		{
			Debug.LogError($"[CircularServiceB] Injection failed: {exception.Message}");
		}
	}
}
