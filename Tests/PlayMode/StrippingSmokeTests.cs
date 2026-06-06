using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// The managed-stripping canary. A plain-C# service is registered ONLY under an interface and
	/// injected via [InjectService]. In the editor (Mono) this always passes; its real purpose is to
	/// run on an IL2CPP player built with High managed stripping - there it fails unless the service
	/// type and interface survive stripping (see the package link.xml plus user-side [Preserve]).
	/// CI builds a stripped player and runs this; locally it is a normal regression test.
	/// </summary>
	public class StrippingSmokeTests
	{
		public interface IStripCanaryService
		{
			int Value { get; }
		}

		private sealed class StripCanaryService : IStripCanaryService
		{
			public int Value => 42;
		}

		private sealed class StripConsumer : ServiceKitBehaviour
		{
			[InjectService] private IStripCanaryService _service;
			public IStripCanaryService Service => _service;
		}

		[UnityTest]
		public IEnumerator InterfaceRegisteredService_IsInjected()
		{
			var locator = ScriptableObject.CreateInstance<ServiceKitLocator>();
			locator.RegisterAndReadyService<IStripCanaryService>(new StripCanaryService());

			var go = new GameObject(nameof(StripConsumer));
			go.SetActive(false);
			var consumer = go.AddComponent<StripConsumer>();
			consumer.ServiceKitLocator = locator;
			go.SetActive(true); // Awake -> registration + injection

			// Wait (bounded) for the async injection to complete.
			for (var i = 0; i < 120 && consumer.Service == null; i++)
			{
				yield return null;
			}

			Assert.IsNotNull(consumer.Service,
				"An interface-registered service must inject - if this fails on an IL2CPP build it was stripped.");
			Assert.AreEqual(42, consumer.Service.Value);

			Object.Destroy(go);
			locator.ClearServices();
			Object.Destroy(locator);
		}
	}
}
