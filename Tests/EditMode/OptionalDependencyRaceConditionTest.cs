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
	[TestFixture]
	public class OptionalDependencyRaceConditionTest
	{
		private ServiceKitLocator _serviceLocator;

		// Test service interfaces
		public interface IServiceA { string Name { get; } }
		public interface IServiceB { string Name { get; } }

		// Test service implementations
		public class ServiceA : IServiceA 
		{ 
			public string Name => "ServiceA";
		}
		
		public class ServiceB : IServiceB 
		{ 
			public string Name => "ServiceB";
		}

		// Consumer with optional dependency that should be injected if service is ready
		public class ServiceConsumer
		{
			[InjectService(Required = false)]
			public IServiceA OptionalServiceA;
			
			public bool InitializeServiceCalled { get; private set; }
			public bool DependencyWasNullDuringInit { get; private set; }
			
			public void InitializeService()
			{
				InitializeServiceCalled = true;
				DependencyWasNullDuringInit = (OptionalServiceA == null);
			}
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
		public async Task OptionalDependency_AlreadyReady_ShouldAlwaysBeInjected()
		{
			// This test verifies that when a service is already registered AND ready,
			// it should ALWAYS be injected into optional dependencies
			
			const int iterations = 100;
			int nullCount = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				// Clear services for each iteration
				_serviceLocator.ClearServices();
				
				// Arrange - Register and ready ServiceA BEFORE injection
				var serviceA = new ServiceA();
				_serviceLocator.RegisterService<IServiceA>(serviceA);
				_serviceLocator.ReadyService<IServiceA>();
				
				// Verify service is ready
				Assert.IsTrue(_serviceLocator.IsServiceReady<IServiceA>(), 
					$"Iteration {i}: ServiceA should be ready before injection");
				
				var consumer = new ServiceConsumer();
				
				// Act - Inject dependencies
				await _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
				
				// Assert - Service should be injected since it was ready
				if (consumer.OptionalServiceA == null)
				{
					nullCount++;
					Debug.LogError($"Iteration {i}: Optional dependency was null despite service being ready!");
				}
			}
			
			Assert.AreEqual(0, nullCount, 
				$"Race condition detected: {nullCount}/{iterations} iterations had null optional dependency despite service being ready");
		}

		[Test]
		public async Task OptionalDependency_BecomesReadyDuringInjection_ShouldBeInjected()
		{
			// This test simulates ServiceA becoming ready while ServiceB's injection is in progress
			
			const int iterations = 100;
			int nullCount = 0;
			int successCount = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				// Clear services for each iteration
				_serviceLocator.ClearServices();
				
				// Arrange
				var serviceA = new ServiceA();
				var consumer = new ServiceConsumer();
				
				// Register but don't ready ServiceA yet
				_serviceLocator.RegisterService<IServiceA>(serviceA);
				
				// Act - Start injection and ready service concurrently
				var injectionTask = Task.Run(async () =>
				{
					await _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
				});
				
				var readyTask = Task.Run(async () =>
				{
					// Small delay to let injection start
					await Task.Delay(1);
					_serviceLocator.ReadyService<IServiceA>();
				});
				
				await Task.WhenAll(injectionTask, readyTask);
				
				// Assert
				if (consumer.OptionalServiceA != null)
				{
					successCount++;
				}
				else
				{
					nullCount++;
				}
			}
			
			Debug.Log($"Results: {successCount} successful injections, {nullCount} null injections out of {iterations} iterations");
			
			// We expect most to succeed since the service is registered
			Assert.Greater(successCount, iterations * 0.8, 
				$"Too many failures: only {successCount}/{iterations} succeeded");
		}

		[Test]
		public async Task OptionalDependency_WithTimeout_ShouldHandleCorrectly()
		{
			// Test what happens when optional dependency times out
			
			// Arrange
			var serviceA = new ServiceA();
			var consumer = new ServiceConsumer();
			
			// Register but never ready ServiceA
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			
			// Act - Inject with short timeout
			bool timedOut = false;
			try
			{
				using (var cts = new CancellationTokenSource(100)) // 100ms timeout
				{
					await _serviceLocator.InjectServicesAsync(consumer)
						.WithCancellation(cts.Token)
						.ExecuteAsync();
				}
			}
			catch (TimeoutException)
			{
				timedOut = true;
			}
			catch (OperationCanceledException)
			{
				timedOut = true;
			}
			
			// Assert
			Assert.IsTrue(timedOut, "Should timeout waiting for registered but not ready service");
			Assert.IsNull(consumer.OptionalServiceA, "Service should not be injected after timeout");
		}

		[Test]
		public async Task ServiceKitBehaviour_Simulation_OptionalDependencyRaceCondition()
		{
			// This test simulates the ServiceKitBehaviour flow more closely
			
			const int iterations = 50;
			int initCalledWithNullDependency = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				// Clear services for each iteration
				_serviceLocator.ClearServices();
				
				// Arrange - ServiceA is registered and ready
				var serviceA = new ServiceA();
				_serviceLocator.RegisterService<IServiceA>(serviceA);
				_serviceLocator.ReadyService<IServiceA>();
				
				var consumer = new ServiceConsumer();
				
				// Act - Simulate ServiceKitBehaviour initialization sequence
				async Task SimulateServiceKitBehaviourFlow()
				{
					// Simulate InjectDependenciesAsync
					try
					{
						await _serviceLocator.InjectServicesAsync(consumer)
							.WithTimeout(30f) // Default timeout
							.ExecuteAsync();
					}
					catch (Exception ex)
					{
						// Simulate HandleDependencyInjectionFailure
						Debug.LogError($"Failed to inject required services: {ex.Message}");
						// But execution continues!
					}
					
					// Simulate InitializeService being called after injection
					consumer.InitializeService();
				}
				
				await SimulateServiceKitBehaviourFlow();
				
				// Assert
				Assert.IsTrue(consumer.InitializeServiceCalled, 
					$"Iteration {i}: InitializeService should have been called");
				
				if (consumer.DependencyWasNullDuringInit)
				{
					initCalledWithNullDependency++;
					Debug.LogError($"Iteration {i}: InitializeService was called with null dependency!");
				}
			}
			
			Assert.AreEqual(0, initCalledWithNullDependency,
				$"Race condition: InitializeService was called with null dependency in {initCalledWithNullDependency}/{iterations} iterations");
		}

		[Test] 
		public async Task OptionalDependency_CancellationDuringResolution_ShouldNotCorruptState()
		{
			// Test that cancellation during resolution doesn't cause issues
			
			// Arrange
			var serviceA = new ServiceA();
			var consumer1 = new ServiceConsumer();
			var consumer2 = new ServiceConsumer();
			
			// Register but don't ready ServiceA
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			
			// Act - Start two injections, cancel one, ready the service
			using (var cts1 = new CancellationTokenSource())
			using (var cts2 = new CancellationTokenSource())
			{
				var injection1 = _serviceLocator.InjectServicesAsync(consumer1)
					.WithCancellation(cts1.Token)
					.ExecuteAsync();
				
				var injection2 = _serviceLocator.InjectServicesAsync(consumer2)
					.WithCancellation(cts2.Token)
					.ExecuteAsync();
				
				// Cancel first injection after a short delay
				await Task.Delay(10);
				cts1.Cancel();
				
				// Ready the service
				_serviceLocator.ReadyService<IServiceA>();
				
				// Wait for second injection
				try
				{
					await injection2;
				}
				catch (OperationCanceledException)
				{
					// This is ok
				}
				
				// First injection should have been cancelled
				bool injection1Cancelled = false;
				try
				{
					await injection1;
				}
				catch (OperationCanceledException)
				{
					injection1Cancelled = true;
				}
				
				// Assert
				Assert.IsTrue(injection1Cancelled, "First injection should have been cancelled");
				Assert.IsNotNull(consumer2.OptionalServiceA, "Second consumer should have the service injected");
				Assert.IsNull(consumer1.OptionalServiceA, "First consumer should not have the service injected (was cancelled)");
			}
		}

		[Test]
		public async Task OptionalDependency_ErrorHandlerDoesNotRethrow_ExecutionContinues()
		{
			// This test verifies the suspected issue where error handlers don't stop execution
			
			// Arrange
			var consumer = new ServiceConsumer();
			bool errorHandlerCalled = false;
			bool executionContinuedAfterError = false;
			
			// Register but never ready ServiceA to cause timeout
			_serviceLocator.RegisterService<IServiceA>(new ServiceA());
			
			// Act
			try
			{
				// Simulate what ServiceKitBehaviour does
				await _serviceLocator.InjectServicesAsync(consumer)
					.WithTimeout(0.05f) // Very short timeout to trigger quickly
					.WithErrorHandling(ex =>
					{
						errorHandlerCalled = true;
						// Note: Error handler doesn't re-throw!
					})
					.ExecuteAsync();
					
				// This line should only execute if no exception was thrown
				executionContinuedAfterError = true;
			}
			catch
			{
				// If we get here, an exception was thrown despite error handler
			}
			
			// Now simulate calling InitializeService
			consumer.InitializeService();
			
			// Assert - This is the bug!
			if (errorHandlerCalled && executionContinuedAfterError)
			{
				Assert.Fail("BUG: Execution continued after injection error! " +
					"Error handler was called but didn't stop the initialization sequence.");
			}
		}
	}
}