using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Tests.EditMode
{
	[TestFixture]
	public class OptionalDependencyTests
	{
		private ServiceKitLocator _serviceLocator;

		// Test service interfaces
		public interface ITestServiceA { }
		public interface ITestServiceB { }
		public interface ITestServiceC { }

		// Test service implementations
		public class TestServiceA : ITestServiceA { }
		public class TestServiceB : ITestServiceB { }
		public class TestServiceC : ITestServiceC { }

		// Consumer with multiple optional dependencies
		public class MultipleOptionalDependencyConsumer
		{
			[InjectService(Required = false)]
			public ITestServiceA OptionalServiceA;
			
			[InjectService(Required = false)]
			public ITestServiceB OptionalServiceB;
			
			[InjectService(Required = false)]
			public ITestServiceC OptionalServiceC;

			public bool InjectionCompleted { get; set; }
		}

		[SetUp]
		public void Setup()
		{
			_serviceLocator = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}

		[TearDown]
		public void TearDown()
		{
			if (_serviceLocator != null)
			{
				_serviceLocator.ClearServices();
				Object.DestroyImmediate(_serviceLocator);
				_serviceLocator = null;
			}
		}

		[Test]
		public async Task OptionalDependency_ComplexScenario_AllShouldBeInjected()
		{
			// Tests race conditions with multiple optional dependencies registered and readied at different times
			
			const int testIterations = 100;
			int fullSuccessCount = 0;
			int partialSuccessCount = 0;
			int failureCount = 0;

			for (int iteration = 0; iteration < testIterations; iteration++)
			{
				// Clear services for each iteration
				_serviceLocator.ClearServices();

				// Arrange
				var serviceA = new TestServiceA();
				var serviceB = new TestServiceB();
				var serviceC = new TestServiceC();
				var consumer = new MultipleOptionalDependencyConsumer();

				// Register all services but don't make them ready
				_serviceLocator.RegisterService<ITestServiceA>(serviceA);
				_serviceLocator.RegisterService<ITestServiceB>(serviceB);
				_serviceLocator.RegisterService<ITestServiceC>(serviceC);

				// Act - Start injection and ready services at different times
				var injectionTask = Task.Run(async () =>
				{
					var builder = _serviceLocator.InjectServicesAsync(consumer);
					await builder.ExecuteAsync();
					consumer.InjectionCompleted = true;
				});

				// Ready services at different intervals to hit different parts of the injection process
				var readyTaskA = Task.Run(async () =>
				{
					await Task.Delay(0); // Ready immediately
					_serviceLocator.ReadyService<ITestServiceA>();
				});

				var readyTaskB = Task.Run(async () =>
				{
					await Task.Delay(2); // Ready slightly delayed
					_serviceLocator.ReadyService<ITestServiceB>();
				});

				var readyTaskC = Task.Run(async () =>
				{
					await Task.Delay(5); // Ready more delayed
					_serviceLocator.ReadyService<ITestServiceC>();
				});

				// Wait for all tasks to complete
				await Task.WhenAll(injectionTask, readyTaskA, readyTaskB, readyTaskC);

				// Count successes
				int injectedCount = 0;
				if (consumer.OptionalServiceA != null) injectedCount++;
				if (consumer.OptionalServiceB != null) injectedCount++;
				if (consumer.OptionalServiceC != null) injectedCount++;

				if (injectedCount == 3)
				{
					fullSuccessCount++;
				}
				else if (injectedCount > 0)
				{
					partialSuccessCount++;
					Debug.LogWarning($"Iteration {iteration}: Only {injectedCount}/3 optional services injected. " +
						$"A={consumer.OptionalServiceA != null}, B={consumer.OptionalServiceB != null}, C={consumer.OptionalServiceC != null}");
				}
				else
				{
					failureCount++;
					Debug.LogError($"Iteration {iteration}: No optional services injected!");
				}

				Assert.IsTrue(consumer.InjectionCompleted, 
					$"Iteration {iteration}: Injection should have completed");
			}

			Debug.Log($"Test Results: {fullSuccessCount} full success, {partialSuccessCount} partial, {failureCount} failures out of {testIterations} iterations");
			
			// All services were registered, so they should all be injected
			Assert.AreEqual(testIterations, fullSuccessCount, 
				$"Race condition detected: {partialSuccessCount + failureCount} iterations didn't inject all optional services");
		}

		// Single field consumer for precise testing
		public class SingleOptionalDependencyConsumer
		{
			[InjectService(Required = false)]
			public ITestServiceB OptionalServiceB;
		}
		
		[Test]
		public async Task OptionalDependency_RegisteredButNeverReady_ShouldWaitAndTimeout()
		{
			// Verifies optional dependencies that are registered but not ready cause timeout
			
			// Arrange
			var serviceB = new TestServiceB();
			var consumer = new SingleOptionalDependencyConsumer();
			
			// Register service B but never make it ready
			_serviceLocator.RegisterService<ITestServiceB>(serviceB);
			
			Debug.Log($"ServiceB registered: {_serviceLocator.IsServiceRegistered<ITestServiceB>()}");
			Debug.Log($"ServiceB ready: {_serviceLocator.IsServiceReady<ITestServiceB>()}");

			// Act
			var injectionCompleted = false;
			var injectionTask = Task.Run(async () =>
			{
				var builder = _serviceLocator.InjectServicesAsync(consumer);
				
				// Use a cancellation token with timeout since service will never be ready
				using (var cts = new CancellationTokenSource(1000)) // 1 second timeout
				{
					try
					{
						await builder.WithCancellation(cts.Token).ExecuteAsync();
						injectionCompleted = true;
					}
					catch (TimeoutException)
					{
						// Expected - injection should timeout waiting for registered service
					}
					catch (OperationCanceledException)
					{
						// Also acceptable - some versions might throw this
					}
				}
			});

			await injectionTask;

			// Assert - Injection should NOT complete because it waits for registered services
			Assert.IsFalse(injectionCompleted, 
				"Injection should wait for registered optional dependencies and timeout");
			Assert.IsNull(consumer.OptionalServiceB, 
				"Service should not be injected since it never became ready");
		}

		[Test]
		public async Task OptionalDependency_NotRegistered_ShouldCompleteImmediately()
		{
			// Verifies unregistered optional dependencies complete immediately with null
			
			// Arrange
			var consumer = new MultipleOptionalDependencyConsumer();
			// Don't register any services

			// Act
			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			var builder = _serviceLocator.InjectServicesAsync(consumer);
			await builder.ExecuteAsync();
			consumer.InjectionCompleted = true; // Mark as completed after ExecuteAsync returns
			stopwatch.Stop();

			// Assert - Injection should complete quickly since no services are registered
			Assert.IsTrue(consumer.InjectionCompleted, "Injection should complete");
			Assert.IsNull(consumer.OptionalServiceA, "ServiceA should be null");
			Assert.IsNull(consumer.OptionalServiceB, "ServiceB should be null");
			Assert.IsNull(consumer.OptionalServiceC, "ServiceC should be null");
			Assert.Less(stopwatch.ElapsedMilliseconds, 100, 
				"Injection should complete quickly when optional services aren't registered");
		}

		[Test]
		public async Task OptionalDependency_MixedScenario_ShouldHandleCorrectly()
		{
			// Tests mix of registered, ready, and non-existent services
			
			// Arrange
			var serviceA = new TestServiceA();
			var serviceB = new TestServiceB();
			var consumer = new MultipleOptionalDependencyConsumer();
			
			// ServiceA: Register and ready immediately
			_serviceLocator.RegisterService<ITestServiceA>(serviceA);
			_serviceLocator.ReadyService<ITestServiceA>();
			
			// ServiceB: Register but never ready (will cause wait)
			_serviceLocator.RegisterService<ITestServiceB>(serviceB);
			
			// ServiceC: Not registered at all

			// Act
			var injectionTask = Task.Run(async () =>
			{
				var builder = _serviceLocator.InjectServicesAsync(consumer);
				
				// Use timeout since ServiceB will never be ready
				using (var cts = new CancellationTokenSource(500))
				{
					try
					{
						await builder.WithCancellation(cts.Token).ExecuteAsync();
						consumer.InjectionCompleted = true;
					}
					catch (TimeoutException)
					{
						// Expected - injection should timeout waiting for ServiceB
					}
					catch (OperationCanceledException)
					{
						// Also acceptable - some versions might throw this
					}
				}
			});

			await injectionTask;

			// Assert
			Assert.IsFalse(consumer.InjectionCompleted, 
				"Injection should NOT complete (timeout waiting for ServiceB)");
			Assert.IsNull(consumer.OptionalServiceA, 
				"ServiceA should be null (injection didn't complete)");
			Assert.IsNull(consumer.OptionalServiceB, 
				"ServiceB should be null (registered but never ready, timed out)");
			Assert.IsNull(consumer.OptionalServiceC, 
				"ServiceC should be null (never registered)");
		}
	}
}