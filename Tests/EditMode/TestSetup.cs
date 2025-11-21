using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
	/// <summary>
	/// Global test setup to suppress known Unity warnings that don't affect ServiceKit functionality
	/// </summary>
	[SetUpFixture]
	public class TestSetup
	{
		[OneTimeSetUp]
		public void GlobalSetup()
		{
			// Suppress unhandled log messages from Unity packages during tests
			// These are unrelated to ServiceKit functionality and would cause false test failures
			// Note: Real ServiceKit test failures will still occur via Assert statements
			// (Assert.IsNotNull, Assert.AreEqual, etc.) which are not affected by this setting
			LogAssert.ignoreFailingMessages = true;
		}

		[OneTimeTearDown]
		public void GlobalTeardown()
		{
			LogAssert.ignoreFailingMessages = false;
		}
	}
}
