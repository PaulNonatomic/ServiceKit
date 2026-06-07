using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Validates how injection failures are surfaced (see ServiceInjectionLog / ClassifyCancellation):
	/// a destroyed target during a teardown is silent, while an awaited service being unregistered
	/// while the target survives is a warning rather than an error.
	/// </summary>
	public class InjectionFailureReportingTests
	{
		public interface IMissingService { }

		public interface IPendingService { }
		public class PendingService : IPendingService { }

#pragma warning disable 0169 // fields are assigned via [InjectService] reflection
		private class RequiresMissingBehaviour : ServiceKitBehaviour
		{
			[InjectService] private IMissingService _missing;
		}

		private class RequiresPendingBehaviour : ServiceKitBehaviour
		{
			[InjectService] private IPendingService _pending;
		}
#pragma warning restore 0169

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

		// Creates an active consumer with the locator assigned before Awake runs.
		private static GameObject AddConsumer<T>(ServiceKitLocator locator) where T : ServiceKitBehaviour
		{
			var go = new GameObject(typeof(T).Name);
			go.SetActive(false);
			var consumer = go.AddComponent<T>();
			consumer.ServiceKitLocator = locator;
			go.SetActive(true); // triggers Awake -> dependency injection
			return go;
		}

		[UnityTest]
		public IEnumerator DestroyedTargetDuringInjection_IsSilent()
		{
			var go = AddConsumer<RequiresMissingBehaviour>(_locator);

			// Let Awake run and injection begin waiting for the (never-registered) required service.
			yield return null;
			yield return null;

			// Destroy the target well before the timeout: a teardown cancellation, not a failure.
			Object.Destroy(go);

			// Allow the cancellation to propagate through the async injection.
			for (var i = 0; i < 5; i++) yield return null;

			// A destroyed target must not log an error or warning.
			LogAssert.NoUnexpectedReceived();
		}

		[UnityTest]
		public IEnumerator ServiceUnregisteredWhileTargetAlive_LogsWarningNotError()
		{
			// A required dependency is registered but never readied, so the consumer waits on it.
			_locator.RegisterService<IPendingService>(new PendingService());

			var go = AddConsumer<RequiresPendingBehaviour>(_locator);

			// Let the consumer reach the waiting state. Generous because, single-threaded (WebGL), each
			// async hop to the await costs a frame pump.
			for (var i = 0; i < 60; i++) yield return null;

			// Unregistering the awaited service while the target is alive is a warning, not an error.
			LogAssert.Expect(LogType.Warning, new Regex("Failed to inject required services"));

			_locator.UnregisterService(typeof(IPendingService));

			// The fault propagates back up the await chain one frame-pump at a time; wait generously.
			for (var i = 0; i < 120; i++) yield return null;

			Object.Destroy(go);
			yield return null;
		}
	}
}
