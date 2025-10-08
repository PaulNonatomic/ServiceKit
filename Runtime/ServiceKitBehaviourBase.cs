using System;
using System.Linq;
using System.Threading;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	public abstract class ServiceKitBehaviourBase<T> : MonoBehaviour where T : class
	{
		protected abstract ServiceKitLocator ServiceKitLocator { get; set; }

		protected bool IsServiceRegistered { get; private set; }
		protected bool IsServiceReady { get; private set; }

		/// <summary>
		/// Cached destroy cancellation token to avoid MissingReferenceException.
		/// Use this for any async operations that should be cancelled when the GameObject is destroyed.
		/// </summary>
		protected CancellationToken CachedDestroyToken { get; private set; }

		protected virtual async void Awake()
		{
			if (IsObjectDestroyed()) return;

			CacheDestroyToken();
			RegisterServiceWithLocator();

			await PerformServiceInitializationSequence();
		}

		private bool IsObjectDestroyed()
		{
			return !this || !gameObject;
		}

		protected void CacheDestroyToken()
		{
			CachedDestroyToken = destroyCancellationToken;
		}

#if SERVICEKIT_UNITASK
		private async UniTask PerformServiceInitializationSequence()
#else
		private async Task PerformServiceInitializationSequence()
#endif
		{
			await InjectDependenciesAsync();
			await InitializeServiceAsync();

			InitializeService();
			MarkServiceAsReady();
		}

		protected virtual void OnDestroy()
		{
			UnregisterServiceFromLocator();
		}

		protected virtual void RegisterServiceWithLocator()
		{
			if (IsServiceLocatorMissing()) return;

			var serviceInstance = CastThisToServiceInterface();
			RegisterInstanceWithLocator(serviceInstance);
			LogRegistrationIfDebugEnabled();
		}

		private T CastThisToServiceInterface()
		{
			return this is T serviceInstance
				? serviceInstance
				: ThrowInterfaceNotImplementedException();
		}

		private T ThrowInterfaceNotImplementedException()
		{
			var errorMessage = GenerateInterfaceNotImplementedErrorMessage();
			LogAndThrowError(errorMessage);
			return null;
		}

		private string GenerateInterfaceNotImplementedErrorMessage()
		{
			var serviceType = typeof(T);
			var implementationType = GetType();
			var implementedInterfaces = GetImplementedInterfaceNames(implementationType);
			return BuildInterfaceNotImplementedMessage(implementationType, serviceType, implementedInterfaces);
		}

		private void LogAndThrowError(string errorMessage)
		{
			Debug.LogError($"[ServiceKit] {errorMessage}", this);
			throw new InvalidOperationException(errorMessage);
		}

		private static string GetImplementedInterfaceNames(Type type)
		{
			var interfaces = type.GetInterfaces();
			if (HasNoInterfaces(interfaces)) return string.Empty;

			return FormatInterfaceList(interfaces);
		}

		private static bool HasNoInterfaces(Type[] interfaces)
		{
			return interfaces.Length == 0;
		}

		private static string FormatInterfaceList(Type[] interfaces)
		{
			var stringBuilder = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				AppendInterfaceNames(stringBuilder, interfaces);
				return stringBuilder.ToString();
			}
			finally
			{
				ServiceKitObjectPool.ReturnStringBuilder(stringBuilder);
			}
		}

		private static void AppendInterfaceNames(System.Text.StringBuilder stringBuilder, Type[] interfaces)
		{
			for (int i = 0; i < interfaces.Length; i++)
			{
				if (IsNotFirstInterface(i)) stringBuilder.Append(", ");
				stringBuilder.Append(interfaces[i].Name);
			}
		}

		private static bool IsNotFirstInterface(int index)
		{
			return index > 0;
		}

		private static string BuildInterfaceNotImplementedMessage(Type implementationType, Type serviceType, string implementedInterfaces)
		{
			var sb = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				sb.Append("Failed to register service for '");
				sb.Append(implementationType.Name);
				sb.Append("' as '");
				sb.Append(serviceType.Name);
				sb.Append("'. This typically means '");
				sb.Append(implementationType.Name);
				sb.Append("' does not implement interface '");
				sb.Append(serviceType.Name);
				sb.Append("'. Current class '");
				sb.Append(implementationType.Name);
				sb.Append("' implements: [");
				sb.Append(implementedInterfaces);
				sb.Append("]. Please ensure '");
				sb.Append(implementationType.Name);
				sb.Append("' properly implements '");
				sb.Append(serviceType.Name);
				sb.Append("'.");
				return sb.ToString();
			}
			finally
			{
				ServiceKitObjectPool.ReturnStringBuilder(sb);
			}
		}

		private void RegisterInstanceWithLocator(T serviceInstance)
		{
			ServiceKitLocator.RegisterService<T>(serviceInstance);
			MarkAsRegistered();
		}

		private void MarkAsRegistered()
		{
			IsServiceRegistered = true;
		}

		private void LogRegistrationIfDebugEnabled()
		{
			if (!IsDebugLoggingEnabled()) return;

			Debug.Log($"[{GetType().Name}] Service registered (not ready yet)");
		}

		protected virtual void MarkServiceAsReady()
		{
			if (IsServiceLocatorMissing()) return;
			if (!IsServiceRegistered) return;

			NotifyLocatorServiceIsReady();
			LogReadyStatusIfDebugEnabled();
		}

		private void NotifyLocatorServiceIsReady()
		{
			ServiceKitLocator.ReadyService<T>();
			MarkAsReady();
		}

		private void MarkAsReady()
		{
			IsServiceReady = true;
		}

		private void LogReadyStatusIfDebugEnabled()
		{
			if (!IsDebugLoggingEnabled()) return;

			Debug.Log($"[{GetType().Name}] Service is now READY!");
		}

		protected virtual void UnregisterServiceFromLocator()
		{
			if (IsServiceLocatorMissing()) return;

			ResetServiceRegistrationState();
			RemoveFromServiceLocator();
		}

		private void ResetServiceRegistrationState()
		{
			MarkAsUnregistered();
			MarkAsNotReady();
		}

		private void MarkAsUnregistered()
		{
			IsServiceRegistered = false;
		}

		private void MarkAsNotReady()
		{
			IsServiceReady = false;
		}

		private void RemoveFromServiceLocator()
		{
			var serviceType = typeof(T);
			ServiceKitLocator.UnregisterService(serviceType);
		}

#if SERVICEKIT_UNITASK
		protected virtual async UniTask InjectDependenciesAsync()
#else
		protected virtual async Task InjectDependenciesAsync()
#endif
		{
			if (IsServiceLocatorMissing()) return;

			LogWaitingForDependenciesIfDebugEnabled();
			await PerformDependencyInjection();
			LogDependenciesInjectedIfDebugEnabled();
		}

		private void LogWaitingForDependenciesIfDebugEnabled()
		{
			if (!IsDebugLoggingEnabled()) return;

			Debug.Log($"[{GetType().Name}] Waiting for dependencies...");
		}

#if SERVICEKIT_UNITASK
		private async UniTask PerformDependencyInjection()
#else
		private async Task PerformDependencyInjection()
#endif
		{
			await ServiceKitLocator.InjectServicesAsync(this)
				.WithCancellation(CachedDestroyToken)
				.WithTimeout()
				.WithErrorHandling(HandleDependencyInjectionFailure)
				.ExecuteAsync();
		}


		private void LogDependenciesInjectedIfDebugEnabled()
		{
			if (!IsDebugLoggingEnabled()) return;

			Debug.Log($"[{GetType().Name}] Dependencies injected!");
		}

		private bool IsDebugLoggingEnabled()
		{
			return ServiceKitSettings.Instance.DebugLogging;
		}

		/// <summary>
		/// Override this to perform initialization after dependencies are injected
		/// but before the service becomes ready
		/// </summary>
#if SERVICEKIT_UNITASK
		protected virtual async UniTask InitializeServiceAsync()
		{
			await UniTask.CompletedTask;
		}
#else
		protected virtual async Task InitializeServiceAsync()
		{
			await Task.CompletedTask;
		}
#endif

		/// <summary>
		/// Override this to perform initialization after dependencies are injected
		/// but before the service becomes ready
		/// </summary>
		protected virtual void InitializeService()
		{
		}

		/// <summary>
		/// Called when service injection fails
		/// </summary>
		protected virtual void HandleDependencyInjectionFailure(Exception exception)
		{
			Debug.LogError($"Failed to inject required services: {exception.Message}", this);
		}

		private bool IsServiceLocatorMissing()
		{
			if (HasServiceLocatorAssigned()) return false;

			// Don't log error during destruction - destruction order is non-deterministic
			if (!Application.isPlaying || IsObjectDestroyed()) return true;

			LogMissingServiceLocatorError();
			return true;
		}

		private bool HasServiceLocatorAssigned()
		{
			return ServiceKitLocator != null;
		}

		private void LogMissingServiceLocatorError()
		{
			Debug.LogError($"{GetType().Name} requires a reference to a ServiceKitLocator.", this);
		}
	}
}
