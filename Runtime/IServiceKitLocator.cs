using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine.SceneManagement;

#if INCLUDE_UNITASK
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	public interface IServiceKitLocator
	{
		// Phase 1: Registration (service not available yet)
		void RegisterService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		
		// Phase 1: Registration with tags
		void RegisterService<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class;
		
		// Phase 1: Registration with circular dependency exemption
		void RegisterServiceWithCircularExemption<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		
		// Phase 1: Registration with circular dependency exemption and tags
		void RegisterServiceWithCircularExemption<T>(T service, ServiceTag[] tags, [CallerMemberName] string registeredBy = null) where T : class;
		
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
		bool IsServiceCircularDependencyExempt<T>() where T : class;
		bool IsServiceCircularDependencyExempt(Type serviceType);
		string GetServiceStatus<T>() where T : class;
		string GetServiceStatus(Type serviceType);
		
		// Service retrieval (only returns ready services)
		void UnregisterService<T>() where T : class;
		void UnregisterService(Type serviceType);
		T GetService<T>() where T : class;
		object GetService(Type serviceType);
		bool TryGetService<T>(out T service) where T : class;
		bool TryGetService(Type serviceType, out object service);
#if INCLUDE_UNITASK
		UniTask<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;
		UniTask<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default);
#else
		Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;
		Task<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default);
#endif
		IServiceInjectionBuilder InjectServicesAsync(object target);
		void ClearServices();

		// Tooling support
		IReadOnlyList<ServiceInfo> GetAllServices();
		IReadOnlyList<ServiceInfo> GetServicesInScene(string sceneName);
		IReadOnlyList<ServiceInfo> GetDontDestroyOnLoadServices();
		void UnregisterServicesFromScene(Scene scene);
		void CleanupDestroyedServices();
		
		// Tag management
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
}