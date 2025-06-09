using System;
using System.Threading.Tasks;

namespace Nonatomic.ServiceKit
{
	public static class ServiceKitExtensions
	{
		/// <summary>
		/// Register a service with a factory function
		/// </summary>
		public static void RegisterServiceFactory<T>(this IServiceKitLocator serviceKitLocator, Func<T> factory) where T : class
		{
			serviceKitLocator.RegisterService(factory());
		}

		/// <summary>
		/// Register a service asynchronously
		/// </summary>
		public static async Task RegisterServiceAsync<T>(this IServiceKitLocator serviceKitLocator, Task<T> serviceTask) where T : class
		{
			var service = await serviceTask;
			serviceKitLocator.RegisterService(service);
		}

		/// <summary>
		/// Check if a service is registered
		/// </summary>
		public static bool HasService<T>(this IServiceKitLocator serviceKitLocator) where T : class
		{
			return serviceKitLocator.GetService<T>() != null;
		}

		/// <summary>
		/// Try to get a service and perform an action if it exists
		/// </summary>
		public static void WithService<T>(this IServiceKitLocator serviceKitLocator, Action<T> action) where T : class
		{
			if (serviceKitLocator.TryGetService<T>(out var service))
			{
				action(service);
			}
		}

		/// <summary>
		/// Try to get a service and return a result if it exists
		/// </summary>
		public static TResult WithService<T, TResult>(this IServiceKitLocator serviceKitLocator, Func<T, TResult> func, TResult defaultValue = default) where T : class
		{
			return serviceKitLocator.TryGetService<T>(out var service) ? func(service) : defaultValue;
		}
	}
}
