using System;

namespace Nonatomic.ServiceKit
{
	[AttributeUsage(AttributeTargets.Field)]
	public class InjectServiceAttribute : Attribute
	{
		/// <summary>
		/// Indicates whether the service is required. 
		/// If true, injection will fail if the service is not available.
		/// If false, the field will remain null if the service is not available.
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
