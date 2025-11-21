using System;
using System.Linq;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Tests.EditMode
{
	[TestFixture]
	public class MultiTypeRegistrationAdvancedTests
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

		#region ReadyService Tests

		[Test]
		public void ReadyService_ByAlternateType_MakesAllTypesReady()
		{
			// Arrange
			var service = new TestAdvancedService();
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>()
				.AlsoAs<IAdvancedTertiary>();

			// Act - Ready by alternate type
			_serviceKit.ReadyService<IAdvancedSecondary>();

			// Assert - All types should be ready
			Assert.IsTrue(_serviceKit.IsServiceReady<IAdvancedPrimary>(), "Primary type should be ready");
			Assert.IsTrue(_serviceKit.IsServiceReady<IAdvancedSecondary>(), "Secondary type should be ready");
			Assert.IsTrue(_serviceKit.IsServiceReady<IAdvancedTertiary>(), "Tertiary type should be ready");
		}

		[Test]
		public void IsServiceRegistered_AllTypesReturnTrue()
		{
			// Arrange
			var service = new TestAdvancedService();
			_serviceKit.RegisterService<IAdvancedPrimary>(service, new[] { typeof(IAdvancedSecondary) });

			// Assert - Both types should be registered (but not ready yet)
			Assert.IsTrue(_serviceKit.IsServiceRegistered<IAdvancedPrimary>(), "Primary should be registered");
			Assert.IsTrue(_serviceKit.IsServiceRegistered<IAdvancedSecondary>(), "Secondary should be registered");
			Assert.IsFalse(_serviceKit.IsServiceReady<IAdvancedPrimary>(), "Primary should not be ready yet");
			Assert.IsFalse(_serviceKit.IsServiceReady<IAdvancedSecondary>(), "Secondary should not be ready yet");
		}

		#endregion

		#region Async Injection Tests

		[Test]
		public async Task InjectServicesAsync_WithMultiTypeService_InjectsCorrectly()
		{
			// Arrange
			var service = new TestAdvancedService();
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>();
			_serviceKit.ReadyService<IAdvancedPrimary>();

			var consumer = new TestConsumerWithMultipleTypes();

			// Act
			await _serviceKit.InjectServicesAsync(consumer).ExecuteAsync();

			// Assert
			Assert.IsNotNull(consumer.PrimaryService, "Primary service should be injected");
			Assert.IsNotNull(consumer.SecondaryService, "Secondary service should be injected");
			Assert.AreSame(service, consumer.PrimaryService, "Should be same instance via primary type");
			Assert.AreSame(service, consumer.SecondaryService, "Should be same instance via secondary type");
		}

		[Test]
		public async Task InjectServicesAsync_OptionalMultiTypeService_InjectsWhenReady()
		{
			// Arrange
			var service = new TestAdvancedService();
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>();

			var consumer = new TestConsumerWithOptionalMultiType();

			// Act - Start injection before service is ready
			var injectionTask = _serviceKit.InjectServicesAsync(consumer)
				.WithTimeout(5f)
				.ExecuteAsync();

			// Ready the service after a short delay
#if SERVICEKIT_UNITASK
			await UniTask.Delay(10);
#else
			await Task.Delay(10);
#endif
			_serviceKit.ReadyService<IAdvancedSecondary>(); // Ready by alternate type

			await injectionTask;

			// Assert
			Assert.IsNotNull(consumer.OptionalPrimary, "Optional primary should be injected");
			Assert.IsNotNull(consumer.OptionalSecondary, "Optional secondary should be injected");
			Assert.AreSame(service, consumer.OptionalPrimary);
			Assert.AreSame(service, consumer.OptionalSecondary);
		}

		#endregion

		#region Tags + Multi-Type Tests

		[Test]
		public void RegisterService_WithTagsAndMultipleTypes_AllTypesHaveTags()
		{
			// Arrange
			var service = new TestAdvancedService();
			var tags = new[] { new ServiceTag("TestTag"), new ServiceTag("MultiType") };

			// Act
			_serviceKit.RegisterService<IAdvancedPrimary>(service, tags)
				.AlsoAs<IAdvancedSecondary>();
			_serviceKit.ReadyService<IAdvancedPrimary>();

			// Assert - Tags should be accessible via both types
			var primaryTags = _serviceKit.GetServiceTags<IAdvancedPrimary>();
			var secondaryTags = _serviceKit.GetServiceTags<IAdvancedSecondary>();

			Assert.AreEqual(2, primaryTags.Count, "Primary should have both tags");
			Assert.AreEqual(2, secondaryTags.Count, "Secondary should have both tags");
			Assert.That(primaryTags, Contains.Item("TestTag"));
			Assert.That(secondaryTags, Contains.Item("MultiType"));
		}

		[Test]
		public void GetServicesWithTag_MultiTypeService_ReturnsOnce()
		{
			// Arrange
			var service = new TestAdvancedService();
			var tags = new[] { new ServiceTag("Shared") };

			// Act
			_serviceKit.RegisterService<IAdvancedPrimary>(service, tags)
				.AlsoAs<IAdvancedSecondary>()
				.AlsoAs<IAdvancedTertiary>();
			_serviceKit.ReadyService<IAdvancedPrimary>();

			var servicesWithTag = _serviceKit.GetServicesWithTag("Shared");

			// Assert - Should return service once, not multiple times
			Assert.AreEqual(1, servicesWithTag.Count, "Should return service only once despite multiple types");
			Assert.AreSame(service, servicesWithTag[0].Service);
		}

		#endregion

		#region ServiceInfo.AllTypes Tests

		[Test]
		public void ServiceInfo_AllTypes_ContainsAllRegisteredTypes()
		{
			// Arrange
			var service = new TestAdvancedService();

			// Act
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>()
				.AlsoAs<IAdvancedTertiary>();
			_serviceKit.ReadyService<IAdvancedPrimary>();

			// Get ServiceInfo indirectly by retrieving via primary type and checking metadata
			var retrievedService = _serviceKit.GetService<IAdvancedPrimary>();

			// Assert - We can't directly access ServiceInfo, but we can verify all types work
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedPrimary>());
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedSecondary>());
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedTertiary>());
		}

		#endregion

		#region Edge Cases and Validation

		[Test]
		public void RegisterService_DuplicateAdditionalTypes_HandledGracefully()
		{
			// Arrange
			var service = new TestAdvancedService();

			// Act - Register with duplicate type in additional types
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>()
				.AlsoAs<IAdvancedSecondary>(); // Duplicate

			_serviceKit.ReadyService<IAdvancedPrimary>();

			// Assert - Should still work correctly
			var bySecondary = _serviceKit.GetService<IAdvancedSecondary>();
			Assert.IsNotNull(bySecondary);
			Assert.AreSame(service, bySecondary);
		}

		[Test]
		public void RegisterService_PrimaryTypeAsAdditional_HandledGracefully()
		{
			// Arrange
			var service = new TestAdvancedService();

			// Act - Try to register primary type again as additional
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedPrimary>(); // Same as primary

			_serviceKit.ReadyService<IAdvancedPrimary>();

			// Assert - Should still work
			var retrieved = _serviceKit.GetService<IAdvancedPrimary>();
			Assert.IsNotNull(retrieved);
			Assert.AreSame(service, retrieved);
		}

		[Test]
		public void AlsoAs_ChainedMultipleTimes_AllTypesRegistered()
		{
			// Arrange
			var service = new TestAdvancedService();

			// Act - Chain multiple AlsoAs calls
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>()
				.AlsoAs<IAdvancedTertiary>()
				.AlsoAs<IAdvancedBase>();

			_serviceKit.ReadyService<IAdvancedPrimary>();

			// Assert - All types should be resolvable
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedPrimary>());
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedSecondary>());
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedTertiary>());
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedBase>());
		}

		[Test]
		public void UnregisterService_ByAlternateType_RemovesAllTypes()
		{
			// Arrange
			var service = new TestAdvancedService();
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>();
			_serviceKit.ReadyService<IAdvancedPrimary>();

			// Act - Unregister by alternate type
			_serviceKit.UnregisterService<IAdvancedSecondary>();

			// Assert - All types should be unregistered
			Assert.IsNull(_serviceKit.GetService<IAdvancedPrimary>(), "Primary should be unregistered");
			Assert.IsNull(_serviceKit.GetService<IAdvancedSecondary>(), "Secondary should be unregistered");
		}

		#endregion

		#region Circular Dependency Exemption with Multi-Type

		[Test]
		public void RegisterServiceWithCircularExemption_MultiType_WorksCorrectly()
		{
			// Arrange
			var service = new TestAdvancedService();

			// Act
			_serviceKit.RegisterServiceWithCircularExemption<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>();
			_serviceKit.ReadyService<IAdvancedPrimary>();

			// Assert
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedPrimary>());
			Assert.IsNotNull(_serviceKit.GetService<IAdvancedSecondary>());
		}

		#endregion

		#region Concurrent Access Tests

		[Test]
		public async Task MultipleConsumers_SameMultiTypeService_AllGetSameInstance()
		{
			// Arrange
			var service = new TestAdvancedService();
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>();
			_serviceKit.ReadyService<IAdvancedPrimary>();

			var consumer1 = new TestConsumerWithMultipleTypes();
			var consumer2 = new TestConsumerWithMultipleTypes();
			var consumer3 = new TestConsumerWithMultipleTypes();

			// Act - Inject into multiple consumers concurrently
#if SERVICEKIT_UNITASK
			await UniTask.WhenAll(
				_serviceKit.InjectServicesAsync(consumer1).ExecuteAsync(),
				_serviceKit.InjectServicesAsync(consumer2).ExecuteAsync(),
				_serviceKit.InjectServicesAsync(consumer3).ExecuteAsync()
			);
#else
			await Task.WhenAll(
				_serviceKit.InjectServicesAsync(consumer1).ExecuteAsync(),
				_serviceKit.InjectServicesAsync(consumer2).ExecuteAsync(),
				_serviceKit.InjectServicesAsync(consumer3).ExecuteAsync()
			);
#endif

			// Assert - All consumers should get same instance
			Assert.AreSame(service, consumer1.PrimaryService);
			Assert.AreSame(service, consumer2.PrimaryService);
			Assert.AreSame(service, consumer3.PrimaryService);
			Assert.AreSame(service, consumer1.SecondaryService);
			Assert.AreSame(service, consumer2.SecondaryService);
			Assert.AreSame(service, consumer3.SecondaryService);
		}

		#endregion

		#region GetServiceStatus with Multi-Type

		[Test]
		public void GetServiceStatus_MultiType_ConsistentAcrossTypes()
		{
			// Arrange
			var service = new TestAdvancedService();
			_serviceKit.RegisterService<IAdvancedPrimary>(service)
				.AlsoAs<IAdvancedSecondary>();

			// Act & Assert - Before ready
			var primaryStatus1 = _serviceKit.GetServiceStatus<IAdvancedPrimary>();
			var secondaryStatus1 = _serviceKit.GetServiceStatus<IAdvancedSecondary>();

			Assert.That(primaryStatus1, Does.Contain("Registered"));
			Assert.That(secondaryStatus1, Does.Contain("Registered"));

			// Ready and check again
			_serviceKit.ReadyService<IAdvancedPrimary>();

			var primaryStatus2 = _serviceKit.GetServiceStatus<IAdvancedPrimary>();
			var secondaryStatus2 = _serviceKit.GetServiceStatus<IAdvancedSecondary>();

			Assert.That(primaryStatus2, Does.Contain("Ready"));
			Assert.That(secondaryStatus2, Does.Contain("Ready"));
		}

		#endregion
	}

	#region Test Classes and Interfaces

	public interface IAdvancedBase
	{
		string GetBaseName();
	}

	public interface IAdvancedPrimary : IAdvancedBase
	{
		void PrimaryAction();
	}

	public interface IAdvancedSecondary
	{
		void SecondaryAction();
	}

	public interface IAdvancedTertiary
	{
		void TertiaryAction();
	}

	public class TestAdvancedService : IAdvancedPrimary, IAdvancedSecondary, IAdvancedTertiary
	{
		public string GetBaseName() => "AdvancedService";
		public void PrimaryAction() { }
		public void SecondaryAction() { }
		public void TertiaryAction() { }
	}

	public class TestConsumerWithMultipleTypes
	{
		[InjectService]
		public IAdvancedPrimary PrimaryService;

		[InjectService]
		public IAdvancedSecondary SecondaryService;
	}

	public class TestConsumerWithOptionalMultiType
	{
		[InjectService(Required = false)]
		public IAdvancedPrimary OptionalPrimary;

		[InjectService(Required = false)]
		public IAdvancedSecondary OptionalSecondary;
	}

	#endregion
}
