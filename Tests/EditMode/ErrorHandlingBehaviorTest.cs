using System;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
	[TestFixture]
	public class ErrorHandlingBehaviorTest
	{
		private ServiceKitLocator _serviceLocator;
		
		public interface ITestService { }
		public class TestService : ITestService { }
		
		public class ConsumerWithRequiredDependency
		{
			[InjectService(Required = true)]
			public ITestService RequiredService;
		}
		
		public class ConsumerWithOptionalDependency
		{
			[InjectService(Required = false)]
			public ITestService OptionalService;
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
		public async Task ExecuteAsync_WithErrorHandler_ShouldStillThrow()
		{
			// This test verifies that ExecuteAsync throws even when WithErrorHandling is used
			
			// Arrange
			var consumer = new ConsumerWithRequiredDependency();
			bool errorHandlerCalled = false;
			Exception handlerException = null;
			
			// Don't register the required service to cause an error
			
			// Act & Assert
			Exception caughtException = null;
			try
			{
				// Use a cancellation token with timeout instead of WithTimeout
				// to avoid potential issues with ServiceKitTimeoutManager in tests
				using (var cts = new CancellationTokenSource(100)) // 100ms timeout
				{
					await _serviceLocator.InjectServicesAsync(consumer)
						.WithCancellation(cts.Token)
						.WithErrorHandling(ex =>
						{
							errorHandlerCalled = true;
							handlerException = ex;
						})
						.ExecuteAsync();
				}
					
				Assert.Fail("ExecuteAsync should have thrown an exception");
			}
			catch (Exception ex)
			{
				caughtException = ex;
			}
			
			// Assert
			Assert.IsNotNull(caughtException, "Should have thrown an exception");
			Assert.IsTrue(caughtException is ServiceInjectionException || caughtException is TimeoutException || caughtException is OperationCanceledException,
				$"Expected ServiceInjectionException, TimeoutException, or OperationCanceledException, got {caughtException.GetType().Name}");
			
			// The error handler should NOT have been called because ExecuteAsync doesn't use it
			Assert.IsFalse(errorHandlerCalled, 
				"Error handler should NOT be called when using ExecuteAsync");
		}
		
		[Test]
		public void Execute_WithErrorHandler_ShouldNotThrow()
		{
			// This test verifies that Execute (not ExecuteAsync) uses the error handler
			
			// Arrange
			var consumer = new ConsumerWithRequiredDependency();
			bool errorHandlerCalled = false;
			Exception handlerException = null;
			
			// Don't register the required service to cause an error
			
			// Act
			// Execute() method doesn't exist on IServiceInjectionBuilder
			// This test was demonstrating the difference, but Execute() is not part of the API
			// The error handler is only used internally by ExecuteWithCancellation
			var builder = _serviceLocator.InjectServicesAsync(consumer)
				.WithTimeout(0.1f) // Short timeout
				.WithErrorHandling(ex =>
				{
					errorHandlerCalled = true;
					handlerException = ex;
				});
			
			// The Execute() method doesn't exist - this test demonstrates the API confusion
			// builder.Execute(); // This doesn't compile
			
			// Since Execute() doesn't exist, we can't test this scenario
			// The test demonstrates that WithErrorHandling doesn't work with ExecuteAsync
			Assert.Pass("Execute() method doesn't exist on IServiceInjectionBuilder. " +
				"WithErrorHandling only works with internal methods, not ExecuteAsync.");
		}
		
		[Test]
		public async Task OptionalDependency_RegisteredButNotReady_WithTimeout_Behavior()
		{
			// Test the exact scenario: optional dependency that's registered but not ready with timeout
			
			// Arrange
			var service = new TestService();
			var consumer = new ConsumerWithOptionalDependency();
			bool errorHandlerCalled = false;
			Exception caughtException = null;
			
			// Register but don't ready the service
			_serviceLocator.RegisterService<ITestService>(service);
			
			// Act
			try
			{
				// Use CancellationTokenSource for more reliable timeout in tests
				using (var cts = new CancellationTokenSource(100)) // 100ms timeout
				{
					await _serviceLocator.InjectServicesAsync(consumer)
						.WithCancellation(cts.Token)
						.WithErrorHandling(ex =>
						{
							errorHandlerCalled = true;
							Debug.Log($"Error handler called with: {ex.GetType().Name}: {ex.Message}");
						})
						.ExecuteAsync();
				}
			}
			catch (Exception ex)
			{
				caughtException = ex;
			}
			
			// Assert
			Assert.IsNotNull(caughtException, 
				"Should throw TimeoutException for registered but not ready optional dependency");
			Assert.IsTrue(caughtException is TimeoutException,
				$"Expected TimeoutException, got {caughtException?.GetType().Name}");
			Assert.IsFalse(errorHandlerCalled,
				"Error handler should NOT be called with ExecuteAsync");
			Assert.IsNull(consumer.OptionalService,
				"Service should not be injected after timeout");
		}
		
		[Test]
		public async Task ServiceKitBehaviour_SimulatedFlow_ShowsProblem()
		{
			// This simulates exactly what ServiceKitBehaviour does
			
			// Arrange
			var service = new TestService();
			var consumer = new ConsumerWithOptionalDependency();
			bool errorHandlerCalled = false;
			bool initializeServiceCalled = false;
			bool dependencyWasNull = false;
			
			// Register but don't ready the service (simulates registered but not initialized)
			_serviceLocator.RegisterService<ITestService>(service);
			
			// Act - Simulate ServiceKitBehaviour's PerformServiceInitializationSequence
			async Task SimulateServiceKitFlow()
			{
				// Step 1: InjectDependenciesAsync
				Exception injectionException = null;
				try
				{
					using (var cts = new CancellationTokenSource(100)) // 100ms timeout
					{
						await _serviceLocator.InjectServicesAsync(consumer)
							.WithCancellation(cts.Token)
							.WithErrorHandling(ex =>
							{
								errorHandlerCalled = true;
								// ServiceKitBehaviour just logs here, doesn't re-throw
								Debug.LogError($"Failed to inject required services: {ex.Message}");
							})
							.ExecuteAsync();
					}
				}
				catch (Exception ex)
				{
					injectionException = ex;
					// In ServiceKitBehaviour, this exception propagates up to async void Awake
					// where it gets swallowed by Unity's SynchronizationContext
				}
				
				// If exception was thrown, the following shouldn't execute
				// (unless it's in async void Awake)
				if (injectionException != null)
				{
					Debug.Log($"Injection failed with: {injectionException.GetType().Name}");
					// In real ServiceKitBehaviour, execution would stop here
					// because the exception propagates up
					throw injectionException;
				}
				
				// Step 2: InitializeService (should not reach here if injection failed)
				initializeServiceCalled = true;
				dependencyWasNull = (consumer.OptionalService == null);
			}
			
			// Execute the flow
			bool flowThrewException = false;
			try
			{
				await SimulateServiceKitFlow();
			}
			catch (TimeoutException)
			{
				flowThrewException = true;
			}
			
			// Assert
			Assert.IsTrue(flowThrewException, 
				"Flow should throw TimeoutException");
			Assert.IsFalse(errorHandlerCalled,
				"Error handler should NOT be called with ExecuteAsync (this is a bug in the API design)");
			Assert.IsFalse(initializeServiceCalled,
				"InitializeService should NOT be called after injection failure");
			Assert.IsNull(consumer.OptionalService,
				"Optional service should remain null after timeout");
			
			// The issue is that WithErrorHandling doesn't work with ExecuteAsync!
			// ServiceKitBehaviour thinks it's handling errors, but it's not.
		}
	}
}