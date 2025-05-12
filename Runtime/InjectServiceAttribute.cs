using System;

namespace Nonatomic.ServiceKit
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class InjectServiceAttribute : Attribute { }
}