using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
	/// <summary>
	/// Verifies that circular-dependency bookkeeping (exemptions / errors) is owned per
	/// ServiceKitLocator and never leaks between independent locator instances.
	/// </summary>
	[TestFixture]
	public class DependencyGraphIsolationTests
	{
		public interface IFoo { }
		public interface IBar { }

		public class Foo : IFoo { }
		public class Bar : IBar { }

		private ServiceKitLocator _locatorA;
		private ServiceKitLocator _locatorB;

		[SetUp]
		public void Setup()
		{
			_locatorA = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_locatorB = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}

		[TearDown]
		public void TearDown()
		{
			DestroyLocator(ref _locatorA);
			DestroyLocator(ref _locatorB);
		}

		private static void DestroyLocator(ref ServiceKitLocator locator)
		{
			if (locator == null) return;
			locator.ClearServices();
			Object.DestroyImmediate(locator);
			locator = null;
		}

		[Test]
		public void CircularExemption_DoesNotLeakBetweenLocators()
		{
			// Exempt IFoo on locator A only.
			_locatorA.RegisterServiceWithCircularExemption<IFoo>(new Foo());

			Assert.IsTrue(_locatorA.IsServiceCircularDependencyExempt<IFoo>(),
				"Locator A should report its own service as exempt");
			Assert.IsFalse(_locatorB.IsServiceCircularDependencyExempt<IFoo>(),
				"Exemption registered on locator A must not be visible on locator B");
		}

		[Test]
		public void ClearingOneLocator_DoesNotClearAnother()
		{
			_locatorA.RegisterServiceWithCircularExemption<IFoo>(new Foo());
			_locatorB.RegisterServiceWithCircularExemption<IBar>(new Bar());

			// Clearing A must not wipe B's dependency-graph state.
			_locatorA.ClearServices();

			Assert.IsFalse(_locatorA.IsServiceCircularDependencyExempt<IFoo>(),
				"Locator A's exemption should be gone after ClearServices");
			Assert.IsTrue(_locatorB.IsServiceCircularDependencyExempt<IBar>(),
				"Clearing locator A must not clear locator B's exemption");
		}
	}
}
