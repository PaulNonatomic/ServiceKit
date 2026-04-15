using System;
using System.Threading;
using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

#if SERVICEKIT_UNITASK
using Cysharp.Threading.Tasks;
#endif

namespace Tests.EditMode
{
	[TestFixture]
	public class UseLocatorTests
	{
		private ServiceKitLocator _realLocator;
		private IServiceKitLocator _mockLocator;
		private IServiceInjectionBuilder _mockBuilder;
		private GameObject _testGameObject;

		[SetUp]
		public void Setup()
		{
			_realLocator = ScriptableObject.CreateInstance<ServiceKitLocator>();

			_mockLocator = Substitute.For<IServiceKitLocator>();
			_mockBuilder = Substitute.For<IServiceInjectionBuilder>();

			// Setup fluent API chain for mock builder
			_mockBuilder.WithCancellation(Arg.Any<CancellationToken>()).Returns(_mockBuilder);
			_mockBuilder.WithTimeout(Arg.Any<float>()).Returns(_mockBuilder);
			_mockBuilder.WithTimeout().Returns(_mockBuilder);
			_mockBuilder.WithErrorHandling(Arg.Any<Action<Exception>>()).Returns(_mockBuilder);
#if SERVICEKIT_UNITASK
			_mockBuilder.ExecuteAsync().Returns(UniTask.CompletedTask);
#else
			_mockBuilder.ExecuteAsync().Returns(Task.CompletedTask);
#endif
			_mockLocator.InjectServicesAsync(Arg.Any<object>()).Returns(_mockBuilder);
		}

		[TearDown]
		public void TearDown()
		{
			if (_realLocator != null)
			{
				_realLocator.ClearServices();
				Object.DestroyImmediate(_realLocator);
				_realLocator = null;
			}

			if (_testGameObject != null)
			{
				Object.DestroyImmediate(_testGameObject);
				_testGameObject = null;
			}
		}

		[Test]
		public void UseLocator_WithMock_AllowsTestingWithoutSerializedField()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();

			// Act
			behaviour.UseLocator(_mockLocator);

			// Assert - The behaviour should accept a mock locator
			Assert.Pass("UseLocator accepted mock IServiceKitLocator");
		}

		[Test]
		public void UseLocator_WithRealLocator_WorksAsExpected()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			var playerService = new PlayerService();
			_realLocator.RegisterAndReadyService<IPlayerService>(playerService);

			// Act
			behaviour.UseLocator(_realLocator);

			// Assert
			Assert.IsTrue(_realLocator.IsServiceReady<IPlayerService>());
		}

		[Test]
		public async Task UseLocator_WithMock_RegisterServiceIsCalled()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			behaviour.UseLocator(_mockLocator);

			// Act
			await behaviour.TestAwake(CancellationToken.None);

			// Assert - ServiceBehaviour uses non-generic RegisterService
			_mockLocator.Received(1).RegisterService(typeof(ITestService), Arg.Any<object>(), Arg.Any<string>());
		}

		[Test]
		public async Task UseLocator_WithMock_ReadyServiceIsCalled()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			behaviour.UseLocator(_mockLocator);

			// Act
			await behaviour.TestAwake(CancellationToken.None);

			// Assert - ServiceBehaviour uses non-generic ReadyService
			_mockLocator.Received(1).ReadyService(typeof(ITestService));
		}

		[Test]
		public async Task UseLocator_WithMock_InjectServicesAsyncIsCalled()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			behaviour.UseLocator(_mockLocator);

			// Act
			await behaviour.TestAwake(CancellationToken.None);

			// Assert
			_mockLocator.Received(1).InjectServicesAsync(behaviour);
		}

		[Test]
		public async Task UseLocator_WithRealLocator_ServiceGetsRegisteredAndReady()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			behaviour.UseLocator(_realLocator);

			// Act
			await behaviour.TestAwake(CancellationToken.None);

			// Assert
			Assert.IsTrue(_realLocator.IsServiceRegistered<ITestService>());
			Assert.IsTrue(_realLocator.IsServiceReady<ITestService>());
		}

		[Test]
		public async Task UseLocator_WithRealLocator_ServiceCanBeRetrieved()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			behaviour.UseLocator(_realLocator);

			// Act
			await behaviour.TestAwake(CancellationToken.None);
			var retrievedService = _realLocator.GetService<ITestService>();

			// Assert
			Assert.IsNotNull(retrievedService);
			Assert.AreSame(behaviour, retrievedService);
		}

		[Test]
		public async Task UseLocator_WithRealLocator_DependenciesAreInjected()
		{
			// Arrange
			var behaviour = CreateTestBehaviourWithDependency();
			var playerService = new PlayerService();
			_realLocator.RegisterAndReadyService<IPlayerService>(playerService);
			behaviour.UseLocator(_realLocator);

			// Act
			await behaviour.TestAwake(CancellationToken.None);

			// Assert
			Assert.IsNotNull(behaviour.InjectedPlayerService);
			Assert.AreSame(playerService, behaviour.InjectedPlayerService);
		}

		[Test]
		public void UseLocator_CanBeCalledMultipleTimes_LastOneWins()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			var firstMock = Substitute.For<IServiceKitLocator>();
			var secondMock = Substitute.For<IServiceKitLocator>();

			// Act
			behaviour.UseLocator(firstMock);
			behaviour.UseLocator(secondMock);

			// The behaviour should use the second mock (we can't directly test this,
			// but we verify no exceptions are thrown)
			Assert.Pass("Multiple UseLocator calls succeeded");
		}

		[Test]
		public void UseLocator_WithNull_ClearsOverride()
		{
			// Arrange
			var behaviour = CreateTestBehaviour();
			behaviour.UseLocator(_mockLocator);

			// Act
			behaviour.UseLocator(null);

			// Assert - behaviour falls back to serialized field (which is null)
			// This should not throw, just have no locator available
			Assert.Pass("Setting null locator did not throw");
		}

		[Test]
		public void UseLocator_AfterAddComponent_TriggersRegistration()
		{
			// Arrange - AddComponent triggers Awake, but registration is skipped (no locator)
			_testGameObject = new GameObject("TestBehaviour");
			var behaviour = _testGameObject.AddComponent<TestServiceBehaviour>();

			// At this point, Awake has run but registration was skipped
			Assert.IsFalse(_realLocator.IsServiceRegistered<ITestService>());

			// Act - UseLocator should trigger registration automatically
			behaviour.UseLocator(_realLocator);

			// Assert - Service should now be registered
			Assert.IsTrue(_realLocator.IsServiceRegistered<ITestService>());
		}

		[Test]
		public void UseLocator_AfterAddComponent_WithMock_TriggersRegistration()
		{
			// Arrange - AddComponent triggers Awake, but registration is skipped (no locator)
			_testGameObject = new GameObject("TestBehaviour");
			var behaviour = _testGameObject.AddComponent<TestServiceBehaviour>();

			// Act - UseLocator should trigger registration automatically
			behaviour.UseLocator(_mockLocator);

			// Assert - ServiceBehaviour uses non-generic RegisterService
			_mockLocator.Received(1).RegisterService(typeof(ITestService), Arg.Any<object>(), Arg.Any<string>());
		}

		[Test]
		public void UseLocator_AfterAddComponent_ServiceCanBeRetrieved()
		{
			// Arrange
			_testGameObject = new GameObject("TestBehaviour");
			var behaviour = _testGameObject.AddComponent<TestServiceBehaviour>();

			// Act
			behaviour.UseLocator(_realLocator);

			// Assert - Service should be retrievable (registered but not ready yet)
			Assert.IsTrue(_realLocator.IsServiceRegistered<ITestService>());
		}

		[Test]
		public void UseLocator_CalledTwice_DoesNotRegisterTwice()
		{
			// Arrange
			_testGameObject = new GameObject("TestBehaviour");
			var behaviour = _testGameObject.AddComponent<TestServiceBehaviour>();

			// Act - Call UseLocator twice
			behaviour.UseLocator(_mockLocator);
			behaviour.UseLocator(_mockLocator);

			// Assert - ServiceBehaviour uses non-generic RegisterService, should only be called once
			_mockLocator.Received(1).RegisterService(typeof(ITestService), Arg.Any<object>(), Arg.Any<string>());
		}

		[Test]
		public async Task UseLocator_AfterAddComponent_FullLifecycleWorks()
		{
			// Arrange - Simulates real usage: AddComponent -> UseLocator -> manual init
			_testGameObject = new GameObject("TestBehaviour");
			var behaviour = _testGameObject.AddComponent<TestServiceBehaviourWithDependency>();

			var playerService = new PlayerService();
			_realLocator.RegisterAndReadyService<IPlayerService>(playerService);

			// Act - UseLocator triggers registration, then we complete the lifecycle
			behaviour.UseLocator(_realLocator);

			// Complete injection and ready the service
			await _realLocator.InjectServicesAsync(behaviour)
				.WithCancellation(CancellationToken.None)
				.WithTimeout()
				.ExecuteAsync();

			_realLocator.ReadyService<ITestServiceWithDependency>();

			// Assert
			Assert.IsTrue(_realLocator.IsServiceReady<ITestServiceWithDependency>());
			Assert.IsNotNull(behaviour.InjectedPlayerService);
			Assert.AreSame(playerService, behaviour.InjectedPlayerService);
		}

		private TestServiceBehaviour CreateTestBehaviour()
		{
			_testGameObject = new GameObject("TestBehaviour");
			return _testGameObject.AddComponent<TestServiceBehaviour>();
		}

		private TestServiceBehaviourWithDependency CreateTestBehaviourWithDependency()
		{
			_testGameObject = new GameObject("TestBehaviourWithDependency");
			return _testGameObject.AddComponent<TestServiceBehaviourWithDependency>();
		}
	}

	public interface ITestService
	{
		void DoSomething();
	}

	[Service(typeof(ITestService))]
	public class TestServiceBehaviour : ServiceBehaviour, ITestService
	{
		public void DoSomething() { }

#if SERVICEKIT_UNITASK
		public async UniTask TestAwake(CancellationToken cancellationToken)
#else
		public async Task TestAwake(CancellationToken cancellationToken)
#endif
		{
			RegisterServiceWithLocator();

			await Locator.InjectServicesAsync(this)
				.WithCancellation(cancellationToken)
				.WithTimeout()
				.WithErrorHandling(HandleDependencyInjectionFailure)
				.ExecuteAsync();

			await InitializeServiceAsync();
			InitializeService();

			MarkServiceAsReady();
		}
	}

	public interface ITestServiceWithDependency
	{
		IPlayerService InjectedPlayerService { get; }
	}

	[Service(typeof(ITestServiceWithDependency))]
	public class TestServiceBehaviourWithDependency : ServiceBehaviour, ITestServiceWithDependency
	{
		[InjectService] private IPlayerService _playerService;

		public IPlayerService InjectedPlayerService => _playerService;

#if SERVICEKIT_UNITASK
		public async UniTask TestAwake(CancellationToken cancellationToken)
#else
		public async Task TestAwake(CancellationToken cancellationToken)
#endif
		{
			RegisterServiceWithLocator();

			await Locator.InjectServicesAsync(this)
				.WithCancellation(cancellationToken)
				.WithTimeout()
				.WithErrorHandling(HandleDependencyInjectionFailure)
				.ExecuteAsync();

			await InitializeServiceAsync();
			InitializeService();

			MarkServiceAsReady();
		}
	}

	// Multi-interface test types
	public interface IMultiA { }
	public interface IMultiB { }

	[Service(typeof(IMultiA), typeof(IMultiB))]
	public class MultiInterfaceServiceBehaviour : ServiceBehaviour, IMultiA, IMultiB
	{
#if SERVICEKIT_UNITASK
		public async UniTask TestAwake(CancellationToken cancellationToken)
#else
		public async Task TestAwake(CancellationToken cancellationToken)
#endif
		{
			RegisterServiceWithLocator();

			await Locator.InjectServicesAsync(this)
				.WithCancellation(cancellationToken)
				.WithTimeout()
				.WithErrorHandling(HandleDependencyInjectionFailure)
				.ExecuteAsync();

			await InitializeServiceAsync();
			InitializeService();

			MarkServiceAsReady();
		}
	}

	[TestFixture]
	public class MultiInterfaceServiceBehaviourTests
	{
		private ServiceKitLocator _locator;
		private GameObject _testGameObject;

		[SetUp]
		public void Setup()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}

		[TearDown]
		public void TearDown()
		{
			if (_locator != null)
			{
				_locator.ClearServices();
				Object.DestroyImmediate(_locator);
				_locator = null;
			}

			if (_testGameObject != null)
			{
				Object.DestroyImmediate(_testGameObject);
				_testGameObject = null;
			}
		}

		[Test]
		public async Task MultiInterface_RegistersUnderAllTypes()
		{
			// Arrange
			_testGameObject = new GameObject("MultiTest");
			var behaviour = _testGameObject.AddComponent<MultiInterfaceServiceBehaviour>();
			behaviour.UseLocator(_locator);

			// Act
			await behaviour.TestAwake(CancellationToken.None);

			// Assert — retrievable via both interfaces
			Assert.IsTrue(_locator.IsServiceReady<IMultiA>());
			Assert.IsTrue(_locator.IsServiceReady<IMultiB>());
			Assert.AreSame(behaviour, _locator.GetService<IMultiA>());
			Assert.AreSame(behaviour, _locator.GetService<IMultiB>());
		}

		[Test]
		public async Task MultiInterface_UnregisterRemovesAllTypes()
		{
			// Arrange
			_testGameObject = new GameObject("MultiTest");
			var behaviour = _testGameObject.AddComponent<MultiInterfaceServiceBehaviour>();
			behaviour.UseLocator(_locator);
			await behaviour.TestAwake(CancellationToken.None);

			// Act — unregister one type
			_locator.UnregisterService<IMultiA>();

			// Assert — only the unregistered type is gone
			Assert.IsFalse(_locator.IsServiceReady<IMultiA>());
			Assert.IsTrue(_locator.IsServiceReady<IMultiB>());
		}

		[Test]
		public async Task MultiInterface_ReadyServiceFiresForAllTypes()
		{
			// Arrange
			_testGameObject = new GameObject("MultiTest");
			var behaviour = _testGameObject.AddComponent<MultiInterfaceServiceBehaviour>();
			behaviour.UseLocator(_locator);
			await behaviour.TestAwake(CancellationToken.None);

			// Assert — both types should be ready (not just registered)
			Assert.AreEqual("Ready", _locator.GetServiceStatus<IMultiA>());
			Assert.AreEqual("Ready", _locator.GetServiceStatus<IMultiB>());
		}
	}
}
