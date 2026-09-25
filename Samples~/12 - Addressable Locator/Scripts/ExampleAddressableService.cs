#if SERVICEKIT_ADDRESSABLES
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.AddressableLocator
{
	public interface IExampleAddressableService
	{
		void DoSomething();
	}

	/// <summary>
	/// Example service whose locator is loaded through Addressables rather than a direct serialized
	/// reference. Because it derives from <see cref="AddressableServiceKitBehaviour"/>:
	///   - assign a ServiceKitLocator <b>AssetReference</b> on this component in the inspector
	///     (and leave the inherited <c>ServiceKitLocator</c> field empty);
	///   - the locator is loaded asynchronously before this service registers, so it is not pulled
	///     into every bundle/scene that references it;
	///   - the Addressables handle is released automatically when this object is destroyed.
	/// Only compiles when the Addressables package is installed (the SERVICEKIT_ADDRESSABLES define).
	/// </summary>
	[Service(typeof(IExampleAddressableService))]
	public class ExampleAddressableService : AddressableServiceKitBehaviour, IExampleAddressableService
	{
		protected override void InitializeService()
		{
			Debug.Log("[Sample] ExampleAddressableService initialized - its locator was loaded via Addressables.");
		}

		public void DoSomething()
		{
			Debug.Log("[Sample] ExampleAddressableService.DoSomething()");
		}
	}
}
#endif
