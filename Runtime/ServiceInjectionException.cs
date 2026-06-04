using System;
using UnityEngine;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Why a dependency injection attempt ended. Used to choose how the failure is
	/// surfaced (error / warning / silent) and to word the message accurately.
	/// </summary>
	public enum ServiceInjectionFailureKind
	{
		/// <summary>A required service never became available within the timeout.</summary>
		Timeout,

		/// <summary>The caller's cancellation token fired (a deliberate cancellation on a non-Unity target).</summary>
		CallerCanceled,

		/// <summary>An awaited service was unregistered before it became available (e.g. its provider was destroyed during a scene change). Target is still alive.</summary>
		ServiceUnregistered,

		/// <summary>The injection target itself was destroyed mid-injection (e.g. a scene change). Expected teardown.</summary>
		TargetDestroyed
	}

	public class ServiceInjectionException : Exception
	{
		public ServiceInjectionException(string message) : base(message)
		{
		}

		public ServiceInjectionException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

	/// <summary>
	/// Thrown when injection is cancelled or times out. Subclasses <see cref="TimeoutException"/>
	/// for backwards compatibility (existing <c>catch (TimeoutException)</c> still works) while
	/// carrying a <see cref="Kind"/> so handlers can route severity correctly.
	/// </summary>
	public class ServiceInjectionTimeoutException : TimeoutException
	{
		public ServiceInjectionTimeoutException(string message, ServiceInjectionFailureKind kind) : base(message)
		{
			Kind = kind;
		}

		public ServiceInjectionFailureKind Kind { get; }
	}

	/// <summary>
	/// Severity-aware logging for injection failures, shared by the builder's default handler
	/// and <c>ServiceKitBehaviour.HandleDependencyInjectionFailure</c> so both route the same way:
	/// a destroyed target is silent (expected teardown), a vanished service is a warning, and
	/// everything else is an error. The <paramref name="context"/> object makes the console entry
	/// select the offending GameObject when clicked.
	/// </summary>
	public static class ServiceInjectionLog
	{
		public static void Report(Exception exception, UnityEngine.Object context)
		{
			var message = $"Failed to inject required services: {exception.Message}";

			switch ((exception as ServiceInjectionTimeoutException)?.Kind)
			{
				case ServiceInjectionFailureKind.TargetDestroyed:
					// The target is gone; the failed injection is moot. Stay silent.
					return;
				case ServiceInjectionFailureKind.ServiceUnregistered:
					Debug.LogWarning(message, context);
					return;
				default:
					Debug.LogError(message, context);
					return;
			}
		}
	}
}
