using System;

namespace Nonatomic.ServiceKit
{
	public class ServiceInjectionException : Exception
	{
		public ServiceInjectionException(string message) : base(message)
		{
		}

		public ServiceInjectionException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
