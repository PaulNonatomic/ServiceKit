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
	public class ExactTimingRaceConditionTest
	{
		private ServiceKitLocator _serviceLocator;
		
		public interface IServiceA { }
		public class ServiceA : IServiceA { }
		
		public class ServiceB
		{
			[InjectService(Required = false)]
			public IServiceA ServiceA;
			
			public bool InitializeCalled;
			public bool ServiceAWasNull;
			
			public void InitializeService()
			{
				InitializeCalled = true;
				ServiceAWasNull = (ServiceA == null);
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
			}
		}
		
		[Test]
		public async Task CheckGetServiceAsyncBehaviorWithCancellation()
		{
			// Test if GetServiceAsync properly handles cancellation for optional deps
			
			// Arrange
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			// Don't ready it yet
			
			// Act - Try to get service with cancellation
			using (var cts = new CancellationTokenSource(100)) // 100ms timeout
			{
				IServiceA result = null;
				bool cancelled = false;
				
				try
				{
					result = await _serviceLocator.GetServiceAsync<IServiceA>(cts.Token);
				}
				catch (OperationCanceledException)
				{
					cancelled = true;
				}
				
				// Assert
				Assert.IsTrue(cancelled, "Should be cancelled waiting for not-ready service");
				Assert.IsNull(result, "Result should be null");
			}
		}
		
		[Test]
		public async Task WhenMultipleWaitersAndOneIsCancelled_OthersShouldStillWork()
		{
			// Test the per-caller TaskCompletionSource implementation
			
			// Arrange
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			
			var consumer1 = new ServiceB();
			var consumer2 = new ServiceB();
			
			// Act - Start two injections with different cancellation tokens
			using (var cts1 = new CancellationTokenSource())
			using (var cts2 = new CancellationTokenSource())
			{
				var task1 = _serviceLocator.InjectServicesAsync(consumer1)
					.WithCancellation(cts1.Token)
					.ExecuteAsync();
				
				var task2 = _serviceLocator.InjectServicesAsync(consumer2)
					.WithCancellation(cts2.Token)
					.ExecuteAsync();
				
				// Cancel first one
				await Task.Delay(10);
				cts1.Cancel();
				
				// Ready the service
				await Task.Delay(10);
				_serviceLocator.ReadyService<IServiceA>();
				
				// Wait for tasks
				bool task1Cancelled = false;
				bool task2Succeeded = false;
				
				try
				{
					await task1;
				}
				catch (OperationCanceledException)
				{
					task1Cancelled = true;
				}
				catch (TimeoutException)
				{
					// Also acceptable
					task1Cancelled = true;
				}
				
				try
				{
					await task2;
					task2Succeeded = true;
				}
				catch
				{
					task2Succeeded = false;
				}
				
				// Assert
				Assert.IsTrue(task1Cancelled, "First task should be cancelled");
				Assert.IsTrue(task2Succeeded, "Second task should succeed");
				Assert.IsNull(consumer1.ServiceA, "Consumer1 should not have service (cancelled)");
				Assert.IsNotNull(consumer2.ServiceA, "Consumer2 should have service injected");
			}
		}
		
		[Test]
		public async Task ExactScenario_ServiceAReadyBeforeServiceBInjection()
		{
			// The exact scenario the user described:
			// ServiceA is registered AND initialized (ready) in the scene
			// ServiceB has optional dependency on ServiceA
			// Sometimes ServiceB.InitializeService is called with null ServiceA
			
			const int iterations = 50;
			int nullCount = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				_serviceLocator.ClearServices();
				
				// ServiceA is fully ready BEFORE ServiceB starts
				var serviceA = new ServiceA();
				_serviceLocator.RegisterService<IServiceA>(serviceA);
				_serviceLocator.ReadyService<IServiceA>();
				
				// Verify it's ready
				Assert.IsTrue(_serviceLocator.IsServiceReady<IServiceA>(), 
					"ServiceA must be ready");
				
				// Now inject into ServiceB
				var serviceB = new ServiceB();
				
				// Simulate ServiceKitBehaviour flow
				try
				{
					// Use timeout like ServiceKitBehaviour does
					await _serviceLocator.InjectServicesAsync(serviceB)
						.WithTimeout(30f)
						.ExecuteAsync();
				}
				catch (Exception ex)
				{
					Debug.LogError($"Iteration {i}: Injection failed: {ex.Message}");
				}
				
				// Call InitializeService
				serviceB.InitializeService();
				
				// Check result
				if (serviceB.ServiceAWasNull)
				{
					nullCount++;
					Debug.LogError($"Iteration {i}: BUG - ServiceA was null during InitializeService!");
					
					// Double-check service state
					var isReady = _serviceLocator.IsServiceReady<IServiceA>();
					var directGet = _serviceLocator.GetService<IServiceA>();
					Debug.LogError($"  ServiceA ready: {isReady}, Direct get: {directGet != null}");
				}
			}
			
			Assert.AreEqual(0, nullCount,
				$"BUG CONFIRMED: ServiceA was null in {nullCount}/{iterations} iterations " +
				"despite being registered and ready BEFORE injection!");
		}
		
		[Test]
		public async Task CheckResolveOptionalServiceLogic()
		{
			// Test the exact logic in ResolveOptionalService
			
			// When service is ready, it should return immediately
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			_serviceLocator.ReadyService<IServiceA>();
			
			var serviceB = new ServiceB();
			
			// This should be instant since service is ready
			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			await _serviceLocator.InjectServicesAsync(serviceB).ExecuteAsync();
			stopwatch.Stop();
			
			Assert.IsNotNull(serviceB.ServiceA, "Service should be injected");
			Assert.Less(stopwatch.ElapsedMilliseconds, 50, 
				"Should be nearly instant when service is ready");
		}
	}
}