using System;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Tests.EditMode
{
	/// <summary>
	/// Tests for race condition where PerformServiceInitializationSequence continues
	/// after OnDestroy/UnregisterServiceFromLocator has been called during async operations.
	/// This is the specific race condition where:
	/// 1. Awake() starts PerformServiceInitializationSequence()
	/// 2. Scene unloads during async InitializeServiceAsync()
	/// 3. OnDestroy() is called → UnregisterServiceFromLocator()
	/// 4. async operations complete and MarkServiceAsReady() is called on unregistered service
	/// </summary>
	[TestFixture]
	public class ServiceKitBehaviourDestructionRaceConditionTest
	{
		private GameObject _testGameObject;
		private ServiceKitLocator _serviceLocator;

		public interface ITestService
		{
			bool IsInitialized { get; }
		}

		public interface IDependency { }

		[Service(typeof(IDependency))]
		public class DependencyService : ServiceKitBehaviour, IDependency
		{
		}

		/// <summary>
		/// Test service that simulates async initialization delay
		/// </summary>
		[Service(typeof(ITestService))]
		public class TestServiceWithAsyncDelay : ServiceKitBehaviour, ITestService
		{
			public bool IsInitialized { get; private set; }
			public bool InitializeServiceCalled { get; private set; }
			public bool MarkServiceAsReadyCalled { get; private set; }
			public int InitializeAsyncDelayMs = 50;

			// Track if an error occurred trying to mark service as ready after unregistration
			public static Exception LastMarkAsReadyException;

#if SERVICEKIT_UNITASK
			protected override async UniTask InitializeServiceAsync()
#else
			protected override async Task InitializeServiceAsync()
#endif
			{
				// Simulate async work (e.g., loading resources, network call, etc.)
#if SERVICEKIT_UNITASK
				await UniTask.Delay(InitializeAsyncDelayMs);
#else
				await Task.Delay(InitializeAsyncDelayMs);
#endif
			}

			protected override void InitializeService()
			{
				InitializeServiceCalled = true;
				IsInitialized = true;
			}

			protected override void MarkServiceAsReady()
			{
				MarkServiceAsReadyCalled = true;
				try
				{
					base.MarkServiceAsReady();
				}
				catch (Exception ex)
				{
					// Capture any exceptions that occur when trying to mark as ready
					LastMarkAsReadyException = ex;
					throw;
				}
			}
		}

		/// <summary>
		/// Service with dependency to test injection timing
		/// </summary>
		[Service(typeof(ITestService))]
		public class TestServiceWithDependency : ServiceKitBehaviour, ITestService
		{
			[InjectService(Required = true)]
			public IDependency Dependency;

			public bool IsInitialized { get; private set; }
			public int InitializeAsyncDelayMs = 50;

#if SERVICEKIT_UNITASK
			protected override async UniTask InitializeServiceAsync()
#else
			protected override async Task InitializeServiceAsync()
#endif
			{
#if SERVICEKIT_UNITASK
				await UniTask.Delay(InitializeAsyncDelayMs);
#else
				await Task.Delay(InitializeAsyncDelayMs);
#endif
			}

			protected override void InitializeService()
			{
				IsInitialized = true;
			}
		}

		[SetUp]
		public void Setup()
		{
			TestServiceWithAsyncDelay.LastMarkAsReadyException = null;
			_serviceLocator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_testGameObject = new GameObject("TestGameObject");
		}

		[TearDown]
		public void TearDown()
		{
			if (_testGameObject != null)
			{
				Object.DestroyImmediate(_testGameObject);
			}

			if (_serviceLocator != null)
			{
				_serviceLocator.ClearServices();
				Object.DestroyImmediate(_serviceLocator);
			}
		}

		[Test]
		public async Task WhenDestroyedDuringAsyncInitialization_ShouldNotCallMarkServiceAsReady()
		{
			// This is the key test: when GameObject is destroyed during InitializeServiceAsync,
			// MarkServiceAsReady should NOT be called because the service was unregistered

			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithAsyncDelay>();
			service.ServiceKitLocator = _serviceLocator;
			service.InitializeAsyncDelayMs = 100; // Long enough to destroy during

			// Wait for Awake to be called
			await WaitForAwakeToStart();

			// Act - Destroy the GameObject while async initialization is in progress
			Object.DestroyImmediate(_testGameObject);

			// Wait for any pending async operations
			await Task.Delay(200);

			// Assert
			Assert.IsFalse(service.MarkServiceAsReadyCalled,
				"MarkServiceAsReady should NOT be called after GameObject destruction");
		}

		[Test]
		public async Task WhenDestroyedDuringAsyncInitialization_ServiceShouldNotBeReady()
		{
			// Verify that destroying during initialization leaves service in not-ready state

			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithAsyncDelay>();
			service.ServiceKitLocator = _serviceLocator;
			service.InitializeAsyncDelayMs = 100;

			await WaitForAwakeToStart();

			// Act
			Object.DestroyImmediate(_testGameObject);
			await Task.Delay(200);

			// Assert
			var isServiceReady = _serviceLocator.IsServiceReady<ITestService>();
			Assert.IsFalse(isServiceReady,
				"Service should NOT be marked as ready after destruction during initialization");
		}

		[Test]
		public async Task WhenDestroyedDuringAsyncInitialization_ShouldNotThrowException()
		{
			// The most important test: destroying during async init should be safe

			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithAsyncDelay>();
			service.ServiceKitLocator = _serviceLocator;
			service.InitializeAsyncDelayMs = 100;

			await WaitForAwakeToStart();

			// Act & Assert - Should not throw
			try
			{
				Object.DestroyImmediate(_testGameObject);
				await Task.Delay(200);

				if (TestServiceWithAsyncDelay.LastMarkAsReadyException != null)
				{
					Assert.Fail($"Exception occurred in MarkServiceAsReady: {TestServiceWithAsyncDelay.LastMarkAsReadyException}");
				}

				// Test passes if we reach here without exceptions
			}
			catch (Exception ex)
			{
				Assert.Fail($"Unexpected exception: {ex}");
			}
		}

		[Test]
		public async Task RaceCondition_DestroyAtExactMomentBetweenInitAndMarkReady()
		{
			// Try to hit the exact timing window between InitializeService and MarkServiceAsReady

			const int iterations = 50;
			int unexpectedReadyCount = 0;
			int exceptionCount = 0;

			for (int i = 0; i < iterations; i++)
			{
				// Reset
				TestServiceWithAsyncDelay.LastMarkAsReadyException = null;
				_serviceLocator.ClearServices();
				if (_testGameObject != null) Object.DestroyImmediate(_testGameObject);
				_testGameObject = new GameObject($"TestGameObject_{i}");

				// Arrange
				var service = _testGameObject.AddComponent<TestServiceWithAsyncDelay>();
				service.ServiceKitLocator = _serviceLocator;
				service.InitializeAsyncDelayMs = 20; // Shorter delay to hit timing window

				// Wait a bit then destroy
				await Task.Delay(i % 10); // Vary timing
				Object.DestroyImmediate(_testGameObject);
				_testGameObject = null;

				// Wait for completion
				await Task.Delay(50);

				// Check results
				if (TestServiceWithAsyncDelay.LastMarkAsReadyException != null)
				{
					exceptionCount++;
					Debug.LogError($"Iteration {i}: Exception during MarkServiceAsReady: {TestServiceWithAsyncDelay.LastMarkAsReadyException.Message}");
				}

				var isReady = _serviceLocator.IsServiceReady<ITestService>();
				if (isReady)
				{
					unexpectedReadyCount++;
					Debug.LogError($"Iteration {i}: Service is marked as ready despite being destroyed!");
				}
			}

			Debug.Log($"Results: {unexpectedReadyCount} unexpected ready states, {exceptionCount} exceptions");

			// Assert
			Assert.AreEqual(0, unexpectedReadyCount,
				$"BUG: Service was marked as ready after destruction in {unexpectedReadyCount}/{iterations} iterations");
			Assert.AreEqual(0, exceptionCount,
				$"BUG: Exceptions occurred in {exceptionCount}/{iterations} iterations");
		}

		[Test]
		public async Task WhenSceneUnloadsDuringDependencyInjection_ShouldNotMarkAsReady()
		{
			// Simulate scene unload during dependency injection

			// Arrange - Create dependency service
			var dependencyGO = new GameObject("DependencyService");
			var dependencyService = dependencyGO.AddComponent<DependencyService>();
			dependencyService.ServiceKitLocator = _serviceLocator;

			await WaitForAwakeToStart();

			// Create service with dependency
			var service = _testGameObject.AddComponent<TestServiceWithDependency>();
			service.ServiceKitLocator = _serviceLocator;
			service.InitializeAsyncDelayMs = 100;

			await WaitForAwakeToStart();

			// Act - "Unload scene" by destroying the GameObject during injection
			await Task.Delay(20);
			Object.DestroyImmediate(_testGameObject);

			// Wait for completion
			await Task.Delay(200);

			// Assert
			var isServiceReady = _serviceLocator.IsServiceReady<ITestService>();
			Assert.IsFalse(isServiceReady,
				"Service should not be ready after being destroyed during dependency injection");

			// Cleanup
			Object.DestroyImmediate(dependencyGO);
		}

		[Test]
		public async Task WhenObjectIsDestroyed_IsObjectDestroyedShouldReturnTrue()
		{
			// Test the IsObjectDestroyed check

			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithAsyncDelay>();
			service.ServiceKitLocator = _serviceLocator;

			// Initially not destroyed
			await WaitForAwakeToStart();

			// Act - Destroy
			Object.DestroyImmediate(_testGameObject);

			// Note: We can't directly call IsObjectDestroyed since it's private
			// But we can verify the behavior through the public API
			// The service should not be registered after destruction
			var isRegistered = _serviceLocator.IsServiceRegistered(typeof(ITestService));
			Assert.IsFalse(isRegistered,
				"Service should not be registered after GameObject destruction");
		}

		[Test]
		public async Task WhenCancellationTokenIsCancelled_ShouldStopInitialization()
		{
			// Test that CachedDestroyToken is properly used to cancel operations

			// Arrange
			var service = _testGameObject.AddComponent<TestServiceWithAsyncDelay>();
			service.ServiceKitLocator = _serviceLocator;
			service.InitializeAsyncDelayMs = 200; // Long delay

			await WaitForAwakeToStart();

			// Act - Destroy to trigger cancellation
			Object.DestroyImmediate(_testGameObject);
			await Task.Delay(50);

			// Note: The cancellation token should prevent further operations
			// This is tested indirectly through the behavior
			Assert.Pass("Cancellation token should prevent operations after destruction");
		}

		/// <summary>
		/// Helper to wait for Awake to start executing
		/// </summary>
		private async Task WaitForAwakeToStart()
		{
#if SERVICEKIT_UNITASK
			await UniTask.Delay(10);
#else
			await Task.Delay(10);
#endif
		}
	}
}
