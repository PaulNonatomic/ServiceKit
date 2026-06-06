using System.Threading.Tasks;
using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// This fixture asserts System.Threading.Tasks awaiter internals (ContinueWith / IsCompleted and the
// RunContinuationsAsynchronously lock-safety). UniTask has a different continuation model, so the
// fixture only applies to the non-UniTask build.
#if !SERVICEKIT_UNITASK
namespace Tests.EditMode
{
	/// <summary>
	/// Verifies awaiter cancellation does not run continuations synchronously while the locator
	/// holds its internal lock, and that pending awaiters are still cancelled on teardown.
	/// </summary>
	[TestFixture]
	public class AwaiterCancellationTests
	{
		public interface IFoo { }
		public class Foo : IFoo { }

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
		public void UnregisterService_DoesNotRunAwaiterContinuationsSynchronously()
		{
			// Registered but never readied so the GetServiceAsync call parks an awaiter.
			_locator.RegisterService<IFoo>(new Foo());

			var task = _locator.GetServiceAsync(typeof(IFoo));

			var ranWhileUnregistering = false;
			var unregistering = false;
			_ = task.ContinueWith(_ =>
			{
				if (unregistering) ranWhileUnregistering = true;
			}, TaskContinuationOptions.ExecuteSynchronously);

			unregistering = true;
			_locator.UnregisterService(typeof(IFoo));
			unregistering = false;

			Assert.IsFalse(ranWhileUnregistering,
				"Awaiter continuation must not run synchronously inside UnregisterService (under _lock)");
		}

		[Test]
		public async Task ClearServices_CancelsPendingAwaiters()
		{
			_locator.RegisterService<IFoo>(new Foo());

			var task = _locator.GetServiceAsync(typeof(IFoo));
			Assert.IsFalse(task.IsCompleted, "Awaiter should still be pending");

			_locator.ClearServices();

			var cancelled = false;
			try
			{
				await task;
			}
			catch (System.OperationCanceledException)
			{
				cancelled = true;
			}

			Assert.IsTrue(cancelled, "Pending awaiter should be cancelled when services are cleared");
		}
	}
}
#endif
