using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Nonatomic.ServiceKit
{
	public interface IServiceKitLocator
	{
		// Phase 1: Registration (service not available yet)
		void RegisterService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		
		// Phase 3: Ready (service becomes available)
		void ReadyService<T>() where T : class;
		void ReadyService(Type serviceType);

		string GetDependencyReport();
		
		// Convenience method for simple services
		void RegisterAndReadyService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
		
		// Status checks
		bool IsServiceRegistered<T>() where T : class;
		bool IsServiceRegistered(Type serviceType);
		bool IsServiceReady<T>() where T : class;
		bool IsServiceReady(Type serviceType);
		string GetServiceStatus<T>() where T : class;
		string GetServiceStatus(Type serviceType);
		
		// Service retrieval (only returns ready services)
		void UnregisterService<T>() where T : class;
		void UnregisterService(Type serviceType);
		T GetService<T>() where T : class;
		object GetService(Type serviceType);
		bool TryGetService<T>(out T service) where T : class;
		bool TryGetService(Type serviceType, out object service);
		Task<T> GetServiceAsync<T>(CancellationToken cancellationToken = default) where T : class;
		Task<object> GetServiceAsync(Type serviceType, CancellationToken cancellationToken = default);
		IServiceInjectionBuilder InjectServicesAsync(object target);
		void ClearServices();

		// Tooling support
		IReadOnlyList<ServiceInfo> GetAllServices();
		IReadOnlyList<ServiceInfo> GetServicesInScene(string sceneName);
		IReadOnlyList<ServiceInfo> GetDontDestroyOnLoadServices();
		void UnregisterServicesFromScene(Scene scene);
		void CleanupDestroyedServices();
	}
}