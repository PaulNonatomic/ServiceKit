using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Validates the <see cref="ServiceKitBehaviour.PrepareLocatorAsync"/> hook: a behaviour that sources
	/// its locator asynchronously must defer registration until the load completes, then register and
	/// initialize normally. This is the exact mechanism <c>AddressableServiceKitBehaviour</c> relies on,
	/// tested here without the Addressables package - a <see cref="TaskCompletionSource{TResult}"/> stands
	/// in for the async load so the core contract is verified deterministically.
	/// </summary>
	public class PrepareLocatorAsyncTests
	{
		public interface IAsyncLocatorService { }

		[Service(typeof(IAsyncLocatorService))]
		private sealed class AsyncLocatorConsumer : ServiceKitBehaviour, IAsyncLocatorService
		{
			public ServiceKitLocator LocatorToLoad;
			public readonly TaskCompletionSource<bool> LoadGate = new TaskCompletionSource<bool>();
			private ServiceKitLocator _loaded;
			public bool Initialized { get; private set; }

			// Null until the async "load" completes - mirrors an addressable locator not yet resolved.
			protected override IServiceLocator ResolveLocator() => _loaded;

#if SERVICEKIT_UNITASK
			protected override async Cysharp.Threading.Tasks.UniTask PrepareLocatorAsync()
#else
			protected override async Task PrepareLocatorAsync()
#endif
			{
				// Stand-in for an async locator source (e.g. _serviceKitLocatorReference.LoadAssetAsync).
				await LoadGate.Task;
				_loaded = LocatorToLoad;
			}

			protected override void InitializeService() => Initialized = true;
		}

		[UnityTest]
		public IEnumerator PrepareLocatorAsync_DefersRegistration_UntilLocatorLoaded()
		{
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			var go = new GameObject(nameof(AsyncLocatorConsumer));
			go.SetActive(false);
			var consumer = go.AddComponent<AsyncLocatorConsumer>();
			consumer.LocatorToLoad = locator;

			go.SetActive(true); // Awake -> CacheDestroyToken -> await PrepareLocatorAsync (gated open)

			// While the locator "load" is pending, registration must NOT have happened yet.
			for (var i = 0; i < 30; i++) yield return null;
			Assert.IsFalse(locator.IsServiceRegistered<IAsyncLocatorService>(),
				"Registration must be deferred until PrepareLocatorAsync completes");

			consumer.LoadGate.SetResult(true); // complete the async locator load

			for (var i = 0; i < 600 && !consumer.Initialized; i++) yield return null;

			Assert.IsTrue(locator.IsServiceRegistered<IAsyncLocatorService>(),
				"Service must register once the locator has loaded");
			Assert.IsTrue(consumer.Initialized,
				"InitializeService must run after the async locator load completes");

			Object.Destroy(go);
			locator.ClearServices();
			Object.Destroy(locator);
		}
	}
}
