using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Validates scene cleanup in an actual player build. When run on a standalone player
	/// UNITY_EDITOR is undefined, so ServiceInfo.DebugData is compiled out and cleanup must
	/// rely on the runtime SceneHandle / IsDontDestroyOnLoad metadata. These tests would have
	/// behaved differently before the runtime-scene-metadata fix.
	/// </summary>
	public class SceneCleanupPlayerTests
	{
		public interface IPlainService { }
		public class PlainService : IPlainService { }

		public interface ISceneService { }
		public class SceneMonoService : MonoBehaviour, ISceneService { }

		private ServiceKitLocator _locator;

		[SetUp]
		public void Setup()
		{
			_locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
		}

		[TearDown]
		public void TearDown()
		{
			if (_locator == null) return;
			_locator.ClearServices();
			Object.Destroy(_locator);
			_locator = null;
		}

		[Test]
		public void RuntimeSceneHandle_IsPopulated_InPlayer()
		{
			var go = new GameObject("SceneSvc");
			try
			{
				var mb = go.AddComponent<SceneMonoService>();
				_locator.RegisterAndReadyService<ISceneService>(mb);

				var info = _locator.GetAllServices().First(s => s.ServiceType == typeof(ISceneService));
				Assert.IsTrue(info.SceneHandle == go.scene.handle && info.SceneHandle != -1,
					$"Runtime scene handle must be populated in a player build (got {info.SceneHandle})");
			}
			finally
			{
				Object.Destroy(go);
			}
		}

		[Test]
		public void UnregisterServicesFromScene_RemovesSceneServices_InPlayer()
		{
			_locator.RegisterAndReadyService<IPlainService>(new PlainService());

			var go = new GameObject("SceneSvc");
			try
			{
				var mb = go.AddComponent<SceneMonoService>();
				_locator.RegisterAndReadyService<ISceneService>(mb);

				Assert.IsTrue(_locator.IsServiceRegistered<ISceneService>());

				_locator.UnregisterServicesFromScene(go.scene);

				Assert.IsFalse(_locator.IsServiceRegistered<ISceneService>(),
					"MonoBehaviour service in the unloaded scene should be removed in a player build");
				Assert.IsTrue(_locator.IsServiceRegistered<IPlainService>(),
					"Non-scene service should survive scene unload in a player build");
			}
			finally
			{
				Object.Destroy(go);
			}
		}
	}
}
