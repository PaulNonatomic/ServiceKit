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
	public class TOCTOURaceConditionTest
	{
		private ServiceKitLocator _serviceLocator;
		
		public interface IServiceA { }
		public class ServiceA : IServiceA { }
		
		public class ServiceB
		{
			[InjectService(Required = false)]
			public IServiceA ServiceA;
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
		public async Task TOCTOU_RaceCondition_BetweenIsReadyAndGetService()
		{
			// This test demonstrates the Time-Of-Check-Time-Of-Use race condition
			// between IsServiceReady and GetService in ResolveOptionalService
			
			const int iterations = 100;
			int raceConditionHits = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				_serviceLocator.ClearServices();
				
				// Setup
				var serviceA = new ServiceA();
				_serviceLocator.RegisterService<IServiceA>(serviceA);
				_serviceLocator.ReadyService<IServiceA>();
				
				var serviceB = new ServiceB();
				
				// Start injection
				var injectionTask = Task.Run(async () =>
				{
					try
					{
						await _serviceLocator.InjectServicesAsync(serviceB).ExecuteAsync();
					}
					catch (Exception ex)
					{
						// Ignore exceptions for this test
						Debug.Log($"Injection exception: {ex.Message}");
					}
				});
				
				// Concurrently unregister the service to hit the race condition window
				var unregisterTask = Task.Run(async () =>
				{
					// Try different timings to hit the sweet spot
					await Task.Delay(i % 5); // Vary delay based on iteration
					_serviceLocator.UnregisterService<IServiceA>();
				});
				
				await Task.WhenAll(injectionTask, unregisterTask);
				
				// Check if we hit the race condition
				// If ServiceA was ready when checked but null when retrieved
				if (serviceB.ServiceA == null)
				{
					raceConditionHits++;
					Debug.Log($"Iteration {i}: Race condition hit - ServiceA is null!");
				}
			}
			
			Debug.Log($"Race condition hit {raceConditionHits}/{iterations} times");
			
			// Even one hit proves the race condition exists
			if (raceConditionHits > 0)
			{
				Assert.Fail($"TOCTOU Race Condition Confirmed: Service was ready when checked " +
					$"but null when retrieved in {raceConditionHits}/{iterations} iterations!");
			}
		}
		
		[Test]
		public void DemonstrateNonAtomicCheck()
		{
			// This demonstrates that IsServiceReady and GetService are not atomic
			
			// Register and ready a service
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			_serviceLocator.ReadyService<IServiceA>();
			
			// Check ready
			bool isReady = _serviceLocator.IsServiceReady<IServiceA>();
			Assert.IsTrue(isReady, "Service should be ready");
			
			// Between these calls, the service could be unregistered!
			_serviceLocator.UnregisterService<IServiceA>();
			
			// Now get returns null even though IsReady returned true
			var service = _serviceLocator.GetService<IServiceA>();
			Assert.IsNull(service, "Service is null after unregistration");
			
			Assert.Pass("Demonstrated non-atomic nature of IsServiceReady + GetService");
		}
		
		[Test]
		public void ProposedFix_AtomicCheckAndGet()
		{
			// The fix should be to make the check and get atomic
			// Either by:
			// 1. Using TryGetService which does both atomically
			// 2. Getting the service first and checking if it's null
			// 3. Adding a new method that does both operations under a single lock
			
			string proposedFix = @"
			// Instead of:
			if (locator.IsServiceReady(serviceType))
			{
				var readyService = _serviceKitLocator.GetService(serviceType);
				return (field, readyService, serviceAttribute.Required);
			}
			
			// Do this (atomic operation):
			var readyService = _serviceKitLocator.GetService(serviceType);
			if (readyService != null)
			{
				return (field, readyService, serviceAttribute.Required);
			}
			
			// Or use TryGetService:
			if (locator.TryGetService(serviceType, out var service))
			{
				return (field, service, serviceAttribute.Required);
			}";
			
			Assert.Pass($"Fix the TOCTOU race condition with: {proposedFix}");
		}
		
		[Test]
		public async Task SceneUnloadRaceCondition()
		{
			// Another scenario: ServiceA is in a scene that gets unloaded
			// This could cause ServiceA to be unregistered between check and get
			
			// Register ServiceA
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			_serviceLocator.ReadyService<IServiceA>();
			
			var serviceB = new ServiceB();
			
			// Start injection
			var injectionTask = Task.Run(async () =>
			{
				await _serviceLocator.InjectServicesAsync(serviceB).ExecuteAsync();
			});
			
			// Simulate scene unload by unregistering services
			await Task.Delay(1);
			_serviceLocator.UnregisterService<IServiceA>();
			
			await injectionTask;
			
			// ServiceB might have null ServiceA due to race condition
			if (serviceB.ServiceA == null)
			{
				Assert.Pass("Scene unload can cause race condition where service " +
					"is ready when checked but gone when retrieved");
			}
		}
	}
}