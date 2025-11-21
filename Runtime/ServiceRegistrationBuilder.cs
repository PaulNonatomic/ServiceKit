using System;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Fluent builder for registering a service under multiple types
	/// </summary>
	public class ServiceRegistrationBuilder
	{
		private readonly ServiceKitLocator _locator;
		private readonly ServiceInfo _serviceInfo;
		private readonly object _serviceInstance;

		internal ServiceRegistrationBuilder(ServiceKitLocator locator, ServiceInfo serviceInfo, object serviceInstance)
		{
			_locator = locator;
			_serviceInfo = serviceInfo;
			_serviceInstance = serviceInstance;
		}

		/// <summary>
		/// Registers the service under an additional type
		/// </summary>
		/// <typeparam name="T">The additional type to register the service as</typeparam>
		/// <returns>This builder for method chaining</returns>
		public ServiceRegistrationBuilder AlsoAs<T>() where T : class
		{
			return AlsoAs(typeof(T));
		}

		/// <summary>
		/// Registers the service under an additional type
		/// </summary>
		/// <param name="additionalType">The additional type to register the service as</param>
		/// <returns>This builder for method chaining</returns>
		public ServiceRegistrationBuilder AlsoAs(Type additionalType)
		{
			if (additionalType == null)
			{
				throw new ArgumentNullException(nameof(additionalType));
			}

			_locator.RegisterAdditionalType(_serviceInfo, _serviceInstance, additionalType);
			return this;
		}
	}
}
