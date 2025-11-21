using System;
using System.Linq;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
	[TestFixture]
	public class ServiceKitBehaviourMultiTypeTests
	{
		private ServiceKitLocator _serviceKit;
		private GameObject _testGameObject;

		[SetUp]
		public void Setup()
		{
			_serviceKit = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_testGameObject = new GameObject("TestGameObject");
		}

		[TearDown]
		public void TearDown()
		{
			if (_testGameObject != null)
			{
				UnityEngine.Object.DestroyImmediate(_testGameObject);
				_testGameObject = null;
			}

			if (_serviceKit != null)
			{
				_serviceKit.ClearServices();

				// Wait synchronously for async operations to complete before destroying the locator
				// This prevents NullReferenceExceptions in async continuations
				System.Threading.Thread.Sleep(50);

				UnityEngine.Object.DestroyImmediate(_serviceKit);
				_serviceKit = null;
			}
		}

		#region ServiceKitBehaviour GetAdditionalRegistrationTypes Tests

		[Test]
		public void ServiceKitBehaviour_WithAdditionalTypes_RegistersUnderAllTypes()
		{
			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithAdditionalTypes>();
			service.ServiceKitLocator = _serviceKit;

			// Act - Manually trigger registration (normally happens in Awake)
			service.TestRegisterServiceWithLocator();

			// Mark as ready
			_serviceKit.ReadyService<ITestServicePrimary>();

			// Assert
			var byPrimary = _serviceKit.GetService<ITestServicePrimary>();
			var byAdditional1 = _serviceKit.GetService<ITestServiceBase>();
			var byAdditional2 = _serviceKit.GetService<ITestServiceUpdatable>();

			Assert.IsNotNull(byPrimary, "Service should be resolvable by primary type");
			Assert.IsNotNull(byAdditional1, "Service should be resolvable by additional type 1");
			Assert.IsNotNull(byAdditional2, "Service should be resolvable by additional type 2");
			Assert.AreSame(service, byPrimary, "All types should resolve to same instance");
			Assert.AreSame(service, byAdditional1, "All types should resolve to same instance");
			Assert.AreSame(service, byAdditional2, "All types should resolve to same instance");
		}

		[Test]
		public void ServiceKitBehaviour_WithoutOverride_RegistersOnlyPrimaryType()
		{
			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithoutAdditionalTypes>();
			service.ServiceKitLocator = _serviceKit;

			// Act
			service.TestRegisterServiceWithLocator();
			_serviceKit.ReadyService<ITestServicePrimary>();

			// Assert
			var byPrimary = _serviceKit.GetService<ITestServicePrimary>();
			var byAdditional = _serviceKit.GetService<ITestServiceBase>();

			Assert.IsNotNull(byPrimary, "Service should be resolvable by primary type");
			Assert.IsNull(byAdditional, "Service should NOT be resolvable by non-registered type");
		}

		[Test]
		public void ServiceKitBehaviour_DerivedClass_CanExtendAdditionalTypes()
		{
			// Arrange
			var service = _testGameObject.AddComponent<DerivedTestService>();
			service.ServiceKitLocator = _serviceKit;

			// Act
			service.TestRegisterServiceWithLocator();
			_serviceKit.ReadyService<ITestServicePrimary>();

			// Assert - Should have base types + derived types
			Assert.IsNotNull(_serviceKit.GetService<ITestServicePrimary>(), "Primary type");
			Assert.IsNotNull(_serviceKit.GetService<ITestServiceBase>(), "Base class additional type");
			Assert.IsNotNull(_serviceKit.GetService<ITestServiceUpdatable>(), "Base class additional type");
			Assert.IsNotNull(_serviceKit.GetService<ITestServiceSpecial>(), "Derived class additional type");
		}

		[Test]
		public void ServiceKitBehaviour_WithInvalidAdditionalType_ThrowsException()
		{
			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithInvalidAdditionalType>();
			service.ServiceKitLocator = _serviceKit;

			// Act & Assert
			var ex = Assert.Throws<InvalidOperationException>(() =>
				service.TestRegisterServiceWithLocator());

			Assert.That(ex.Message, Does.Contain("TestServiceWithInvalidAdditionalType"));
			Assert.That(ex.Message, Does.Contain("ITestServiceUnrelated"));
			Assert.That(ex.Message, Does.Contain("does not implement"));
		}

		#endregion

		#region Inheritance Tests

		[Test]
		public void ServiceKitBehaviour_DerivedClassCanReplaceAdditionalTypes()
		{
			// Arrange
			var service = _testGameObject.AddComponent<DerivedTestServiceReplace>();
			service.ServiceKitLocator = _serviceKit;

			// Act
			service.TestRegisterServiceWithLocator();
			_serviceKit.ReadyService<ITestServicePrimary>();

			// Assert - Should have only derived types, not base types
			Assert.IsNotNull(_serviceKit.GetService<ITestServicePrimary>(), "Primary type");
			Assert.IsNull(_serviceKit.GetService<ITestServiceBase>(), "Base class type should be replaced");
			Assert.IsNotNull(_serviceKit.GetService<ITestServiceSpecial>(), "Derived class type");
		}

		#endregion
	}

	#region Test Service Interfaces

	public interface ITestServiceBase
	{
		string GetBaseName();
	}

	public interface ITestServicePrimary : ITestServiceBase
	{
		void DoPrimaryAction();
	}

	public interface ITestServiceUpdatable
	{
		void Update();
	}

	public interface ITestServiceSpecial
	{
		void DoSpecialAction();
	}

	public interface ITestServiceUnrelated
	{
		void UnrelatedMethod();
	}

	#endregion

	#region Test ServiceKitBehaviour Classes

	// Service with additional types
	public class TestServiceWithAdditionalTypes : ServiceKitBehaviour<ITestServicePrimary>, ITestServicePrimary, ITestServiceUpdatable
	{
		protected override Type[] GetAdditionalRegistrationTypes()
		{
			return new[] { typeof(ITestServiceBase), typeof(ITestServiceUpdatable) };
		}

		public string GetBaseName() => "TestService";
		public void DoPrimaryAction() { }
		public void Update() { }

		// Expose protected method for testing
		public void TestRegisterServiceWithLocator()
		{
			RegisterServiceWithLocator();
		}
	}

	// Service without override (default behavior)
	public class TestServiceWithoutAdditionalTypes : ServiceKitBehaviour<ITestServicePrimary>, ITestServicePrimary
	{
		public string GetBaseName() => "TestService";
		public void DoPrimaryAction() { }

		public void TestRegisterServiceWithLocator()
		{
			RegisterServiceWithLocator();
		}
	}

	// Base service class
	public class BaseTestService : ServiceKitBehaviour<ITestServicePrimary>, ITestServicePrimary, ITestServiceUpdatable
	{
		protected override Type[] GetAdditionalRegistrationTypes()
		{
			return new[] { typeof(ITestServiceBase), typeof(ITestServiceUpdatable) };
		}

		public string GetBaseName() => "BaseService";
		public void DoPrimaryAction() { }
		public void Update() { }

		public void TestRegisterServiceWithLocator()
		{
			RegisterServiceWithLocator();
		}
	}

	// Derived service that extends additional types
	public class DerivedTestService : BaseTestService, ITestServiceSpecial
	{
		protected override Type[] GetAdditionalRegistrationTypes()
		{
			// Extend base types with additional type
			var baseTypes = base.GetAdditionalRegistrationTypes();
			return baseTypes.Concat(new[] { typeof(ITestServiceSpecial) }).ToArray();
		}

		public void DoSpecialAction() { }
	}

	// Derived service that replaces additional types
	public class DerivedTestServiceReplace : BaseTestService, ITestServiceSpecial
	{
		protected override Type[] GetAdditionalRegistrationTypes()
		{
			// Replace base types entirely
			return new[] { typeof(ITestServiceSpecial) };
		}

		public void DoSpecialAction() { }
	}

	// Service with invalid additional type (doesn't implement it)
	public class TestServiceWithInvalidAdditionalType : ServiceKitBehaviour<ITestServicePrimary>, ITestServicePrimary
	{
		protected override Type[] GetAdditionalRegistrationTypes()
		{
			// Returns a type this class doesn't implement
			return new[] { typeof(ITestServiceUnrelated) };
		}

		public string GetBaseName() => "Invalid";
		public void DoPrimaryAction() { }

		public void TestRegisterServiceWithLocator()
		{
			RegisterServiceWithLocator();
		}
	}

	#endregion
}
