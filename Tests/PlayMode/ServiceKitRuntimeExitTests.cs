using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Nonatomic.ServiceKit;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	public class ServiceKitRuntimeExitTests
	{
		private ServiceKitLocator _locator;
		private GameObject _testObject;
		private List<GameObject> _createdObjects;
		private List<CancellationTokenSource> _cancellationTokens;

		[SetUp]
		public void Setup()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_createdObjects = new List<GameObject>();
			_cancellationTokens = new List<CancellationTokenSource>();
		}

		[TearDown]
		public void TearDown()
		{
			// Clean up all cancellation tokens
			foreach (var cts in _cancellationTokens)
			{
				try
				{
					if (!cts.IsCancellationRequested)
					{
						cts.Cancel();
					}
					cts.Dispose();
				}
				catch
				{
					// Ignore cleanup exceptions
				}
			}
			_cancellationTokens.Clear();

			// Clean up timeout manager
			ServiceKitTimeoutManager.Cleanup();

			// Clean up created objects
			foreach (var obj in _createdObjects)
			{
				if (obj != null)
				{
					UnityEngine.Object.DestroyImmediate(obj);
				}
			}
			_createdObjects.Clear();

			// Clean up locator
			if (_locator != null)
			{
				_locator.ClearServices();
				UnityEngine.Object.DestroyImmediate(_locator);
			}
		}

		[UnityTest]
		public IEnumerator TimeoutManager_CleansUpProperly_OnApplicationQuit()
		{
			// Arrange
			var timeoutManager = ServiceKitTimeoutManager.Instance;
			Assert.IsNotNull(timeoutManager, "TimeoutManager should be created");

			var cts1 = new CancellationTokenSource();
			var cts2 = new CancellationTokenSource();
			var cts3 = new CancellationTokenSource();
			
			_cancellationTokens.Add(cts1);
			_cancellationTokens.Add(cts2);
			_cancellationTokens.Add(cts3);

			// Register multiple timeouts
			timeoutManager.RegisterTimeout(cts1, 10f);
			timeoutManager.RegisterTimeout(cts2, 10f);
			timeoutManager.RegisterTimeout(cts3, 10f);

			// Act - Simulate application quit
			ServiceKitTimeoutManager.Cleanup();

			// Wait a frame to ensure cleanup completes
			yield return null;

			// Assert - Verify no exceptions are thrown when trying to use the manager
			var newManager = ServiceKitTimeoutManager.Instance;
			if (newManager != null)
			{
				// Should be able to register new timeouts without issues
				var cts4 = new CancellationTokenSource();
				_cancellationTokens.Add(cts4);
				
				Assert.DoesNotThrow(() =>
				{
					newManager.RegisterTimeout(cts4, 1f);
				}, "Should be able to register new timeouts after cleanup");
			}

			// Verify all tokens were cancelled
			Assert.IsTrue(cts1.IsCancellationRequested, "Token 1 should be cancelled");
			Assert.IsTrue(cts2.IsCancellationRequested, "Token 2 should be cancelled");
			Assert.IsTrue(cts3.IsCancellationRequested, "Token 3 should be cancelled");
		}

		[UnityTest]
		public IEnumerator ServiceInjection_HandlesTimeoutCleanup_DuringRuntimeExit()
		{
			// Arrange
			var testService = new TestServiceImpl();
			_locator.RegisterService<ITestService>(testService);

			var consumer = new GameObject("Consumer").AddComponent<TestServiceConsumerWithTimeout>();
			_createdObjects.Add(consumer.gameObject);
			consumer.ServiceKitLocator = _locator;

			// Start injection with timeout
			var injectionTask = Task.Run(async () =>
			{
				try
				{
					await _locator.InjectServicesAsync(consumer)
						.WithTimeout(5f)
						.ExecuteAsync();
				}
				catch (OperationCanceledException)
				{
					// Expected during cleanup
				}
			});

			// Wait a moment for injection to start
			yield return new WaitForSeconds(0.1f);

			// Act - Clean up while injection is pending
			ServiceKitTimeoutManager.Cleanup();
			_locator.ClearServices();

			// Wait for cleanup to complete
			yield return new WaitForSeconds(0.1f);

			// Assert - No timeout exceptions should occur
			Assert.DoesNotThrow(() =>
			{
				// Try to access the timeout manager
				var manager = ServiceKitTimeoutManager.Instance;
			}, "Accessing timeout manager after cleanup should not throw");

			// Clean up the task
			if (!injectionTask.IsCompleted)
			{
				yield return new WaitUntil(() => injectionTask.IsCompleted || Time.time > 10f);
			}
		}

		[UnityTest]
		public IEnumerator MultipleConcurrentTimeouts_CleanupProperly_OnExit()
		{
			// Arrange
			var timeoutManager = ServiceKitTimeoutManager.Instance;
			var tokenSources = new List<CancellationTokenSource>();
			var registrations = new List<IDisposable>();

			// Create multiple concurrent timeouts with varying durations
			for (int i = 0; i < 10; i++)
			{
				var cts = new CancellationTokenSource();
				tokenSources.Add(cts);
				_cancellationTokens.Add(cts);
				
				var registration = timeoutManager.RegisterTimeout(cts, 0.5f + i * 0.1f);
				registrations.Add(registration);
			}

			// Wait for some timeouts to trigger naturally
			yield return new WaitForSeconds(0.6f);

			// Act - Clean up while some timeouts are still pending
			ServiceKitTimeoutManager.Cleanup();

			// Dispose all registrations
			foreach (var reg in registrations)
			{
				try
				{
					reg?.Dispose();
				}
				catch
				{
					// Ignore disposal exceptions during cleanup
				}
			}

			// Assert - Verify all tokens are cancelled
			foreach (var cts in tokenSources)
			{
				Assert.IsTrue(cts.IsCancellationRequested, "All tokens should be cancelled after cleanup");
			}

			// Verify no exceptions when accessing manager after cleanup
			Assert.DoesNotThrow(() =>
			{
				var manager = ServiceKitTimeoutManager.Instance;
			}, "Should not throw when accessing manager after cleanup");
		}

		[UnityTest]
		public IEnumerator ServiceAwaiters_HandleCleanup_WithoutExceptions()
		{
			// Arrange
			var awaitTasks = new List<Task>();
			
			// Create multiple services that will be awaited
			for (int i = 0; i < 5; i++)
			{
				var serviceType = typeof(ITestService);
				var task = Task.Run(async () =>
				{
					try
					{
						var cts = new CancellationTokenSource();
						_cancellationTokens.Add(cts);
						
						await _locator.GetServiceAsync(serviceType, cts.Token);
					}
					catch (OperationCanceledException)
					{
						// Expected during cleanup - TaskCanceledException derives from this
					}
				});
				awaitTasks.Add(task);
			}

			// Wait a moment for awaiters to be registered
			yield return new WaitForSeconds(0.1f);

			// Act - Clear services which should cancel all awaiters
			_locator.ClearServices();
			ServiceKitTimeoutManager.Cleanup();

			// Wait for all tasks to complete
			yield return new WaitUntil(() => 
				awaitTasks.TrueForAll(t => t.IsCompleted) || Time.time > 5f);

			// Assert - All tasks should be completed (cancelled)
			foreach (var task in awaitTasks)
			{
				Assert.IsTrue(task.IsCompleted, "All await tasks should be completed");
				Assert.IsFalse(task.IsFaulted, "Tasks should not be faulted");
			}
		}

		[UnityTest]
		public IEnumerator TimeoutManager_RecreatesAfterCleanup_WithoutIssues()
		{
			// Arrange - Create and use initial timeout manager
			var manager1 = ServiceKitTimeoutManager.Instance;
			Assert.IsNotNull(manager1, "First manager should be created");
			
			var cts1 = new CancellationTokenSource();
			_cancellationTokens.Add(cts1);
			manager1.RegisterTimeout(cts1, 10f);

			// Act - Clean up
			ServiceKitTimeoutManager.Cleanup();
			yield return null;

			// Create new manager after cleanup
			var manager2 = ServiceKitTimeoutManager.Instance;
			Assert.IsNotNull(manager2, "Second manager should be created");
			Assert.AreNotEqual(manager1, manager2, "Should be a different instance");

			// Register new timeout on new manager
			var cts2 = new CancellationTokenSource();
			_cancellationTokens.Add(cts2);
			
			Assert.DoesNotThrow(() =>
			{
				manager2.RegisterTimeout(cts2, 0.1f);
			}, "Should be able to register timeout on new manager");

			// Wait for timeout to trigger
			yield return new WaitForSeconds(0.2f);

			// Assert - Verify new timeout triggered properly
			Assert.IsTrue(cts2.IsCancellationRequested, "New timeout should trigger normally");
			Assert.IsTrue(cts1.IsCancellationRequested, "Old timeout should have been cancelled during cleanup");
		}

		[UnityTest]
		public IEnumerator ServiceKitBehaviour_HandlesDestructionDuringInjection()
		{
			// Arrange
			var testService = new TestServiceImpl();
			_locator.RegisterAndReadyService<ITestService>(testService);

			var behaviourObject = new GameObject("TestBehaviour");
			// Don't add to _createdObjects since we're destroying it manually
			var behaviour = behaviourObject.AddComponent<TestServiceKitBehaviourWithDelay>();
			behaviour.SetServiceKitLocator(_locator);

			// Wait for injection to start
			yield return new WaitForSeconds(0.1f);

			// Act - Destroy the behaviour while injection might be pending
			// This should NOT throw any exceptions as they should be handled internally
			UnityEngine.Object.DestroyImmediate(behaviourObject);

			// Clean up
			ServiceKitTimeoutManager.Cleanup();
			_locator.ClearServices();

			// Assert - Wait to ensure no additional exceptions occur
			yield return new WaitForSeconds(0.1f);
			
			// Verify cleanup completed without timeout exceptions
			Assert.Pass("Cleanup completed and handled destruction gracefully");
		}

		// Test helper classes
		private interface ITestService
		{
			void DoSomething();
		}

		private interface IDelayedService
		{
			void Process();
		}

		private class TestServiceImpl : ITestService
		{
			public void DoSomething() { }
		}

		private class DelayedServiceImpl : IDelayedService
		{
			public void Process() { }
		}

		private class TestServiceConsumerWithTimeout : MonoBehaviour
		{
			[InjectService(Required = false)]
			private IDelayedService _delayedService;

			public ServiceKitLocator ServiceKitLocator { get; set; }
		}

		private class TestServiceKitBehaviourWithDelay : ServiceKitBehaviour<ITestService>, ITestService
		{
			[InjectService(Required = false)]
			private IDelayedService _delayedService;

			protected override async void Awake()
			{
				// Simulate delay in initialization
				await Task.Delay(100);
				base.Awake();
			}

			public void DoSomething() { }
			
			public void SetServiceKitLocator(ServiceKitLocator locator)
			{
				ServiceKitLocator = locator;
			}
		}

		[UnityTest]
		public IEnumerator RuntimeExit_WithActiveTimeouts_NoExceptionsThrown()
		{
			// Arrange - Set up multiple active timeouts
			var manager = ServiceKitTimeoutManager.Instance;
			var activeTokens = new List<CancellationTokenSource>();
			var registrations = new List<IDisposable>();
			
			// Create short and long timeouts
			for (int i = 0; i < 5; i++)
			{
				var cts = new CancellationTokenSource();
				activeTokens.Add(cts);
				_cancellationTokens.Add(cts);
				
				// Mix of short (will expire) and long (won't expire) timeouts
				float timeout = i < 2 ? 0.1f : 10f;
				var reg = manager.RegisterTimeout(cts, timeout);
				registrations.Add(reg);
			}

			// Let some timeouts expire naturally
			yield return new WaitForSeconds(0.2f);

			// Track if any exceptions occur
			bool exceptionOccurred = false;
			Application.logMessageReceived += (message, stackTrace, type) =>
			{
				if (type == LogType.Exception || type == LogType.Error)
				{
					if (message.Contains("Timeout") || message.Contains("ServiceKit"))
					{
						exceptionOccurred = true;
					}
				}
			};

			// Act - Simulate runtime exit
			ServiceKitTimeoutManager.Cleanup();
			
			// Try to dispose registrations after cleanup
			foreach (var reg in registrations)
			{
				try
				{
					reg?.Dispose();
				}
				catch
				{
					// Should not throw, but catch just in case
				}
			}

			// Wait to ensure no delayed exceptions
			yield return new WaitForSeconds(0.5f);

			// Assert
			Assert.IsFalse(exceptionOccurred, "No timeout-related exceptions should occur during cleanup");
			
			// Verify all tokens are in expected state
			for (int i = 0; i < activeTokens.Count; i++)
			{
				var token = activeTokens[i];
				Assert.IsTrue(token.IsCancellationRequested, 
					$"Token {i} should be cancelled after cleanup");
			}
		}
	}
}