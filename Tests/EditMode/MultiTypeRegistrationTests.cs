using System;
using System.Linq;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
	[TestFixture]
	public class MultiTypeRegistrationTests
	{
		private ServiceKitLocator _serviceKit;

		[SetUp]
		public void Setup()
		{
			_serviceKit = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}

		[TearDown]
		public void TearDown()
		{
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

		#region Manual Registration with Additional Types

		[Test]
		public void RegisterService_WithAdditionalTypes_ServiceResolvableByAllTypes()
		{
			// Arrange
			var service = new TestMultiInterfaceService();
			var additionalTypes = new[] { typeof(ITestBaseService), typeof(ITestGameService) };

			// Act
			_serviceKit.RegisterService<ITestPlayerService>(service, additionalTypes);
			_serviceKit.ReadyService<ITestPlayerService>();

			// Assert
			var byPrimary = _serviceKit.GetService<ITestPlayerService>();
			var byBase = _serviceKit.GetService<ITestBaseService>();
			var byGame = _serviceKit.GetService<ITestGameService>();

			Assert.IsNotNull(byPrimary, "Service should be resolvable by primary type");
			Assert.IsNotNull(byBase, "Service should be resolvable by base type");
			Assert.IsNotNull(byGame, "Service should be resolvable by game type");
			Assert.AreSame(service, byPrimary, "Primary type should return same instance");
			Assert.AreSame(service, byBase, "Base type should return same instance");
			Assert.AreSame(service, byGame, "Game type should return same instance");
		}

		[Test]
		public void RegisterService_WithAdditionalTypes_AllTypesBecomeReady()
		{
			// Arrange
			var service = new TestMultiInterfaceService();
			var additionalTypes = new[] { typeof(ITestBaseService) };

			// Act
			_serviceKit.RegisterService<ITestPlayerService>(service, additionalTypes);
			_serviceKit.ReadyService<ITestPlayerService>();

			// Assert
			Assert.IsTrue(_serviceKit.IsServiceReady<ITestPlayerService>(), "Primary type should be ready");
			Assert.IsTrue(_serviceKit.IsServiceReady<ITestBaseService>(), "Additional type should be ready");
		}

		[Test]
		public void RegisterService_WithInvalidAdditionalType_ThrowsException()
		{
			// Arrange
			var service = new TestMultiInterfaceService();
			var invalidType = typeof(ITestUnrelatedService); // Service doesn't implement this

			// Act & Assert
			var ex = Assert.Throws<InvalidOperationException>(() =>
				_serviceKit.RegisterService<ITestPlayerService>(service, new[] { invalidType }));

			Assert.That(ex.Message, Does.Contain("TestMultiInterfaceService"));
			Assert.That(ex.Message, Does.Contain("ITestUnrelatedService"));
			Assert.That(ex.Message, Does.Contain("does not implement"));
		}

		[Test]
		public void UnregisterService_RemovesAllRegisteredTypes()
		{
			// Arrange
			var service = new TestMultiInterfaceService();
			var additionalTypes = new[] { typeof(ITestBaseService), typeof(ITestGameService) };
			_serviceKit.RegisterService<ITestPlayerService>(service, additionalTypes);
			_serviceKit.ReadyService<ITestPlayerService>();

			// Act
			_serviceKit.UnregisterService<ITestPlayerService>();

			// Assert
			Assert.IsNull(_serviceKit.GetService<ITestPlayerService>(), "Primary type should be unregistered");
			Assert.IsNull(_serviceKit.GetService<ITestBaseService>(), "Additional type should be unregistered");
			Assert.IsNull(_serviceKit.GetService<ITestGameService>(), "Additional type should be unregistered");
		}

		#endregion

		#region Builder Pattern Tests

		[Test]
		public void RegisterService_WithBuilderPattern_ServiceResolvableByAllTypes()
		{
			// Arrange
			var service = new TestMultiInterfaceService();

			// Act
			_serviceKit.RegisterService<ITestPlayerService>(service)
				.AlsoAs<ITestBaseService>()
				.AlsoAs<ITestGameService>();
			_serviceKit.ReadyService<ITestPlayerService>();

			// Assert
			var byPrimary = _serviceKit.GetService<ITestPlayerService>();
			var byBase = _serviceKit.GetService<ITestBaseService>();
			var byGame = _serviceKit.GetService<ITestGameService>();

			Assert.IsNotNull(byPrimary);
			Assert.IsNotNull(byBase);
			Assert.IsNotNull(byGame);
			Assert.AreSame(service, byPrimary);
			Assert.AreSame(service, byBase);
			Assert.AreSame(service, byGame);
		}

		[Test]
		public void BuilderPattern_AlsoAs_WithInvalidType_ThrowsException()
		{
			// Arrange
			var service = new TestMultiInterfaceService();

			// Act
			var builder = _serviceKit.RegisterService<ITestPlayerService>(service);

			// Assert
			var ex = Assert.Throws<InvalidOperationException>(() =>
				builder.AlsoAs<ITestUnrelatedService>());

			Assert.That(ex.Message, Does.Contain("TestMultiInterfaceService"));
			Assert.That(ex.Message, Does.Contain("ITestUnrelatedService"));
		}

		[Test]
		public void BuilderPattern_AlsoAs_WithNullType_ThrowsException()
		{
			// Arrange
			var service = new TestMultiInterfaceService();
			var builder = _serviceKit.RegisterService<ITestPlayerService>(service);

			// Act & Assert
			var ex = Assert.Throws<ArgumentNullException>(() => builder.AlsoAs(null));
			Assert.That(ex.ParamName, Is.EqualTo("additionalType"));
		}

		#endregion

		#region ServiceInfo Tests

		[Test]
		public void ServiceInfo_TracksAllRegisteredTypes()
		{
			// Arrange
			var service = new TestMultiInterfaceService();
			var additionalTypes = new[] { typeof(ITestBaseService), typeof(ITestGameService) };

			// Act
			_serviceKit.RegisterService<ITestPlayerService>(service, additionalTypes);
			_serviceKit.ReadyService<ITestPlayerService>();

			// Get the service info indirectly by checking the service is available
			var retrievedService = _serviceKit.GetService<ITestPlayerService>();

			// Assert
			Assert.IsNotNull(retrievedService, "Service should be retrievable");
			// Note: ServiceInfo is internal, so we verify by checking all types are resolvable
			Assert.IsNotNull(_serviceKit.GetService<ITestPlayerService>());
			Assert.IsNotNull(_serviceKit.GetService<ITestBaseService>());
			Assert.IsNotNull(_serviceKit.GetService<ITestGameService>());
		}

		#endregion

		#region First-Registered Priority Tests

		[Test]
		public void MultipleServices_SameBaseType_FirstRegisteredWins()
		{
			// Arrange
			var service1 = new TestMultiInterfaceService();
			var service2 = new TestAlternativeService();

			// Act - Both register as ITestBaseService
			_serviceKit.RegisterService<ITestPlayerService>(service1, new[] { typeof(ITestBaseService) });
			_serviceKit.RegisterService<ITestAlternativeService>(service2, new[] { typeof(ITestBaseService) });
			_serviceKit.ReadyService<ITestPlayerService>();
			_serviceKit.ReadyService<ITestAlternativeService>();

			// Assert - First registered service should be returned
			var resolved = _serviceKit.GetService<ITestBaseService>();
			Assert.AreSame(service1, resolved, "First registered service should be returned");
		}

		#endregion

		#region Validation Tests

		[Test]
		public void RegisterService_NullAdditionalType_ThrowsArgumentNullException()
		{
			// Arrange
			var service = new TestMultiInterfaceService();
			var typesWithNull = new Type[] { typeof(ITestBaseService), null };

			// Act & Assert
			var ex = Assert.Throws<ArgumentNullException>(() =>
				_serviceKit.RegisterService<ITestPlayerService>(service, typesWithNull));

			Assert.That(ex.Message, Does.Contain("Additional type cannot be null"));
		}

		#endregion
	}

	#region Test Service Classes and Interfaces

	// Base interface
	public interface ITestBaseService
	{
		string GetName();
	}

	// Derived interfaces
	public interface ITestPlayerService : ITestBaseService
	{
		void DoPlayerAction();
	}

	public interface ITestGameService
	{
		void Update();
	}

	public interface ITestAlternativeService : ITestBaseService
	{
		void DoAlternativeAction();
	}

	// Unrelated interface for negative tests
	public interface ITestUnrelatedService
	{
		void UnrelatedMethod();
	}

	// Service that implements multiple interfaces
	public class TestMultiInterfaceService : ITestPlayerService, ITestGameService
	{
		public string GetName() => "TestMultiInterfaceService";
		public void DoPlayerAction() { }
		public void Update() { }
	}

	// Alternative service for testing first-registered priority
	public class TestAlternativeService : ITestAlternativeService
	{
		public string GetName() => "TestAlternativeService";
		public void DoAlternativeAction() { }
	}

	#endregion
}
