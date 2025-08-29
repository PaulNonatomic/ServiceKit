using System;

namespace Nonatomic.ServiceKit
{
	[AttributeUsage(AttributeTargets.Field)]
	public class InjectServiceAttribute : Attribute
	{
		/// <summary>
		/// Indicates whether the service is required for successful injection.
		/// 
		/// When true (default): Injection will always wait for the service and fail if not available within timeout.
		/// 
		/// When false: Uses intelligent 3-state dependency resolution:
		/// • If service is ready → inject immediately
		/// • If service is registered but not ready → wait for it (treat as required temporarily)
		/// • If service is not registered → skip injection (field remains null)
		/// 
		/// This allows optional dependencies to be automatically resolved when available,
		/// while not blocking injection when the service will never be registered.
		/// </summary>
		public bool Required { get; set; } = true;

		public InjectServiceAttribute()
		{
		}

		public InjectServiceAttribute(bool required)
		{
			Required = required;
		}
	}
}
