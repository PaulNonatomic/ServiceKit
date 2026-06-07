using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine.SceneManagement;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Core service-locator surface: registration, readiness, retrieval (sync and async), injection and
	/// resolution status. This is the slice the injection engine and most consumers actually depend on;
	/// an alternative locator that only needs dependency injection can implement just this facet.
	/// </summary>
	public interface IServiceLocator
	{
		// Phase 1: Registration (service not available yet) - Generic
		void RegisterService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		void RegisterService<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class;
		void RegisterServiceWithCircularExemption<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		void RegisterServiceWithCircularExemption<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class;

		// Phase 1: Registration (service not available yet) - Non-Generic
		void RegisterService(Type serviceType, object service, [CallerMemberName] string registeredBy = null);
		void RegisterService(Type serviceType, object service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null);
		void RegisterServiceWithCircularExemption(Type serviceType, object service, [CallerMemberName] string registeredBy = null);
		void RegisterServiceWithCircularExemption(Type serviceType, object service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null);

		// Phase 3: Ready (service becomes available)
		void ReadyService<T>() where T : class;
		void ReadyService(Type serviceType);

		// Convenience methods for simple services
		void RegisterAndReadyService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		void RegisterAndReadyService<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class;
		void RegisterAndReadyServiceWithCircularExemption<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		void RegisterAndReadyServiceWithCircularExemption<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class;

		// Status checks
		bool IsServiceRegistered<T>() where T : class;
		bool IsServiceRegistered(Type serviceType);
		bool IsServiceReady<T>() where T : class;
		bool IsServiceReady(Type serviceType);
		ServiceResolutionStatus TryResolveService(Type serviceType, out object service);

		// Service retrieval (only returns ready services)
		void UnregisterService<T>() where T : class;
		void UnregisterService(Type serviceType);
		T GetService<T>() where T : class;
		object GetService(Type serviceType);
		bool TryGetService<T>(out T service) where T : class;
		bool TryGetService(Type serviceType, out object service);
#if SERVICEKIT_UNITASK
		UniTask<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;
		UniTask<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default);
#else
		Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;
		Task<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default);
#endif

		// Injection
		[Obsolete("Use Inject(target) or the InjectAsync(target, token) extension method instead.")]
		IServiceInjectionBuilder InjectServicesAsync(object target);
		IServiceInjectionBuilder Inject(object target);
		void ClearServices();

		// Fluent registration API
		IServiceRegistrationBuilder Register<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		IServiceRegistrationBuilder Register(object service, [CallerMemberName] string registeredBy = null);
	}

	/// <summary>
	/// Tag assignment and tag-based queries. Optional facet - a locator that does not use service tags
	/// need not provide these to satisfy the core <see cref="IServiceLocator"/>.
	/// </summary>
	public interface IServiceTagRegistry
	{
		void AddTagsToService<T>(params ServiceTag[] tags) where T : class;
		void AddTagsToService(Type serviceType, params ServiceTag[] tags);
		void RemoveTagsFromService<T>(params string[] tags) where T : class;
		void RemoveTagsFromService(Type serviceType, params string[] tags);
		IReadOnlyList<string> GetServiceTags<T>() where T : class;
		IReadOnlyList<string> GetServiceTags(Type serviceType);
		IReadOnlyList<ServiceInfo> GetServicesWithTag(string tag);
		IReadOnlyList<ServiceInfo> GetServicesWithAnyTag(params string[] tags);
		IReadOnlyList<ServiceInfo> GetServicesWithAllTags(params string[] tags);
	}

	/// <summary>
	/// Scene-scoped lifecycle: enumerate services by scene and clean them up when scenes unload.
	/// Optional facet, separate from the core locator so a non-scene-aware locator can skip it.
	/// </summary>
	public interface IServiceSceneManager
	{
		IReadOnlyList<ServiceInfo> GetServicesInScene(string sceneName);
		IReadOnlyList<ServiceInfo> GetDontDestroyOnLoadServices();
		void UnregisterServicesFromScene(Scene scene);
		void CleanupDestroyedServices();
	}

	/// <summary>
	/// Inspection and diagnostics: enumerate everything, report a service's status, and query
	/// circular-dependency state. Optional facet used by tooling and the editor window.
	/// </summary>
	public interface IServiceDiagnostics
	{
		bool IsServiceCircularDependencyExempt<T>() where T : class;
		bool IsServiceCircularDependencyExempt(Type serviceType);
		bool HasCircularDependencyError<T>() where T : class;
		bool HasCircularDependencyError(Type serviceType);
		string GetServiceStatus<T>() where T : class;
		string GetServiceStatus(Type serviceType);
		IReadOnlyList<ServiceInfo> GetAllServices();
	}

	/// <summary>
	/// The full ServiceKit locator surface, composed from the core locator plus the tag, scene and
	/// diagnostics facets. Existing code keeps depending on this unchanged; new code (and alternative
	/// locator implementations) can instead depend on only the facet(s) they need.
	/// </summary>
	public interface IServiceKitLocator : IServiceLocator, IServiceTagRegistry, IServiceSceneManager, IServiceDiagnostics
	{
	}
}
