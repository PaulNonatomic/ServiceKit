using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Definitive single-threaded async-resumption tests. These exist to validate WebGL (no thread
	/// pool): does an injection that genuinely *awaits* a not-yet-ready service resume and complete
	/// when continuations can only run on the main-thread frame pump? The pre-existing tests all used
	/// the ready-immediately fast path (or thread-pool scaffolding), so this gap was untested.
	/// Waits are deliberately generous because, single-threaded, each await hop costs a frame pump.
	/// </summary>
	public class AsyncResumeTests
	{
		public interface IRequiredDep { int Value { get; } }
		private sealed class RequiredDep : IRequiredDep { public int Value => 7; }

		public interface IOptionalDep { }

#pragma warning disable 0169
		private sealed class RequiredConsumer : ServiceKitBehaviour
		{
			[InjectService] private IRequiredDep _dep;
			public IRequiredDep Dep => _dep;
			public bool Initialized { get; private set; }
			protected override void InitializeService() => Initialized = true;
		}

		private sealed class OptionalConsumer : ServiceKitBehaviour
		{
			[InjectService(Required = false)] private IOptionalDep _opt;
			public IOptionalDep Opt => _opt;
			public bool Initialized { get; private set; }
			protected override void InitializeService() => Initialized = true;
		}
#pragma warning restore 0169

		private static T AddConsumer<T>(ServiceKitLocator locator, out GameObject go) where T : ServiceKitBehaviour
		{
			go = new GameObject(typeof(T).Name);
			go.SetActive(false);
			var consumer = go.AddComponent<T>();
			consumer.ServiceKitLocator = locator;
			go.SetActive(true); // Awake -> registration + injection
			return consumer;
		}

		// A required dependency is registered-not-ready; the consumer must AWAIT it, then resume and
		// complete once it becomes ready. This is the path that only works if continuations resume on
		// the frame pump without a thread pool. Fails (or hangs -> bounded here) if WebGL can't resume.
		[UnityTest]
		public IEnumerator RequiredDep_BecomesReadyDuringWait_ResumesAndInjects()
		{
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			locator.RegisterService<IRequiredDep>(new RequiredDep()); // registered, NOT ready
			var consumer = AddConsumer<RequiredConsumer>(locator, out var go);

			// Let the consumer reach the await (still null = still waiting).
			for (var i = 0; i < 60 && consumer.Dep == null; i++) yield return null;
			Assert.IsNull(consumer.Dep, "Consumer should still be awaiting the not-ready dependency");

			locator.ReadyService<IRequiredDep>();

			// Generous: single-threaded resume + the rest of the injection chain, one hop per frame.
			for (var i = 0; i < 600 && !consumer.Initialized; i++) yield return null;

			Assert.IsNotNull(consumer.Dep, "Required dependency must inject after it becomes ready");
			Assert.AreEqual(7, consumer.Dep.Value);
			Assert.IsTrue(consumer.Initialized, "Injection must complete (InitializeService must run)");

			Object.Destroy(go);
			locator.ClearServices();
			Object.Destroy(locator);
		}

		// An optional dependency is never registered. Injection must still COMPLETE (skip it). This
		// exercises WaitForAwakePhaseCompletion (the non-UniTask Task.Delay(1) yield) - if that does
		// not resume on WebGL, injection never finishes and Initialized stays false.
		[UnityTest]
		public IEnumerator OptionalDep_NotRegistered_InjectionStillCompletes()
		{
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			var consumer = AddConsumer<OptionalConsumer>(locator, out var go);

			for (var i = 0; i < 600 && !consumer.Initialized; i++) yield return null;

			Assert.IsTrue(consumer.Initialized, "Injection must complete even when an optional dependency is absent");
			Assert.IsNull(consumer.Opt, "Unregistered optional dependency should be left null");

			Object.Destroy(go);
			locator.ClearServices();
			Object.Destroy(locator);
		}
	}
}
