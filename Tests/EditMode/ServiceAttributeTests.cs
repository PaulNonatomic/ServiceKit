using System;
using System.Reflection;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nonatomic.ServiceKit.Tests.EditMode
{
	[TestFixture]
	public class ServiceAttributeTests
	{
		private interface IFoo { }
		private interface IBar { }

		private class NoAttributeBehaviour : ServiceKitBehaviour, IFoo { }

		[Service(typeof(IFoo))]
		private class SingleTypeBehaviour : ServiceKitBehaviour, IFoo { }

		[Service(typeof(IFoo), typeof(IBar))]
		private class MultiTypeBehaviour : ServiceKitBehaviour, IFoo, IBar { }

		[Service]
		private class ExplicitEmptyBehaviour : ServiceKitBehaviour { }

		[Test]
		public void ServiceAttribute_NoArgs_HasEmptyServiceTypes()
		{
			var attribute = new ServiceAttribute();
			Assert.IsNotNull(attribute.ServiceTypes);
			Assert.AreEqual(0, attribute.ServiceTypes.Length);
		}

		[Test]
		public void ServiceAttribute_SingleType_HasOneServiceType()
		{
			var attribute = new ServiceAttribute(typeof(IFoo));
			Assert.AreEqual(1, attribute.ServiceTypes.Length);
			Assert.AreEqual(typeof(IFoo), attribute.ServiceTypes[0]);
		}

		[Test]
		public void ServiceAttribute_MultipleTypes_HasAllServiceTypes()
		{
			var attribute = new ServiceAttribute(typeof(IFoo), typeof(IBar));
			Assert.AreEqual(2, attribute.ServiceTypes.Length);
			Assert.AreEqual(typeof(IFoo), attribute.ServiceTypes[0]);
			Assert.AreEqual(typeof(IBar), attribute.ServiceTypes[1]);
		}

		[Test]
		public void ServiceAttribute_CircularDependencyExempt_DefaultsFalse()
		{
			var attribute = new ServiceAttribute();
			Assert.IsFalse(attribute.CircularDependencyExempt);
		}

		[Test]
		public void ServiceAttribute_CircularDependencyExempt_CanBeSetTrue()
		{
			var attribute = new ServiceAttribute(typeof(IFoo)) { CircularDependencyExempt = true };
			Assert.IsTrue(attribute.CircularDependencyExempt);
		}

		[Test]
		public void ServiceKitBehaviourServiceTypes_NoAttribute_FallsToConcreteType()
		{
			var go = new GameObject("Test");
			try
			{
				var behaviour = go.AddComponent<NoAttributeBehaviour>();
				var prop = typeof(ServiceKitBehaviour).GetProperty("ServiceTypes", BindingFlags.Instance | BindingFlags.NonPublic);
				var types = (Type[])prop.GetValue(behaviour);

				Assert.AreEqual(1, types.Length);
				Assert.AreEqual(typeof(NoAttributeBehaviour), types[0]);
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ServiceKitBehaviourServiceTypes_WithAttribute_UsesAttributeTypes()
		{
			var go = new GameObject("Test");
			try
			{
				var behaviour = go.AddComponent<SingleTypeBehaviour>();
				var prop = typeof(ServiceKitBehaviour).GetProperty("ServiceTypes", BindingFlags.Instance | BindingFlags.NonPublic);
				var types = (Type[])prop.GetValue(behaviour);

				Assert.AreEqual(1, types.Length);
				Assert.AreEqual(typeof(IFoo), types[0]);
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}
	}
}
