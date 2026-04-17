using System;
using System.Threading;
using System.Threading.Tasks;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit
{
	public static class ServiceKitExtensions
	{
		/// <summary>
		/// Inject all [InjectService] fields on the target using default timeout,
		/// cancellation, and error handling. This is the recommended one-liner for
		/// most injection scenarios.
		/// </summary>
#if SERVICEKIT_UNITASK
		public static UniTask InjectAsync(this IServiceKitLocator locator, object target, CancellationToken cancellationToken)
#else
		public static Task InjectAsync(this IServiceKitLocator locator, object target, CancellationToken cancellationToken)
#endif
		{
			return locator.Inject(target)
				.WithTimeout()
				.WithCancellation(cancellationToken)
				.WithErrorHandling()
				.ExecuteAsync();
		}

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
