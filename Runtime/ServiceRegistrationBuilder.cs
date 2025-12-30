using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Fluent builder for configuring service registration.
	/// </summary>
	public class ServiceRegistrationBuilder : IServiceRegistrationBuilder
	{
		private readonly IServiceKitLocator _locator;
		private readonly object _service;
		private readonly string _registeredBy;
		private readonly List<Type> _serviceTypes = new();
		private readonly List<ServiceTag> _tags = new();
		private bool _circularExemption;
		private bool _executed;

		internal ServiceRegistrationBuilder(
			IServiceKitLocator locator,
			object service,
			[CallerMemberName] string registeredBy = null)
		{
			_locator = locator ?? throw new ArgumentNullException(nameof(locator));
			_service = service ?? throw new ArgumentNullException(nameof(service));
			_registeredBy = registeredBy;
		}

		/// <summary>
		/// Register the service as an additional type.
		/// Can be chained multiple times for multi-type registration.
		/// </summary>
		public IServiceRegistrationBuilder As<T>() where T : class
		{
			return As(typeof(T));
		}

		/// <summary>
		/// Register the service as an additional type.
		/// Can be chained multiple times for multi-type registration.
		/// </summary>
		public IServiceRegistrationBuilder As(Type serviceType)
		{
			if (serviceType == null)
				throw new ArgumentNullException(nameof(serviceType));

			if (!serviceType.IsInstanceOfType(_service))
			{
				throw new ArgumentException(
					$"Service of type '{_service.GetType().Name}' does not implement '{serviceType.Name}'.",
					nameof(serviceType));
			}

			if (!_serviceTypes.Contains(serviceType))
			{
				_serviceTypes.Add(serviceType);
			}

			return this;
		}

		/// <summary>
		/// Add tags to the service registration.
		/// Tags can be used for filtering and organization.
		/// </summary>
		public IServiceRegistrationBuilder WithTags(params string[] tags)
		{
			if (tags == null) return this;

			foreach (var tag in tags)
			{
				if (!string.IsNullOrEmpty(tag))
				{
					_tags.Add(new ServiceTag { name = tag });
				}
			}

			return this;
		}

		/// <summary>
		/// Add tags to the service registration.
		/// Tags can be used for filtering and organization.
		/// </summary>
		public IServiceRegistrationBuilder WithTags(params ServiceTag[] tags)
		{
			if (tags == null) return this;

			_tags.AddRange(tags);
			return this;
		}

		/// <summary>
		/// Exempt this service from circular dependency detection.
		/// Use with caution - only when you understand the implications.
		/// </summary>
		public IServiceRegistrationBuilder WithCircularExemption()
		{
			_circularExemption = true;
			return this;
		}

		/// <summary>
		/// Complete registration without marking the service as ready.
		/// The service will be discoverable but not injectable until Ready() is called separately.
		/// </summary>
		public void Register()
		{
			ExecuteRegistration(markAsReady: false);
		}

		/// <summary>
		/// Complete registration and immediately mark the service as ready.
		/// This is the most common terminal operation.
		/// </summary>
		public void Ready()
		{
			ExecuteRegistration(markAsReady: true);
		}

		private void ExecuteRegistration(bool markAsReady)
		{
			if (_executed)
			{
				throw new InvalidOperationException(
					"This registration builder has already been executed. " +
					"Create a new builder for additional registrations.");
			}

			_executed = true;

			// If no types specified, use the concrete type
			if (_serviceTypes.Count == 0)
			{
				_serviceTypes.Add(_service.GetType());
			}

			var tagsArray = _tags.Count > 0 ? _tags.ToArray() : null;

			foreach (var serviceType in _serviceTypes)
			{
				RegisterServiceType(serviceType, tagsArray);

				if (markAsReady)
				{
					_locator.ReadyService(serviceType);
				}
			}
		}

		private void RegisterServiceType(Type serviceType, ServiceTag[] tags)
		{
			if (_circularExemption)
			{
				if (tags != null)
				{
					_locator.RegisterServiceWithCircularExemption(serviceType, _service, tags, _registeredBy);
				}
				else
				{
					_locator.RegisterServiceWithCircularExemption(serviceType, _service, _registeredBy);
				}
			}
			else
			{
				if (tags != null)
				{
					_locator.RegisterService(serviceType, _service, tags, _registeredBy);
				}
				else
				{
					_locator.RegisterService(serviceType, _service, _registeredBy);
				}
			}
		}
	}
}
