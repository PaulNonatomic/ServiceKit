using System;
using System.Collections;
using System.Collections.Generic;
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
	public class AwakeOrderRaceConditionTest
	{
		private ServiceKitLocator _serviceLocator;
		private GameObject _testGameObject;
		
		// Test service interfaces
		public interface IServiceA { string Name { get; } }
		public interface IServiceB { string Name { get; } }
		public interface IServiceC { string Name { get; } }
		
		// Mock ServiceKitBehaviour that simulates the real behavior
		public class MockServiceBehaviour<T> : MonoBehaviour where T : class
		{
			public ServiceKitLocator ServiceLocator { get; set; }
			public bool IsRegistered { get; protected set; }
			public bool IsReady { get; protected set; }
			public bool InjectionCompleted { get; protected set; }
			public bool InitializeServiceCalled { get; protected set; }
			public int AwakeOrder { get; protected set; }
			public static int AwakeCounter = 0;
			
			// Track what happened during injection
			public bool OptionalDependencyWasNull { get; protected set; }
			public string InjectionResult { get; protected set; }
			
			protected virtual async void Awake()
			{
				AwakeOrder = ++AwakeCounter;
				
				// Simulate RegisterServiceWithLocator
				if (ServiceLocator != null && this is T service)
				{
					ServiceLocator.RegisterService<T>(service);
					IsRegistered = true;
					Debug.Log($"[{GetType().Name}] Registered at Awake order {AwakeOrder}");
				}
				
				// Simulate PerformServiceInitializationSequence
				await SimulateServiceInitialization();
			}
			
			private async Task SimulateServiceInitialization()
			{
				// Simulate InjectDependenciesAsync
				await InjectDependencies();
				
				// Simulate InitializeService
				InitializeService();
				
				// Simulate MarkServiceAsReady
				if (ServiceLocator != null && IsRegistered)
				{
					ServiceLocator.ReadyService<T>();
					IsReady = true;
					Debug.Log($"[{GetType().Name}] Ready at Awake order {AwakeOrder}");
				}
			}
			
			protected virtual async Task InjectDependencies()
			{
				// Default implementation - override in derived classes
				await Task.CompletedTask;
				InjectionCompleted = true;
			}
			
			protected virtual void InitializeService()
			{
				InitializeServiceCalled = true;
			}
		}
		
		// ServiceA with optional dependency on ServiceB
		public class ServiceAWithOptionalB : MockServiceBehaviour<IServiceA>, IServiceA
		{
			[InjectService(Required = false)]
			public IServiceB ServiceB;
			
			public string Name => "ServiceA";
			
			protected override async Task InjectDependencies()
			{
				if (ServiceLocator == null) return;
				
				Debug.Log($"[ServiceA] Starting injection at Awake order {AwakeOrder}");
				
				// Simulate what ServiceInjectionBuilder does for optional dependencies
				var locator = ServiceLocator;
				
				// Check if ServiceB is ready (atomic TryGetService)
				if (locator.TryGetService<IServiceB>(out var readyService))
				{
					ServiceB = readyService;
					InjectionResult = "ServiceB was ready, injected immediately";
					Debug.Log($"[ServiceA] {InjectionResult}");
				}
				else if (!locator.IsServiceRegistered<IServiceB>())
				{
					// ServiceB is not registered - treat as truly optional
					ServiceB = null;
					OptionalDependencyWasNull = true;
					InjectionResult = "ServiceB not registered, treating as optional (null)";
					Debug.Log($"[ServiceA] {InjectionResult}");
				}
				else
				{
					// ServiceB is registered but not ready - wait for it
					InjectionResult = "ServiceB registered but not ready, waiting...";
					Debug.Log($"[ServiceA] {InjectionResult}");
					
					try
					{
						using (var cts = new CancellationTokenSource(1000)) // 1 second timeout
						{
							ServiceB = await locator.GetServiceAsync<IServiceB>(cts.Token);
							InjectionResult = "ServiceB became ready and was injected";
							Debug.Log($"[ServiceA] {InjectionResult}");
						}
					}
					catch (OperationCanceledException)
					{
						ServiceB = null;
						OptionalDependencyWasNull = true;
						InjectionResult = "Timed out waiting for ServiceB";
						Debug.Log($"[ServiceA] {InjectionResult}");
					}
				}
				
				InjectionCompleted = true;
			}
			
			protected override void InitializeService()
			{
				base.InitializeService();
				
				if (ServiceB == null)
				{
					Debug.LogError($"[ServiceA] BUG: InitializeService called with null ServiceB!");
				}
				else
				{
					Debug.Log($"[ServiceA] InitializeService called with ServiceB = {ServiceB.Name}");
				}
			}
		}
		
		// Simple ServiceB
		public class ServiceBSimple : MockServiceBehaviour<IServiceB>, IServiceB
		{
			public string Name => "ServiceB";
		}
		
		[SetUp]
		public void Setup()
		{
			_serviceLocator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_testGameObject = new GameObject("TestObject");
			MockServiceBehaviour<IServiceA>.AwakeCounter = 0;
		}
		
		[TearDown]
		public void TearDown()
		{
			if (_serviceLocator != null)
			{
				_serviceLocator.ClearServices();
				Object.DestroyImmediate(_serviceLocator);
			}
			
			if (_testGameObject != null)
			{
				Object.DestroyImmediate(_testGameObject);
			}
		}
		
		[Test]
		public async Task WhenServiceAAwakeFirst_OptionalServiceBIsNull()
		{
			// This test simulates ServiceA's Awake being called before ServiceB's Awake
			// Result: ServiceB is not registered when ServiceA checks, so it gets null
			
			// Create ServiceA first (simulating it getting Awake first)
			var serviceA = _testGameObject.AddComponent<ServiceAWithOptionalB>();
			serviceA.ServiceLocator = _serviceLocator;
			
			// Wait a frame to let ServiceA's Awake complete
			await Task.Delay(10);
			
			// Now create ServiceB (simulating it getting Awake second)
			var serviceB = _testGameObject.AddComponent<ServiceBSimple>();
			serviceB.ServiceLocator = _serviceLocator;
			
			// Wait for both to complete
			await Task.Delay(100);
			
			// Assert
			Assert.IsTrue(serviceA.IsRegistered, "ServiceA should be registered");
			Assert.IsTrue(serviceA.IsReady, "ServiceA should be ready");
			Assert.IsTrue(serviceA.InitializeServiceCalled, "ServiceA InitializeService should be called");
			
			Assert.IsTrue(serviceB.IsRegistered, "ServiceB should be registered");
			Assert.IsTrue(serviceB.IsReady, "ServiceB should be ready");
			
			// THE BUG: ServiceA has null ServiceB despite ServiceB being available!
			Assert.IsTrue(serviceA.OptionalDependencyWasNull, 
				"BUG CONFIRMED: ServiceA got null for ServiceB because ServiceB wasn't registered yet when checked!");
			Assert.IsNull(serviceA.ServiceB, 
				"ServiceA.ServiceB is null even though ServiceB exists in the scene");
			
			Debug.Log($"ServiceA Awake Order: {serviceA.AwakeOrder}, ServiceB Awake Order: {serviceB.AwakeOrder}");
			Debug.Log($"ServiceA injection result: {serviceA.InjectionResult}");
		}
		
		[Test]
		public async Task WhenServiceBAwakeFirst_ServiceAGetsInjected()
		{
			// This test simulates ServiceB's Awake being called before ServiceA's Awake
			// Result: ServiceB is registered when ServiceA checks, so it waits and gets it
			
			// Create ServiceB first (simulating it getting Awake first)
			var serviceB = _testGameObject.AddComponent<ServiceBSimple>();
			serviceB.ServiceLocator = _serviceLocator;
			
			// Wait a frame to let ServiceB's Awake complete registration
			await Task.Delay(10);
			
			// Now create ServiceA (simulating it getting Awake second)
			var serviceA = _testGameObject.AddComponent<ServiceAWithOptionalB>();
			serviceA.ServiceLocator = _serviceLocator;
			
			// Wait for both to complete
			await Task.Delay(100);
			
			// Assert
			Assert.IsTrue(serviceA.IsRegistered, "ServiceA should be registered");
			Assert.IsTrue(serviceA.IsReady, "ServiceA should be ready");
			Assert.IsTrue(serviceA.InitializeServiceCalled, "ServiceA InitializeService should be called");
			
			Assert.IsTrue(serviceB.IsRegistered, "ServiceB should be registered");
			Assert.IsTrue(serviceB.IsReady, "ServiceB should be ready");
			
			// When ServiceB registers first, ServiceA successfully gets it
			Assert.IsFalse(serviceA.OptionalDependencyWasNull, 
				"ServiceA should have gotten ServiceB since it was registered");
			Assert.IsNotNull(serviceA.ServiceB, 
				"ServiceA.ServiceB should not be null when ServiceB registered first");
			Assert.AreEqual("ServiceB", serviceA.ServiceB.Name);
			
			Debug.Log($"ServiceA Awake Order: {serviceA.AwakeOrder}, ServiceB Awake Order: {serviceB.AwakeOrder}");
			Debug.Log($"ServiceA injection result: {serviceA.InjectionResult}");
		}
		
		[Test]
		public async Task SimulateRealWorldScenario_MultipleServices()
		{
			// This simulates a real-world scenario with multiple services
			// where the Awake order is non-deterministic
			
			const int iterations = 10;
			int nullDependencyCount = 0;
			
			for (int i = 0; i < iterations; i++)
			{
				// Reset for each iteration
				Setup();
				MockServiceBehaviour<IServiceA>.AwakeCounter = 0;
				
				// Randomly decide order
				bool serviceAFirst = (i % 2) == 0;
				
				ServiceAWithOptionalB serviceA = null;
				ServiceBSimple serviceB = null;
				
				if (serviceAFirst)
				{
					// ServiceA gets Awake first
					serviceA = _testGameObject.AddComponent<ServiceAWithOptionalB>();
					serviceA.ServiceLocator = _serviceLocator;
					await Task.Delay(5); // Small delay
					
					serviceB = _testGameObject.AddComponent<ServiceBSimple>();
					serviceB.ServiceLocator = _serviceLocator;
				}
				else
				{
					// ServiceB gets Awake first
					serviceB = _testGameObject.AddComponent<ServiceBSimple>();
					serviceB.ServiceLocator = _serviceLocator;
					await Task.Delay(5); // Small delay
					
					serviceA = _testGameObject.AddComponent<ServiceAWithOptionalB>();
					serviceA.ServiceLocator = _serviceLocator;
				}
				
				// Wait for initialization
				await Task.Delay(100);
				
				// Check result
				if (serviceA.OptionalDependencyWasNull)
				{
					nullDependencyCount++;
					Debug.Log($"Iteration {i}: ServiceA has null ServiceB (Awake order: A={serviceA.AwakeOrder}, B={serviceB.AwakeOrder})");
				}
				else
				{
					Debug.Log($"Iteration {i}: ServiceA has ServiceB injected (Awake order: A={serviceA.AwakeOrder}, B={serviceB.AwakeOrder})");
				}
				
				// Cleanup for next iteration
				TearDown();
			}
			
			Debug.Log($"Results: {nullDependencyCount}/{iterations} had null dependencies");
			Assert.Greater(nullDependencyCount, 0, 
				"Race condition confirmed: Some iterations had null optional dependencies due to Awake order");
		}
		
		[Test]
		public async Task ProposedFix_FrameDelayForUnregisteredOptionalDependencies()
		{
			// This test demonstrates how a frame delay could solve the issue
			
			// Create ServiceA first
			var serviceA = _testGameObject.AddComponent<ServiceAWithOptionalBWithDelay>();
			serviceA.ServiceLocator = _serviceLocator;
			
			// Wait a tiny bit
			await Task.Delay(5);
			
			// Create ServiceB (after ServiceA started but before frame delay)
			var serviceB = _testGameObject.AddComponent<ServiceBSimple>();
			serviceB.ServiceLocator = _serviceLocator;
			
			// Wait for completion
			await Task.Delay(200);
			
			// With frame delay, ServiceA should successfully get ServiceB
			Assert.IsNotNull(serviceA.ServiceB, 
				"With frame delay, ServiceA should get ServiceB even when Awake is called first");
			Assert.AreEqual("ServiceB", serviceA.ServiceB.Name);
			
			Debug.Log($"With frame delay: {serviceA.InjectionResult}");
		}
		
		// ServiceA variant that waits a frame when optional dependency is not registered
		public class ServiceAWithOptionalBWithDelay : ServiceAWithOptionalB
		{
			protected override async Task InjectDependencies()
			{
				if (ServiceLocator == null) return;
				
				Debug.Log($"[ServiceA with delay] Starting injection at Awake order {AwakeOrder}");
				
				var locator = ServiceLocator;
				
				// Check if ServiceB is ready
				if (locator.TryGetService<IServiceB>(out var readyService))
				{
					ServiceB = readyService;
					InjectionResult = "ServiceB was ready, injected immediately";
					Debug.Log($"[ServiceA with delay] {InjectionResult}");
				}
				else if (!locator.IsServiceRegistered<IServiceB>())
				{
					// PROPOSED FIX: Wait one frame for other services to register
					InjectionResult = "ServiceB not registered, waiting one frame...";
					Debug.Log($"[ServiceA with delay] {InjectionResult}");
					
					await Task.Yield(); // Wait one frame
					
					// Check again after frame delay
					if (locator.IsServiceRegistered<IServiceB>())
					{
						InjectionResult = "ServiceB registered after frame delay, waiting for it to be ready...";
						Debug.Log($"[ServiceA with delay] {InjectionResult}");
						
						try
						{
							using (var cts = new CancellationTokenSource(1000))
							{
								ServiceB = await locator.GetServiceAsync<IServiceB>(cts.Token);
								InjectionResult = "ServiceB injected successfully after frame delay";
								Debug.Log($"[ServiceA with delay] {InjectionResult}");
							}
						}
						catch (OperationCanceledException)
						{
							ServiceB = null;
							OptionalDependencyWasNull = true;
							InjectionResult = "Timed out waiting for ServiceB after frame delay";
						}
					}
					else
					{
						// Still not registered after frame delay - truly optional
						ServiceB = null;
						OptionalDependencyWasNull = true;
						InjectionResult = "ServiceB still not registered after frame delay, treating as truly optional";
						Debug.Log($"[ServiceA with delay] {InjectionResult}");
					}
				}
				else
				{
					// ServiceB is registered but not ready - wait for it
					InjectionResult = "ServiceB registered but not ready, waiting...";
					Debug.Log($"[ServiceA with delay] {InjectionResult}");
					
					try
					{
						using (var cts = new CancellationTokenSource(1000))
						{
							ServiceB = await locator.GetServiceAsync<IServiceB>(cts.Token);
							InjectionResult = "ServiceB became ready and was injected";
							Debug.Log($"[ServiceA with delay] {InjectionResult}");
						}
					}
					catch (OperationCanceledException)
					{
						ServiceB = null;
						OptionalDependencyWasNull = true;
						InjectionResult = "Timed out waiting for ServiceB";
					}
				}
				
				InjectionCompleted = true;
			}
		}
	}
}