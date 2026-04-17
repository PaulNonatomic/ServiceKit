using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Base class for services that can be registered with ServiceKit.
	/// Use the [Service] attribute to specify what types this service registers as.
	///
	/// Examples:
	/// [Service(typeof(IMyService))]                  // Single interface
	/// [Service(typeof(IFoo), typeof(IBar))]          // Multiple interfaces
	/// [Service]                                       // Concrete type (no attribute needed)
	/// public class MyService : ServiceKitBehaviour { }
	/// </summary>
	public abstract class ServiceKitBehaviour : MonoBehaviour
	{
		[SerializeField] public ServiceKitLocator ServiceKitLocator;

		private IServiceKitLocator _locatorOverride;
		private Type[] _cachedServiceTypes;
		private bool _isCircularDependencyExempt;
		private int _registrationGuard;

		/// <summary>
		/// Returns the active service locator. Uses the override if set, otherwise falls back to the serialized field.
		/// </summary>
		protected IServiceKitLocator Locator => _locatorOverride ?? ServiceKitLocator;

		/// <summary>
		/// Sets an override for the ServiceKitLocator. Useful for unit testing with mocks.
		/// If Awake() already ran but registration was skipped due to missing locator,
		/// this method will trigger registration automatically.
		/// </summary>
		/// <param name="locator">The IServiceKitLocator to use (can be a mock)</param>
		public void UseLocator(IServiceKitLocator locator)
		{
			_locatorOverride = locator;

			// If Awake already ran but registration was skipped due to missing locator,
			// perform registration now
			if (!IsServiceRegistered && Locator != null)
			{
				RegisterServiceWithLocator();
			}
		}

		protected bool IsServiceRegistered { get; private set; }
		protected bool IsServiceReady { get; private set; }

		/// <summary>
		/// Cached destroy cancellation token to avoid MissingReferenceException.
		/// Use this for any async operations that should be cancelled when the GameObject is destroyed.
		/// </summary>
		protected CancellationToken CachedDestroyToken { get; private set; }

		/// <summary>
		/// The types this service is registered as.
		/// Determined by the [Service] attribute, or falls back to the concrete type.
		/// </summary>
		protected Type[] ServiceTypes
		{
			get
			{
				if (_cachedServiceTypes != null) return _cachedServiceTypes;

				var attribute = GetType().GetCustomAttribute<ServiceAttribute>();
				if (attribute == null || attribute.ServiceTypes.Length == 0)
				{
					// No attribute or empty types - use concrete class type
					_cachedServiceTypes = new[] { GetType() };
					_isCircularDependencyExempt = false;
				}
				else
				{
					_cachedServiceTypes = attribute.ServiceTypes;
					_isCircularDependencyExempt = attribute.CircularDependencyExempt;
				}

				return _cachedServiceTypes;
			}
		}

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

		private void CacheDestroyToken()
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
			if (IsServiceRegistered) return;

			// Prevent concurrent double-registration (e.g., Awake + UseLocator racing)
			if (Interlocked.CompareExchange(ref _registrationGuard, 1, 0) != 0) return;

			ValidateServiceTypeImplementations();
			RegisterInstanceWithLocator();
			LogRegistrationIfDebugEnabled();
		}

		private void ValidateServiceTypeImplementations()
		{
			foreach (var serviceType in ServiceTypes)
			{
				if (!serviceType.IsInstanceOfType(this))
				{
					var errorMessage = BuildInterfaceNotImplementedMessage(GetType(), serviceType);
					LogAndThrowError(errorMessage);
				}
			}
		}

		private void RegisterInstanceWithLocator()
		{
			foreach (var serviceType in ServiceTypes)
			{
				if (_isCircularDependencyExempt)
				{
					Locator.RegisterServiceWithCircularExemption(serviceType, this);
				}
				else
				{
					Locator.RegisterService(serviceType, this);
				}
			}

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
			foreach (var serviceType in ServiceTypes)
			{
				Locator.ReadyService(serviceType);
			}

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
			foreach (var serviceType in ServiceTypes)
			{
				Locator.UnregisterService(serviceType);
			}
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
			await Locator.Inject(this)
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

			LogMissingServiceLocatorWarning();
			return true;
		}

		private bool HasServiceLocatorAssigned()
		{
			return Locator != null;
		}

		private void LogMissingServiceLocatorWarning()
		{
			Debug.LogWarning($"{GetType().Name} requires a reference to a ServiceKitLocator.", this);
		}

		private void LogAndThrowError(string errorMessage)
		{
			Debug.LogError($"[ServiceKit] {errorMessage}", this);
			throw new InvalidOperationException(errorMessage);
		}

		private static string GetImplementedInterfaceNames(Type type)
		{
			var interfaces = type.GetInterfaces();
			if (interfaces.Length == 0) return string.Empty;

			var stringBuilder = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				for (int i = 0; i < interfaces.Length; i++)
				{
					if (i > 0) stringBuilder.Append(", ");
					stringBuilder.Append(interfaces[i].Name);
				}

				return stringBuilder.ToString();
			}
			finally
			{
				ServiceKitObjectPool.ReturnStringBuilder(stringBuilder);
			}
		}

		private static string BuildInterfaceNotImplementedMessage(Type implementationType, Type serviceType)
		{
			var implementedInterfaces = GetImplementedInterfaceNames(implementationType);
			var sb = ServiceKitObjectPool.RentStringBuilder();
			try
			{
				sb.Append("Failed to register service for '");
				sb.Append(implementationType.Name);
				sb.Append("' as '");
				sb.Append(serviceType.Name);
				sb.Append("'. This typically means '");
				sb.Append(implementationType.Name);
				sb.Append("' does not implement '");
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
	}
}
