using System.Reflection;
using Nonatomic.ServiceKit;
using NUnit.Framework;

namespace Tests.EditMode
{
	/// <summary>
	/// Verifies that losing and regaining editor focus does not permanently disable the
	/// timeout manager (previously the focus/pause handlers latched the shutdown flags).
	/// </summary>
	[TestFixture]
	public class TimeoutManagerFocusTests
	{
		[SetUp]
		public void Setup()
		{
			// Reset any latched static state from earlier tests.
			ServiceKitTimeoutManager.Cleanup();
		}

		[TearDown]
		public void TearDown()
		{
			ServiceKitTimeoutManager.Cleanup();
		}

		[Test]
		public void EditorFocusLossThenRegain_DoesNotPermanentlyDisableManager()
		{
			var manager = ServiceKitTimeoutManager.Instance;
			Assert.IsNotNull(manager, "Manager should be created");

			var onFocus = typeof(ServiceKitTimeoutManager)
				.GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(onFocus, "OnApplicationFocus should exist");

			// Simulate the editor losing focus (edit mode), then regaining it.
			onFocus.Invoke(manager, new object[] { false });
			onFocus.Invoke(manager, new object[] { true });

			Assert.IsNotNull(ServiceKitTimeoutManager.Instance,
				"After regaining editor focus the timeout manager must be usable again");
		}
	}
}
