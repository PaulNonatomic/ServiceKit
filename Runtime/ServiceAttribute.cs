using System;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Specifies the service type(s) under which this class should be registered.
	/// If no types are specified, the concrete class type is used.
	///
	/// Usage:
	/// [Service(typeof(IFoo))]                    // Single interface
	/// [Service(typeof(IFoo), typeof(IBar))]      // Multiple interfaces
	/// [Service]                                   // Concrete type (class itself)
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class ServiceAttribute : Attribute
	{
		/// <summary>
		/// The types under which this service should be registered.
		/// If empty, the concrete class type is used.
		/// </summary>
		public Type[] ServiceTypes { get; }

		/// <summary>
		/// When true, this service is exempt from circular dependency detection.
		/// Default: false
		/// </summary>
		public bool CircularDependencyExempt { get; set; }

		/// <summary>
		/// Register against the concrete class type.
		/// </summary>
		public ServiceAttribute()
		{
			ServiceTypes = Array.Empty<Type>();
		}

		/// <summary>
		/// Register against a single interface/type.
		/// </summary>
		public ServiceAttribute(Type serviceType)
		{
			ServiceTypes = new[] { serviceType };
		}

		/// <summary>
		/// Register against multiple interfaces/types.
		/// </summary>
		public ServiceAttribute(params Type[] serviceTypes)
		{
			ServiceTypes = serviceTypes ?? Array.Empty<Type>();
		}
	}
}
