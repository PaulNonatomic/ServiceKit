using System;

namespace Nonatomic.ServiceKit.Exceptions
{
	public class CircularDependencyException : Exception
	{
		public CircularDependencyException(string message) : base(message) {}
	}
}