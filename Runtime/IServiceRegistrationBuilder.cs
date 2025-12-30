using System;

namespace Nonatomic.ServiceKit
{
	/// <summary>
	/// Fluent builder for configuring service registration.
	///
	/// Usage:
	/// serviceKit.Register(myService)
	///     .As&lt;IMyService&gt;()
	///     .As&lt;IOtherInterface&gt;()
	///     .WithTags("core", "player")
	///     .WithCircularExemption()
	///     .Ready();
	/// </summary>
	public interface IServiceRegistrationBuilder
	{
		/// <summary>
		/// Register the service as an additional type.
		/// Can be chained multiple times for multi-type registration.
		/// </summary>
		IServiceRegistrationBuilder As<T>() where T : class;

		/// <summary>
		/// Register the service as an additional type.
		/// Can be chained multiple times for multi-type registration.
		/// </summary>
		IServiceRegistrationBuilder As(Type serviceType);

		/// <summary>
		/// Add tags to the service registration.
		/// Tags can be used for filtering and organization.
		/// </summary>
		IServiceRegistrationBuilder WithTags(params string[] tags);

		/// <summary>
		/// Add tags to the service registration.
		/// Tags can be used for filtering and organization.
		/// </summary>
		IServiceRegistrationBuilder WithTags(params ServiceTag[] tags);

		/// <summary>
		/// Exempt this service from circular dependency detection.
		/// Use with caution - only when you understand the implications.
		/// </summary>
		IServiceRegistrationBuilder WithCircularExemption();

		/// <summary>
		/// Complete registration without marking the service as ready.
		/// The service will be discoverable but not injectable until Ready() is called separately.
		/// </summary>
		void Register();

		/// <summary>
		/// Complete registration and immediately mark the service as ready.
		/// This is the most common terminal operation.
		/// </summary>
		void Ready();
	}
}
