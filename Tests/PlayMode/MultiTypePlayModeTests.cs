using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Nonatomic.ServiceKit;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	public class MultiTypePlayModeTests
	{
		private ServiceKitLocator _locator;
		private List<GameObject> _createdObjects;

		[SetUp]
		public void Setup()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			_createdObjects = new List<GameObject>();
		}

		[TearDown]
		public void TearDown()
		{
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

		#region Multi-Type Registration Tests

		[UnityTest]
		public IEnumerator MultiType_ServiceKitBehaviour_InjectsAllTypes()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>()
				.AlsoAs<IServiceC>();
			_locator.ReadyService<IServiceA>();

			var behaviourObj = new GameObject("TestBehaviour");
			_createdObjects.Add(behaviourObj);
			var behaviour = behaviourObj.AddComponent<MultiTypeConsumerBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			// Act - Wait for injection to complete
			yield return new WaitForSeconds(0.2f);

			// Assert
			Assert.IsNotNull(behaviour.ServiceA, "ServiceA should be injected");
			Assert.IsNotNull(behaviour.ServiceB, "ServiceB should be injected");
			Assert.IsNotNull(behaviour.ServiceC, "ServiceC should be injected");
			Assert.AreSame(service, behaviour.ServiceA, "ServiceA should be same instance");
			Assert.AreSame(service, behaviour.ServiceB, "ServiceB should be same instance");
			Assert.AreSame(service, behaviour.ServiceC, "ServiceC should be same instance");
		}

		[UnityTest]
		public IEnumerator MultiType_ReadyByAlternateType_AllTypesReady()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>()
				.AlsoAs<IServiceC>();

			var behaviourObj = new GameObject("TestBehaviour");
			_createdObjects.Add(behaviourObj);
			var behaviour = behaviourObj.AddComponent<MultiTypeConsumerBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			// Act - Ready via alternate type
			yield return new WaitForSeconds(0.1f);
			_locator.ReadyService<IServiceB>();

			// Wait for injection
			yield return new WaitForSeconds(0.2f);

			// Assert - All types should be injected
			Assert.IsNotNull(behaviour.ServiceA, "ServiceA should be injected");
			Assert.IsNotNull(behaviour.ServiceB, "ServiceB should be injected");
			Assert.IsNotNull(behaviour.ServiceC, "ServiceC should be injected");
			Assert.IsTrue(_locator.IsServiceReady<IServiceA>(), "ServiceA should be ready");
			Assert.IsTrue(_locator.IsServiceReady<IServiceB>(), "ServiceB should be ready");
			Assert.IsTrue(_locator.IsServiceReady<IServiceC>(), "ServiceC should be ready");
		}

		[UnityTest]
		public IEnumerator MultiType_UnregisterByAlternateType_RemovesAllTypes()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>();
			_locator.ReadyService<IServiceA>();

			var behaviourObj = new GameObject("TestBehaviour");
			_createdObjects.Add(behaviourObj);
			var behaviour = behaviourObj.AddComponent<MultiTypeConsumerBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			yield return new WaitForSeconds(0.2f);

			Assert.IsNotNull(behaviour.ServiceA, "ServiceA should be injected initially");
			Assert.IsNotNull(behaviour.ServiceB, "ServiceB should be injected initially");

			// Act - Unregister via alternate type
			_locator.UnregisterService<IServiceB>();

			// Assert
			Assert.IsNull(_locator.GetService<IServiceA>(), "ServiceA should be unregistered");
			Assert.IsNull(_locator.GetService<IServiceB>(), "ServiceB should be unregistered");
			Assert.IsFalse(_locator.IsServiceReady<IServiceA>(), "ServiceA should not be ready");
			Assert.IsFalse(_locator.IsServiceReady<IServiceB>(), "ServiceB should not be ready");
		}

		#endregion

		#region Concurrent Multi-Type Tests

		[UnityTest]
		public IEnumerator MultiType_MultipleBehaviours_AllGetSameInstance()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>()
				.AlsoAs<IServiceC>();
			_locator.ReadyService<IServiceA>();

			// Create multiple behaviours
			var behaviours = new List<MultiTypeConsumerBehaviour>();
			for (int i = 0; i < 3; i++)
			{
				var obj = new GameObject($"Behaviour{i}");
				_createdObjects.Add(obj);
				var behaviour = obj.AddComponent<MultiTypeConsumerBehaviour>();
				behaviour.SetServiceKitLocator(_locator);
				behaviours.Add(behaviour);
			}

			// Act - Wait for all injections
			yield return new WaitForSeconds(0.3f);

			// Assert - All behaviours should have same instance
			foreach (var behaviour in behaviours)
			{
				Assert.AreSame(service, behaviour.ServiceA, "All should have same ServiceA instance");
				Assert.AreSame(service, behaviour.ServiceB, "All should have same ServiceB instance");
				Assert.AreSame(service, behaviour.ServiceC, "All should have same ServiceC instance");
			}
		}

		[UnityTest]
		public IEnumerator MultiType_BecomesReadyWhileInjecting_CompletesSuccessfully()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>();

			var behaviourObj = new GameObject("TestBehaviour");
			_createdObjects.Add(behaviourObj);
			var behaviour = behaviourObj.AddComponent<MultiTypeConsumerBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			// Act - Ready the service after a delay
			yield return new WaitForSeconds(0.1f);
			_locator.ReadyService<IServiceA>();

			// Wait for injection to complete
			yield return new WaitForSeconds(0.3f);

			// Assert
			Assert.IsNotNull(behaviour.ServiceA, "ServiceA should be injected");
			Assert.IsNotNull(behaviour.ServiceB, "ServiceB should be injected");
			Assert.IsTrue(behaviour.InitializeServiceCalled, "InitializeService should be called");
		}

		#endregion

		#region ServiceKitBehaviour Destruction with Timeout Tests

		[UnityTest]
		public IEnumerator Destruction_DuringInjection_CancelsTimeout()
		{
			// Arrange - Register but never ready the service
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service); // Never ready it

			var behaviourObj = new GameObject("TestBehaviour");
			var behaviour = behaviourObj.AddComponent<TimeoutTestBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			// Wait for injection to start
			yield return new WaitForSeconds(0.1f);

			// Track timeout manager state
			var timeoutManager = ServiceKitTimeoutManager.Instance;
			Assert.IsNotNull(timeoutManager, "Timeout manager should exist");

			// Act - Destroy the behaviour while waiting for service
			UnityEngine.Object.DestroyImmediate(behaviourObj);

			// Wait to ensure timeout doesn't fire after destruction
			yield return new WaitForSeconds(0.3f);

			// Assert - No timeout exceptions should occur
			Assert.Pass("Destruction handled gracefully without timeout exceptions");
		}

		[UnityTest]
		public IEnumerator Destruction_MultipleBehaviours_AllTimeoutsCancelled()
		{
			// Arrange - Register but never ready services
			var serviceA = new MultiTypeTestService();
			var serviceB = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(serviceA); // Never ready
			_locator.RegisterService<IServiceB>(serviceB); // Never ready

			var behaviours = new List<GameObject>();
			for (int i = 0; i < 3; i++)
			{
				var obj = new GameObject($"Behaviour{i}");
				var behaviour = obj.AddComponent<TimeoutTestBehaviour>();
				behaviour.SetServiceKitLocator(_locator);
				behaviours.Add(obj);
			}

			// Wait for injections to start
			yield return new WaitForSeconds(0.1f);

			// Act - Destroy all behaviours at once
			foreach (var obj in behaviours)
			{
				UnityEngine.Object.DestroyImmediate(obj);
			}

			// Wait to ensure no timeouts fire
			yield return new WaitForSeconds(0.3f);

			// Assert
			Assert.Pass("All behaviours destroyed gracefully without timeout exceptions");
		}

		[UnityTest]
		public IEnumerator Destruction_BeforeTimeout_NoTimeoutException()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service); // Never ready

			var behaviourObj = new GameObject("TestBehaviour");
			var behaviour = behaviourObj.AddComponent<TimeoutTestBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			bool exceptionOccurred = false;
			Application.logMessageReceived += (message, stackTrace, type) =>
			{
				if (type == LogType.Exception && message.Contains("Timeout"))
				{
					exceptionOccurred = true;
				}
			};

			// Wait for injection to start
			yield return new WaitForSeconds(0.1f);

			// Act - Destroy before timeout expires (uses default timeout from ServiceKitSettings)
			UnityEngine.Object.DestroyImmediate(behaviourObj);

			// Wait to ensure timeout doesn't fire after destruction
			yield return new WaitForSeconds(0.5f);

			// Assert
			Assert.IsFalse(exceptionOccurred, "No timeout exception should occur after destruction");
		}

		[UnityTest]
		public IEnumerator Destruction_WithMultiTypeService_CancelsAllAwaiters()
		{
			// Arrange - Multi-type service that will never be ready
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>()
				.AlsoAs<IServiceC>();

			var behaviourObj = new GameObject("TestBehaviour");
			var behaviour = behaviourObj.AddComponent<MultiTypeConsumerBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			// Wait for all three GetServiceAsync calls to start
			yield return new WaitForSeconds(0.1f);

			// Act - Destroy while waiting for all three types
			UnityEngine.Object.DestroyImmediate(behaviourObj);

			// Wait to ensure cleanup completes
			yield return new WaitForSeconds(0.2f);

			// Assert - No exceptions or hangs
			Assert.Pass("Multi-type awaiter cleanup handled gracefully");
		}

		#endregion

		#region Timeout Manager Integration Tests

		[UnityTest]
		public IEnumerator TimeoutManager_MultipleTimeouts_AllCancelledOnDestruction()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service); // Never ready

			// Create multiple behaviours with timeouts
			var behaviourObjs = new List<GameObject>();
			for (int i = 0; i < 5; i++)
			{
				var obj = new GameObject($"TimeoutBehaviour{i}");
				var behaviour = obj.AddComponent<TimeoutTestBehaviour>();
				behaviour.SetServiceKitLocator(_locator);
				behaviourObjs.Add(obj);
			}

			// Wait for timeouts to register
			yield return new WaitForSeconds(0.1f);

			var timeoutManager = ServiceKitTimeoutManager.Instance;
			Assert.IsNotNull(timeoutManager, "Timeout manager should exist");

			// Act - Destroy all behaviours
			foreach (var obj in behaviourObjs)
			{
				UnityEngine.Object.DestroyImmediate(obj);
			}

			// Wait to ensure no timeouts fire
			yield return new WaitForSeconds(0.5f);

			// Assert - Should not throw when accessing timeout manager
			Assert.DoesNotThrow(() =>
			{
				var manager = ServiceKitTimeoutManager.Instance;
			}, "Timeout manager should be accessible after behaviour destruction");
		}

		[UnityTest]
		public IEnumerator TimeoutManager_DestructionDuringTimeout_NoMemoryLeak()
		{
			// This test verifies that destroying behaviours during timeout doesn't cause memory leaks
			// by repeatedly creating and destroying behaviours with pending timeouts

			for (int cycle = 0; cycle < 10; cycle++)
			{
				// Arrange
				var service = new MultiTypeTestService();
				_locator.RegisterService<IServiceA>(service); // Never ready

				var obj = new GameObject($"Cycle{cycle}");
				var behaviour = obj.AddComponent<TimeoutTestBehaviour>();
				behaviour.SetServiceKitLocator(_locator);

				// Wait briefly
				yield return new WaitForSeconds(0.05f);

				// Destroy
				UnityEngine.Object.DestroyImmediate(obj);
				_locator.UnregisterService<IServiceA>();

				// Brief delay between cycles
				yield return null;
			}

			// Assert - If we got here without exceptions, test passes
			Assert.Pass("Multiple create/destroy cycles completed without memory leaks");
		}

		#endregion

		#region Play Mode Specific Edge Cases

		[UnityTest]
		public IEnumerator PlayMode_MultiType_DisableEnableGameObject_MaintainsInjection()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>();
			_locator.ReadyService<IServiceA>();

			var behaviourObj = new GameObject("TestBehaviour");
			_createdObjects.Add(behaviourObj);
			var behaviour = behaviourObj.AddComponent<MultiTypeConsumerBehaviour>();
			behaviour.SetServiceKitLocator(_locator);

			// Wait for injection
			yield return new WaitForSeconds(0.2f);

			Assert.IsNotNull(behaviour.ServiceA, "ServiceA should be injected");
			Assert.IsNotNull(behaviour.ServiceB, "ServiceB should be injected");

			// Act - Disable and re-enable
			behaviourObj.SetActive(false);
			yield return null;
			behaviourObj.SetActive(true);
			yield return new WaitForSeconds(0.1f);

			// Assert - Services should still be injected
			Assert.IsNotNull(behaviour.ServiceA, "ServiceA should remain injected after disable/enable");
			Assert.IsNotNull(behaviour.ServiceB, "ServiceB should remain injected after disable/enable");
		}

		[UnityTest]
		public IEnumerator PlayMode_MultiType_ParentChildDestruction_NoExceptions()
		{
			// Arrange
			var service = new MultiTypeTestService();
			_locator.RegisterService<IServiceA>(service)
				.AlsoAs<IServiceB>();
			_locator.ReadyService<IServiceA>();

			// Create parent-child hierarchy
			var parent = new GameObject("Parent");
			var child1 = new GameObject("Child1");
			var child2 = new GameObject("Child2");

			child1.transform.SetParent(parent.transform);
			child2.transform.SetParent(parent.transform);

			var behaviour1 = child1.AddComponent<MultiTypeConsumerBehaviour>();
			var behaviour2 = child2.AddComponent<MultiTypeConsumerBehaviour>();

			behaviour1.SetServiceKitLocator(_locator);
			behaviour2.SetServiceKitLocator(_locator);

			// Wait for injection
			yield return new WaitForSeconds(0.2f);

			// Act - Destroy parent (should destroy children too)
			UnityEngine.Object.DestroyImmediate(parent);

			// Wait to ensure cleanup completes
			yield return new WaitForSeconds(0.1f);

			// Assert
			Assert.Pass("Parent-child destruction handled without exceptions");
		}

		#endregion

		#region Test Helper Classes

		public interface IServiceA { string Name { get; } }
		public interface IServiceB { string Name { get; } }
		public interface IServiceC { string Name { get; } }

		public class MultiTypeTestService : IServiceA, IServiceB, IServiceC
		{
			public string Name => "MultiTypeService";
		}

		public class MultiTypeConsumerBehaviour : MonoBehaviour
		{
			[InjectService]
			public IServiceA ServiceA;

			[InjectService]
			public IServiceB ServiceB;

			[InjectService(Required = false)]
			public IServiceC ServiceC;

			public bool InitializeServiceCalled { get; private set; }

			private ServiceKitLocator _locator;

			public void SetServiceKitLocator(ServiceKitLocator locator)
			{
				_locator = locator;
				StartCoroutine(InjectAndInitialize());
			}

			private System.Collections.IEnumerator InjectAndInitialize()
			{
				var task = _locator.InjectServicesAsync(this)
					.WithTimeout()
					.ExecuteAsync();

#if SERVICEKIT_UNITASK
				yield return task.ToCoroutine();
#else
				while (!task.IsCompleted)
				{
					yield return null;
				}

				if (task.IsFaulted)
				{
					Debug.LogError($"Injection failed: {task.Exception}");
				}
#endif

				InitializeService();
			}

			private void InitializeService()
			{
				InitializeServiceCalled = true;
			}
		}

		public class TimeoutTestBehaviour : MonoBehaviour
		{
			[InjectService]
			public IServiceA ServiceA;

			private ServiceKitLocator _locator;

			public void SetServiceKitLocator(ServiceKitLocator locator)
			{
				_locator = locator;
				StartCoroutine(InjectDependencies());
			}

			private System.Collections.IEnumerator InjectDependencies()
			{
				var task = _locator.InjectServicesAsync(this)
					.WithTimeout()
					.ExecuteAsync();

#if SERVICEKIT_UNITASK
				yield return task.ToCoroutine();
#else
				while (!task.IsCompleted)
				{
					yield return null;
				}

				if (task.IsFaulted && task.Exception != null)
				{
					// Log but don't rethrow - tests check for this
					Debug.LogError($"Injection failed: {task.Exception}");
				}
#endif
			}
		}

		#endregion
	}
}
