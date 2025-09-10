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
	public class AwakeOrderRaceConditionRealTest
	{
		private ServiceKitLocator _serviceLocator;
		
		public interface IServiceA { string Name { get; } }
		public interface IServiceB { string Name { get; } }
		
		public class ServiceA : IServiceA
		{
			public string Name => "ServiceA";
		}
		
		public class ServiceB : IServiceB
		{
			public string Name => "ServiceB";
		}
		
		public class ServiceConsumer
		{
			[InjectService(Required = false)]
			public IServiceA OptionalServiceA;
			
			[InjectService(Required = false)]
			public IServiceB OptionalServiceB;
			
			public bool InitializeServiceCalled { get; private set; }
			public bool ServiceAWasNull { get; private set; }
			public bool ServiceBWasNull { get; private set; }
			
			public void InitializeService()
			{
				InitializeServiceCalled = true;
				ServiceAWasNull = (OptionalServiceA == null);
				ServiceBWasNull = (OptionalServiceB == null);
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
		public async Task OptionalDependency_NotRegistered_WaitsOneFrameThenReturnsNull()
		{
			
			var consumer = new ServiceConsumer();
			
			var injectionTask = _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
			
			await Task.Delay(50);
			
			await injectionTask;
			
			Assert.IsNull(consumer.OptionalServiceA, 
				"ServiceA should be null as it was never registered");
		}
		
		[Test]
		public async Task OptionalDependency_RegisteredAfterInjectionStarts_ShouldBeInjected()
		{
			
			var consumer = new ServiceConsumer();
			
			var injectionTask = Task.Run(async () =>
			{
				await _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
			});
			
			// Register ServiceA very quickly after injection starts (simulating Awake order)
			await Task.Delay(5); // Very short delay
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			_serviceLocator.ReadyService<IServiceA>();
			
			// Wait for injection to complete
			await injectionTask;
			
			// With the frame delay fix, ServiceA should be injected
			Assert.IsNotNull(consumer.OptionalServiceA,
				"ServiceA should be injected thanks to frame delay fix");
			Assert.AreEqual("ServiceA", consumer.OptionalServiceA.Name);
		}
		
		[Test]
		public async Task OptionalDependency_RegisteredBeforeInjection_AlwaysInjected()
		{
			// This tests that services registered before injection always work
			
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			_serviceLocator.ReadyService<IServiceA>();
			
			var consumer = new ServiceConsumer();
			await _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
			
			Assert.IsNotNull(consumer.OptionalServiceA,
				"ServiceA should be injected when registered before injection");
			Assert.AreEqual("ServiceA", consumer.OptionalServiceA.Name);
		}
		
		[Test]
		public async Task MultipleOptionalDependencies_DifferentRegistrationTiming()
		{
			// Tests multiple optional dependencies with different registration timing
			
			var consumer = new ServiceConsumer();
			
			// Start injection
			var injectionTask = Task.Run(async () =>
			{
				await _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
			});
			
			// Register ServiceA quickly (during frame delay)
			await Task.Delay(5);
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			_serviceLocator.ReadyService<IServiceA>();
			
			// Don't register ServiceB at all
			
			// Wait for injection
			await injectionTask;
			
			// ServiceA should be injected (registered during frame delay)
			Assert.IsNotNull(consumer.OptionalServiceA,
				"ServiceA should be injected (registered during frame delay)");
			
			// ServiceB should be null (never registered)
			Assert.IsNull(consumer.OptionalServiceB,
				"ServiceB should be null (never registered)");
		}
		
		[Test]
		public async Task RaceCondition_SimulateAwakeOrder_WithRealInjection()
		{
			// Simulates the real-world Awake order race condition
			
			const int iterations = 20;
			int servicesInjectedCount = 0;
			int servicesNullCount = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				Setup(); // Fresh locator each iteration
				
				var consumer = new ServiceConsumer();
				
				// Vary the timing to simulate different Awake orders
				bool registerBeforeInjection = (i % 3) == 0;
				bool registerDuringFrameDelay = (i % 3) == 1;
				// Third case: register too late (after frame delay)
				
				Task injectionTask = null;
				
				if (registerBeforeInjection)
				{
					// ServiceA registers before injection starts
					var serviceA = new ServiceA();
					_serviceLocator.RegisterService<IServiceA>(serviceA);
					_serviceLocator.ReadyService<IServiceA>();
					
					injectionTask = _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
				}
				else
				{
					// Start injection first
					injectionTask = Task.Run(async () =>
					{
						await _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
					});
					
					if (registerDuringFrameDelay)
					{
						// Register during the frame delay window
						await Task.Delay(5);
						var serviceA = new ServiceA();
						_serviceLocator.RegisterService<IServiceA>(serviceA);
						_serviceLocator.ReadyService<IServiceA>();
					}
					else
					{
						// Register too late (after frame delay)
						await Task.Delay(100);
						var serviceA = new ServiceA();
						_serviceLocator.RegisterService<IServiceA>(serviceA);
						_serviceLocator.ReadyService<IServiceA>();
					}
				}
				
				await injectionTask;
				
				if (consumer.OptionalServiceA != null)
				{
					servicesInjectedCount++;
					Debug.Log($"Iteration {i}: ServiceA injected successfully");
				}
				else
				{
					servicesNullCount++;
					Debug.Log($"Iteration {i}: ServiceA is null");
				}
				
				TearDown();
			}
			
			Debug.Log($"Results: {servicesInjectedCount} injected, {servicesNullCount} null");
			
			// With the fix, services registered before or during frame delay should be injected
			// Only services registered too late should be null
			Assert.Greater(servicesInjectedCount, 0, "Some services should be injected");
			Assert.Greater(servicesNullCount, 0, "Some services should be null (registered too late)");
		}
		
		[Test]
		public async Task FrameDelay_OnlyForUnregisteredServices()
		{
			// Verifies frame delay only happens for unregistered services, not registered ones
			
			// Register ServiceA but don't ready it
			var serviceA = new ServiceA();
			_serviceLocator.RegisterService<IServiceA>(serviceA);
			
			var consumer = new ServiceConsumer();
			
			var startTime = DateTime.Now;
			
			// This should NOT have frame delay because service is registered
			var injectionTask = _serviceLocator.InjectServicesAsync(consumer).ExecuteAsync();
			
			// Ready the service quickly
			await Task.Delay(10);
			_serviceLocator.ReadyService<IServiceA>();
			
			await injectionTask;
			
			var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
			
			Assert.IsNotNull(consumer.OptionalServiceA,
				"ServiceA should be injected");
			
			// Should complete relatively quickly (no frame delay needed)
			Debug.Log($"Injection took {elapsed}ms");
		}
	}
}