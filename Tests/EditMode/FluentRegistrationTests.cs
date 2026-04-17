using System;
using NUnit.Framework;
using UnityEngine;

namespace Nonatomic.ServiceKit.Tests.EditMode
{
	[TestFixture]
	public class FluentRegistrationTests
	{
		private ServiceKitLocator _locator;

		[SetUp]
		public void Setup()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}

		[TearDown]
		public void TearDown()
		{
			if (_locator != null)
			{
				_locator.ClearServices();
				UnityEngine.Object.DestroyImmediate(_locator);
				_locator = null;
			}
		}

		[Test]
		public void Register_WithAs_RegistersServiceUnderSpecifiedType()
		{
			// Arrange
			var service = new TestService();

			// Act
			_locator.Register(service)
				.As<ITestService>()
				.Ready();

			// Assert
			Assert.IsTrue(_locator.IsServiceReady<ITestService>());
			Assert.AreEqual(service, _locator.GetService<ITestService>());
		}

		[Test]
		public void Register_WithMultipleAs_RegistersServiceUnderAllTypes()
		{
			// Arrange
			var service = new MultiInterfaceService();

			// Act
			_locator.Register(service)
				.As<ITestService>()
				.As<IAnotherService>()
				.Ready();

			// Assert
			Assert.IsTrue(_locator.IsServiceReady<ITestService>());
			Assert.IsTrue(_locator.IsServiceReady<IAnotherService>());
			Assert.AreSame(service, _locator.GetService<ITestService>());
			Assert.AreSame(service, _locator.GetService<IAnotherService>());
		}

		[Test]
		public void Register_WithoutAs_RegistersAsConcreteType()
		{
			// Arrange
			var service = new TestService();

			// Act
			_locator.Register(service).Ready();

			// Assert
			Assert.IsTrue(_locator.IsServiceReady<TestService>());
			Assert.AreEqual(service, _locator.GetService<TestService>());
		}

		[Test]
		public void Register_WithTags_AddsTagsToService()
		{
			// Arrange
			var service = new TestService();

			// Act
			_locator.Register(service)
				.As<ITestService>()
				.WithTags("core", "player")
				.Ready();

			// Assert
			var services = _locator.GetServicesWithTag("core");
			Assert.AreEqual(1, services.Count);
			Assert.AreEqual(service, services[0].Service);

			var playerServices = _locator.GetServicesWithTag("player");
			Assert.AreEqual(1, playerServices.Count);
		}

		[Test]
		public void Register_WithCircularExemption_ExemptsFromCircularDependencyCheck()
		{
			// Arrange
			var service = new TestService();

			// Act
			_locator.Register(service)
				.As<ITestService>()
				.WithCircularExemption()
				.Ready();

			// Assert
			Assert.IsTrue(_locator.IsServiceCircularDependencyExempt<ITestService>());
		}

		[Test]
		public void Register_CalledTwice_ThrowsInvalidOperationException()
		{
			// Arrange
			var service = new TestService();
			var builder = _locator.Register(service).As<ITestService>();

			// Act
			builder.Ready();

			// Assert
			Assert.Throws<InvalidOperationException>(() => builder.Ready());
		}

		[Test]
		public void Register_WithRegisterOnly_DoesNotMarkAsReady()
		{
			// Arrange
			var service = new TestService();

			// Act
			_locator.Register(service)
				.As<ITestService>()
				.Register();

			// Assert
			Assert.IsTrue(_locator.IsServiceRegistered<ITestService>());
			Assert.IsFalse(_locator.IsServiceReady<ITestService>());
		}

		[Test]
		public void Register_AsNonImplementedInterface_ThrowsArgumentException()
		{
			// Arrange
			var service = new TestService(); // Does not implement IAnotherService

			// Act & Assert
			Assert.Throws<ArgumentException>(() =>
			{
				_locator.Register(service)
					.As<IAnotherService>()
					.Ready();
			});
		}

		[Test]
		public void Register_GenericVersion_RegistersAsConcreteTypeNotGenericParameter()
		{
			// Arrange
			var service = new TestService();

			// Act - Register<T> uses T for type inference only, NOT as the registration type.
			// The service registers as its concrete type unless .As<>() is used.
			_locator.Register<ITestService>(service).Ready();

			// Assert
			Assert.IsTrue(_locator.IsServiceReady<TestService>());
			Assert.IsFalse(_locator.IsServiceReady<ITestService>());
		}

		[Test]
		public void Register_ChainedFluently_WorksCorrectly()
		{
			// Arrange
			var service = new MultiInterfaceService();

			// Act
			_locator.Register(service)
				.As<ITestService>()
				.As<IAnotherService>()
				.WithTags("audio", "core")
				.WithCircularExemption()
				.Ready();

			// Assert
			Assert.IsTrue(_locator.IsServiceReady<ITestService>());
			Assert.IsTrue(_locator.IsServiceReady<IAnotherService>());
			Assert.IsTrue(_locator.IsServiceCircularDependencyExempt<ITestService>());
			Assert.IsTrue(_locator.IsServiceCircularDependencyExempt<IAnotherService>());

			var audioServices = _locator.GetServicesWithTag("audio");
			Assert.AreEqual(2, audioServices.Count); // Both registrations have the tag
		}

		// Test interfaces and classes
		private interface ITestService
		{
			void DoSomething();
		}

		private interface IAnotherService
		{
			void DoSomethingElse();
		}

		private class TestService : ITestService
		{
			public void DoSomething() { }
		}

		private class MultiInterfaceService : ITestService, IAnotherService
		{
			public void DoSomething() { }
			public void DoSomethingElse() { }
		}
	}
}
