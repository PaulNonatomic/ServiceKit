using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

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

			// Start the async operation before registering the service
			Task<IPlayerService> serviceTask;
#if SERVICEKIT_UNITASK
			serviceTask = Task.Run(async () =>
			{
				var result = await _realServiceKitLocator.GetServiceAsync<IPlayerService>();
				return result;
			});
#else
			serviceTask = _realServiceKitLocator.GetServiceAsync<IPlayerService>();
#endif
			
			// Give the task a moment to start
			await Task.Delay(10);

			// Now, register and ready the service
			_realServiceKitLocator.RegisterAndReadyService<IPlayerService>(playerService);

			// Await the task, which should now complete
			var result = await serviceTask.ConfigureAwait(false);

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
#if SERVICEKIT_UNITASK
			await _realServiceKitLocator.InjectServicesAsync(derivedInstance).ExecuteAsync().AsTask().ConfigureAwait(false);
#else
			await _realServiceKitLocator.InjectServicesAsync(derivedInstance).ExecuteAsync().ConfigureAwait(false);
#endif

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
#if SERVICEKIT_UNITASK
			await _realServiceKitLocator.InjectServicesAsync(deeplyDerivedInstance).ExecuteAsync().AsTask().ConfigureAwait(false);
#else
			await _realServiceKitLocator.InjectServicesAsync(deeplyDerivedInstance).ExecuteAsync().ConfigureAwait(false);
#endif

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
#if SERVICEKIT_UNITASK
			await _realServiceKitLocator.InjectServicesAsync(mixedInstance).ExecuteAsync().AsTask().ConfigureAwait(false);
#else
			await _realServiceKitLocator.InjectServicesAsync(mixedInstance).ExecuteAsync().ConfigureAwait(false);
#endif

			// Assert
			Assert.IsNotNull(mixedInstance.PlayerService, "Required PlayerService should be injected");
			Assert.IsNull(mixedInstance.InventoryService, "Optional InventoryService should be null when not registered");
			Assert.AreEqual(playerService, mixedInstance.PlayerService);
		}

		[Test]
		public async Task ServiceKitTimeoutManager_ThreadSafetyTest_NoExceptionThrown()
		{
			// Arrange - Test the thread safety of ServiceKitTimeoutManager operations
			// This test validates that the lock-based fix prevents ArgumentOutOfRangeException
			// We'll test on the main thread but simulate rapid add/remove operations
			
			const int numOperations = 100;
			const float timeoutDuration = 0.01f; // Very short timeout
			var exceptions = new List<Exception>();
			var registrations = new List<IDisposable>();

			// Create a ServiceKitTimeoutManager on a GameObject for testing
			var go = new GameObject("TestTimeoutManager");
			var timeoutManager = go.AddComponent<ServiceKitTimeoutManager>();
			
			try
			{
				// Act - Rapidly register and dispose timeouts on main thread to test the fixed race condition
				// This simulates the scenario where GameObjects are created/destroyed rapidly
				
				// First, register a bunch of timeouts
				for (int i = 0; i < numOperations; i++)
				{
					try
					{
						var cts = new CancellationTokenSource();
						var registration = timeoutManager.RegisterTimeout(cts, timeoutDuration);
						registrations.Add(registration);
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}

				// Then rapidly dispose them while Update() might be processing
				// This tests the exact race condition that was fixed
				var disposalTasks = new List<Task>();
				
				// Create multiple tasks that dispose registrations concurrently
				for (int taskIndex = 0; taskIndex < 3; taskIndex++)
				{
					int startIndex = taskIndex * (registrations.Count / 3);
					int endIndex = Math.Min((taskIndex + 1) * (registrations.Count / 3), registrations.Count);
					
					disposalTasks.Add(Task.Run(async () =>
					{
						try
						{
							for (int i = startIndex; i < endIndex; i++)
							{
								if (i < registrations.Count)
								{
									registrations[i]?.Dispose();
								}
								await Task.Yield(); // Allow other tasks to run
							}
						}
						catch (Exception ex)
						{
							lock (exceptions)
							{
								exceptions.Add(ex);
							}
						}
					}));
				}

				// Wait for disposal tasks to complete
				await Task.WhenAll(disposalTasks);
				
				// Give some time for Update() to process any remaining timeouts
				await Task.Delay(100);

				// Test additional rapid operations to stress test the locks
				for (int i = 0; i < 50; i++)
				{
					try
					{
						var cts = new CancellationTokenSource();
						var registration = timeoutManager.RegisterTimeout(cts, timeoutDuration);
						
						// Immediately dispose to test rapid add/remove
						registration.Dispose();
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}

				// Give final processing time
				await Task.Delay(50);

				// Assert - No ArgumentOutOfRangeException should occur with the lock-based fix
				if (exceptions.Count > 0)
				{
					var aggregateException = new AggregateException(exceptions);
					Assert.Fail($"Thread safety test failed with {exceptions.Count} exceptions: {aggregateException}");
				}
				
				Assert.Pass("No race condition exceptions occurred - the lock-based fix is working correctly");
			}
			finally
			{
				// Clean up the test GameObject
				Object.DestroyImmediate(go);
			}
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
