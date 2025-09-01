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
	public abstract class ServiceKitBehaviour<T> : MonoBehaviour where T : class 
	{
		[SerializeField] protected ServiceKitLocator ServiceKitLocator;

		protected bool IsServiceRegistered;
		protected bool IsServiceReady;
		
		/// <summary>
		/// Cached destroy cancellation token to avoid MissingReferenceException.
		/// Use this for any async operations that should be cancelled when the GameObject is destroyed.
		/// </summary>
		protected CancellationToken CachedDestroyToken { get; private set; }

		protected virtual async void Awake()
		{
			if (!this || !gameObject) return;
			
			CachedDestroyToken = destroyCancellationToken;
			RegisterServiceWithLocator();
			
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
			if (this is T serviceInstance) return serviceInstance;

			ThrowInterfaceNotImplementedException();
			return null;
		}

		private void ThrowInterfaceNotImplementedException()
		{
			var serviceType = typeof(T);
			var implementationType = GetType();
			var implementedInterfaces = GetImplementedInterfaceNames(implementationType);
			var errorMessage = BuildInterfaceNotImplementedMessage(implementationType, serviceType, implementedInterfaces);
			
			Debug.LogError($"[ServiceKit] {errorMessage}", this);
			throw new InvalidOperationException(errorMessage);
		}

		private static string GetImplementedInterfaceNames(Type type)
		{
			var interfaces = type.GetInterfaces();
			var interfaceNames = interfaces.Select(i => i.Name);
			return string.Join(", ", interfaceNames);
		}

		private static string BuildInterfaceNotImplementedMessage(Type implementationType, Type serviceType, string implementedInterfaces)
		{
			return $"Failed to register service for '{implementationType.Name}' as '{serviceType.Name}'. " +
				   $"This typically means '{implementationType.Name}' does not implement interface '{serviceType.Name}'. " +
				   $"Current class '{implementationType.Name}' implements: [{implementedInterfaces}]. " +
				   $"Please ensure '{implementationType.Name}' properly implements '{serviceType.Name}'.";
		}

		private void RegisterInstanceWithLocator(T serviceInstance)
		{
			ServiceKitLocator.RegisterService<T>(serviceInstance);
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
			IsServiceRegistered = false;
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