using System;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
	[TestFixture]
	public class ServiceKitTests
	{
		private IServiceKitLocator _mockServiceKitLocator;
		private IServiceInjectionBuilder _mockBuilder;
		private ServiceKitLocator _realServiceKitLocator;

		[SetUp]
		public void Setup()
		{
			// Create a real instance for implementation tests
			_realServiceKitLocator = ScriptableObject.CreateInstance<ServiceKitLocator>();

			// Create mocks for contract tests
			_mockServiceKitLocator = Substitute.For<IServiceKitLocator>();
			_mockBuilder = Substitute.For<IServiceInjectionBuilder>();

			// Setup fluent API chain for mock builder
			_mockBuilder.WithCancellation(Arg.Any<CancellationToken>()).Returns(_mockBuilder);
			_mockBuilder.WithTimeout(Arg.Any<float>()).Returns(_mockBuilder);
			_mockBuilder.WithErrorHandling(Arg.Any<Action<Exception>>()).Returns(_mockBuilder);

			_mockServiceKitLocator.InjectServicesAsync(Arg.Any<object>()).Returns(_mockBuilder);
		}

		[TearDown]
		public void TearDown()
		{
			if (_realServiceKitLocator != null)
			{
				_realServiceKitLocator.ClearServices();
				Object.DestroyImmediate(_realServiceKitLocator);
				_realServiceKitLocator = null;
			}
		}

		[Test]
		public void GetService_ReturnsRegisteredService()
		{
			// Arrange
			var expectedService = new PlayerService();
			_mockServiceKitLocator.GetService<IPlayerService>().Returns(expectedService);

			// Act
			var result = _mockServiceKitLocator.GetService<IPlayerService>();

			// Assert
			Assert.AreEqual(expectedService, result);
		}

		[Test]
		public void TryGetService_ReturnsTrueWhenServiceExists()
		{
			// Arrange
			var expectedService = new PlayerService();
			IPlayerService service;
			_mockServiceKitLocator.TryGetService(out service)
				.Returns(x =>
				{
					x[0] = expectedService;
					return true;
				});

			// Act
			bool result = _mockServiceKitLocator.TryGetService<IPlayerService>(out var actualService);

			// Assert
			Assert.IsTrue(result);
			Assert.AreEqual(expectedService, actualService);
		}

		[Test]
		public async Task GetServiceAsync_WaitsForServiceToBeReady()
		{
			// Arrange
			var playerService = new PlayerService();

			// Act
			// Start awaiting the service BEFORE it's ready
			var serviceTask = _realServiceKitLocator.GetServiceAsync<IPlayerService>();
			Assert.IsFalse(serviceTask.IsCompleted, "Task should not be complete before service is ready");

			// Now, register and ready the service
			_realServiceKitLocator.RegisterAndReadyService<IPlayerService>(playerService);

			// Await the task, which should now complete
			var result = await serviceTask;

			// Assert
			Assert.AreEqual(playerService, result);
		}

		[Test]
		public void InjectServicesAsync_ReturnsFluentBuilder()
		{
			// Arrange
			var target = new TestClass();

			// Act
			var builder = _mockServiceKitLocator.InjectServicesAsync(target);

			// Assert
			Assert.IsNotNull(builder);
			Assert.AreEqual(_mockBuilder, builder);
		}

		[Test]
		public async Task InjectServicesAsync_InjectsServicesIntoInheritedFields()
		{
			// Arrange
			var playerService = new PlayerService();
			var inventoryService = new InventoryService();
			
			// Register and immediately ready the services for injection
			_realServiceKitLocator.RegisterAndReadyService<IPlayerService>(playerService);
			_realServiceKitLocator.RegisterAndReadyService<IInventoryService>(inventoryService);
			
			var derivedInstance = new TestDerivedClass();

			// Act
			await _realServiceKitLocator.InjectServicesAsync(derivedInstance).ExecuteAsync();

			// Assert
			Assert.IsNotNull(derivedInstance.PlayerService, "PlayerService should be injected from base class");
			Assert.IsNotNull(derivedInstance.InventoryService, "InventoryService should be injected from derived class");
			Assert.AreEqual(playerService, derivedInstance.PlayerService);
			Assert.AreEqual(inventoryService, derivedInstance.InventoryService);
		}

		[Test]
		public async Task InjectServicesAsync_InjectsServicesIntoMultipleInheritanceLevels()
		{
			// Arrange
			var playerService = new PlayerService();
			var inventoryService = new InventoryService();
			
			// Register and ready services
			_realServiceKitLocator.RegisterAndReadyService<IPlayerService>(playerService);
			_realServiceKitLocator.RegisterAndReadyService<IInventoryService>(inventoryService);
			
			var deeplyDerivedInstance = new TestDeeplyDerivedClass();

			// Act
			await _realServiceKitLocator.InjectServicesAsync(deeplyDerivedInstance).ExecuteAsync();

			// Assert
			Assert.IsNotNull(deeplyDerivedInstance.PlayerService, "PlayerService should be injected from grand-parent class");
			Assert.IsNotNull(deeplyDerivedInstance.InventoryService, "InventoryService should be injected from parent class");
			Assert.AreEqual(playerService, deeplyDerivedInstance.PlayerService);
			Assert.AreEqual(inventoryService, deeplyDerivedInstance.InventoryService);
		}

		[Test]
		public async Task InjectServicesAsync_HandlesMixedRequiredAndOptionalInheritedFields()
		{
			// Arrange
			var playerService = new PlayerService();
			// Note: NOT registering InventoryService to test optional injection
			
			_realServiceKitLocator.RegisterAndReadyService<IPlayerService>(playerService);
			
			var mixedInstance = new TestMixedRequiredOptionalClass();

			// Act
			await _realServiceKitLocator.InjectServicesAsync(mixedInstance).ExecuteAsync();

			// Assert
			Assert.IsNotNull(mixedInstance.PlayerService, "Required PlayerService should be injected");
			Assert.IsNull(mixedInstance.InventoryService, "Optional InventoryService should be null when not registered");
			Assert.AreEqual(playerService, mixedInstance.PlayerService);
		}

		// Test classes for inheritance testing
		private class TestClass
		{
			[InjectService] private IPlayerService _playerService;
		}

		private class TestBaseClass
		{
			[InjectService] private IPlayerService _playerService;
			
			public IPlayerService PlayerService => _playerService;
		}

		private class TestDerivedClass : TestBaseClass
		{
			[InjectService] private IInventoryService _inventoryService;
			
			public IInventoryService InventoryService => _inventoryService;
		}

		private class TestMiddleClass : TestBaseClass
		{
			[InjectService] private IInventoryService _inventoryService;
			
			public IInventoryService InventoryService => _inventoryService;
		}

		private class TestDeeplyDerivedClass : TestMiddleClass
		{
			// This class has no direct injection fields but should inherit from both parent and grandparent
		}

		private class TestMixedRequiredOptionalClass
		{
			[InjectService(Required = true)] private IPlayerService _playerService;
			[InjectService(Required = false)] private IInventoryService _inventoryService;
			
			public IPlayerService PlayerService => _playerService;
			public IInventoryService InventoryService => _inventoryService;
		}
	}
}
