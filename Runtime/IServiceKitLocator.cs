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
		void RegisterService<T>(T service, [CallerMemberName] string registeredBy = null) where T : class;
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
