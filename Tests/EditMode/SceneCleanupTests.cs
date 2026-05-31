using System.Linq;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
	/// <summary>
	/// Verifies scene-based cleanup relies on runtime scene metadata (available in player builds),
	/// not editor-only debug data.
	/// </summary>
	[TestFixture]
	public class SceneCleanupTests
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
			Object.DestroyImmediate(_locator);
			_locator = null;
		}

		[Test]
		public void RuntimeSceneHandle_IsPopulatedForMonoBehaviourServices()
		{
			var go = new GameObject("SceneService");
			try
			{
				var mb = go.AddComponent<SceneMonoService>();
				_locator.RegisterAndReadyService<ISceneService>(mb);

				var info = _locator.GetAllServices().First(s => s.ServiceType == typeof(ISceneService));
				Assert.IsTrue(info.SceneHandle == go.scene.handle && info.SceneHandle != -1,
					$"Runtime scene handle must be populated for player-build scene cleanup " +
					$"(expected {go.scene.handle}, got {info.SceneHandle})");
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void UnregisterServicesFromScene_RemovesSceneServices_KeepsNonSceneServices()
		{
			// Non-MonoBehaviour service has no scene and should survive a scene unload.
			_locator.RegisterAndReadyService<IPlainService>(new PlainService());

			var go = new GameObject("SceneService");
			try
			{
				var mb = go.AddComponent<SceneMonoService>();
				_locator.RegisterAndReadyService<ISceneService>(mb);

				Assert.IsTrue(_locator.IsServiceRegistered<ISceneService>());
				Assert.IsTrue(_locator.IsServiceRegistered<IPlainService>());

				_locator.UnregisterServicesFromScene(go.scene);

				Assert.IsFalse(_locator.IsServiceRegistered<ISceneService>(),
					"MonoBehaviour service in the unloaded scene should be removed");
				Assert.IsTrue(_locator.IsServiceRegistered<IPlainService>(),
					"Non-scene service should be retained across scene unload");
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}
	}
}
