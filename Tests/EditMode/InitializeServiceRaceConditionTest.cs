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
	public class InitializeServiceRaceConditionTest
	{
		private ServiceKitLocator _serviceLocator;
		
		public interface IServiceA { }
		public class ServiceA : IServiceA { }
		
		// This simulates a ServiceKitBehaviour-like class
		public class ServiceBWithOptionalDependency
		{
			[InjectService(Required = false)]
			public IServiceA ServiceA;
			
			public bool InjectionAttempted { get; private set; }
			public bool InitializeServiceCalled { get; private set; }
			public bool ServiceAWasNullDuringInit { get; private set; }
			public Exception InjectionException { get; private set; }
			
			public async Task SimulateServiceKitBehaviourFlow(ServiceKitLocator locator)
			{
				// Simulate PerformServiceInitializationSequence
				try
				{
					// Step 1: InjectDependenciesAsync
					await InjectDependenciesAsync(locator);
					
					// Step 2: InitializeServiceAsync (empty in most cases)
					await Task.CompletedTask;
					
					// Step 3: InitializeService
					InitializeService();
					
					// Step 4: MarkServiceAsReady
					// (not relevant for this test)
				}
				catch (Exception ex)
				{
					// In real ServiceKitBehaviour, this goes to async void Awake
					throw;
				}
			}
			
			public async Task InjectDependenciesAsync(ServiceKitLocator locator)
			{
				InjectionAttempted = true;
				
				// This mimics what ServiceKitBehaviour does
				try
				{
					// Use CancellationTokenSource for reliable timeout in tests
					using (var cts = new CancellationTokenSource(500)) // 500ms timeout
					{
						await locator.InjectServicesAsync(this)
							.WithCancellation(cts.Token)
							.WithErrorHandling(HandleDependencyInjectionFailure)
							.ExecuteAsync();
					}
				}
				catch (Exception ex)
				{
					InjectionException = ex;
					throw; // Re-throw to stop initialization
				}
			}
			
			private void HandleDependencyInjectionFailure(Exception exception)
			{
				// This mimics ServiceKitBehaviour's error handler
				Debug.LogError($"Failed to inject required services: {exception.Message}");
				// Note: This doesn't re-throw!
			}
			
			public void InitializeService()
			{
				InitializeServiceCalled = true;
				ServiceAWasNullDuringInit = (ServiceA == null);
				
				if (ServiceAWasNullDuringInit)
				{
					Debug.LogError("BUG: InitializeService called but ServiceA is null!");
				}
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
		public async Task WhenServiceAIsReadyBeforeInjection_ShouldAlwaysBeInjected()
		{
			// ServiceA is fully ready before ServiceB starts injection
			
			// Arrange
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			_serviceLocator.ReadyService<IServiceA>();
			
			var serviceB = new ServiceBWithOptionalDependency();
			
			// Act
			await serviceB.SimulateServiceKitBehaviourFlow(_serviceLocator);
			
			// Assert
			Assert.IsTrue(serviceB.InjectionAttempted, "Injection should have been attempted");
			Assert.IsNull(serviceB.InjectionException, "No exception should occur");
			Assert.IsTrue(serviceB.InitializeServiceCalled, "InitializeService should be called");
			Assert.IsNotNull(serviceB.ServiceA, "ServiceA should be injected");
			Assert.IsFalse(serviceB.ServiceAWasNullDuringInit, 
				"ServiceA should NOT be null during InitializeService");
		}
		
		[Test]
		public async Task WhenServiceAIsRegisteredButNotReady_ShouldTimeout()
		{
			// ServiceA is registered but never becomes ready
			
			// Arrange
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			// NOT calling ReadyService!
			
			var serviceB = new ServiceBWithOptionalDependency();
			
			// Act
			bool exceptionThrown = false;
			try
			{
				await serviceB.SimulateServiceKitBehaviourFlow(_serviceLocator);
			}
			catch (TimeoutException)
			{
				exceptionThrown = true;
			}
			catch (OperationCanceledException)
			{
				// CancellationTokenSource timeout throws OperationCanceledException
				exceptionThrown = true;
			}
			
			// Assert
			Assert.IsTrue(serviceB.InjectionAttempted, "Injection should have been attempted");
			Assert.IsNotNull(serviceB.InjectionException, "Exception should occur");
			Assert.IsTrue(exceptionThrown, "TimeoutException or OperationCanceledException should be thrown");
			Assert.IsFalse(serviceB.InitializeServiceCalled, 
				"InitializeService should NOT be called after timeout");
			Assert.IsNull(serviceB.ServiceA, "ServiceA should not be injected");
		}
		
		[Test]
		public async Task RaceCondition_ServiceABecomesReadyDuringInjection()
		{
			// This simulates the race condition where ServiceA becomes ready
			// while ServiceB is attempting injection
			
			const int iterations = 20;
			int successCount = 0;
			int failureCount = 0;
			int initWithNullCount = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				// Reset for each iteration
				_serviceLocator.ClearServices();
				
				// Arrange
				var serviceA = new ServiceA();
				_serviceLocator.RegisterService<IServiceA>(serviceA);
				
				var serviceB = new ServiceBWithOptionalDependency();
				
				// Act - Start injection and ready service concurrently
				var injectionTask = serviceB.SimulateServiceKitBehaviourFlow(_serviceLocator);
				
				var readyTask = Task.Run(async () =>
				{
					// Vary the delay to hit different timing windows
					await Task.Delay(i % 10);
					_serviceLocator.ReadyService<IServiceA>();
				});
				
				// Wait for both
				bool injectionSucceeded = false;
				try
				{
					await Task.WhenAll(injectionTask, readyTask);
					injectionSucceeded = true;
				}
				catch (TimeoutException)
				{
					// This can happen if ready comes too late
				}
				catch (OperationCanceledException)
				{
					// Also can happen with CancellationTokenSource timeout
				}
				
				// Analyze results
				if (injectionSucceeded)
				{
					successCount++;
					if (serviceB.ServiceAWasNullDuringInit)
					{
						initWithNullCount++;
						Debug.LogError($"Iteration {i}: BUG DETECTED - InitializeService called with null ServiceA!");
					}
				}
				else
				{
					failureCount++;
				}
			}
			
			Debug.Log($"Results: {successCount} succeeded, {failureCount} timed out, " +
				$"{initWithNullCount} had null during init");
			
			// Assert
			Assert.AreEqual(0, initWithNullCount,
				$"RACE CONDITION DETECTED: InitializeService was called with null dependency " +
				$"in {initWithNullCount}/{iterations} iterations!");
		}
		
		[Test]
		public async Task PotentialFix_CheckServiceStateBeforeInitialize()
		{
			// This tests a potential fix: checking if services were actually injected
			// before calling InitializeService
			
			// Arrange
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			// Don't ready it to cause timeout
			
			var serviceB = new ServiceBWithOptionalDependency();
			
			// Act - Modified flow that checks injection success
			bool shouldCallInitialize = true;
			try
			{
				await serviceB.InjectDependenciesAsync(_serviceLocator);
			}
			catch (TimeoutException)
			{
				// If injection failed, don't call InitializeService
				shouldCallInitialize = false;
				Debug.Log("Injection failed with TimeoutException, skipping InitializeService");
			}
			catch (OperationCanceledException)
			{
				// Also handle OperationCanceledException which is thrown when CancellationToken times out
				shouldCallInitialize = false;
				Debug.Log("Injection failed with OperationCanceledException, skipping InitializeService");
			}
			
			if (shouldCallInitialize)
			{
				serviceB.InitializeService();
			}
			
			// Assert
			Assert.IsFalse(serviceB.InitializeServiceCalled,
				"InitializeService should not be called when injection fails");
		}
	}
}