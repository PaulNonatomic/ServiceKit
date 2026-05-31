using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nonatomic.ServiceKit.Tests.PlayMode
{
	/// <summary>
	/// Verifies the timeout manager uses unscaled time so timeouts still fire while the
	/// game is paused (Time.timeScale == 0).
	/// </summary>
	public class TimeoutManagerUnscaledTimeTests
	{
		private float _originalTimeScale;

		[SetUp]
		public void Setup()
		{
			_originalTimeScale = Time.timeScale;
		}

		[TearDown]
		public void TearDown()
		{
			Time.timeScale = _originalTimeScale;
			ServiceKitTimeoutManager.Cleanup();
		}

		[UnityTest]
		public IEnumerator Timeout_FiresEvenWhenTimeScaleIsZero()
		{
			var manager = ServiceKitTimeoutManager.Instance;
			Assert.IsNotNull(manager, "Manager should be created");

			var cts = new CancellationTokenSource();

			// Pause the game: scaled time stops advancing entirely.
			Time.timeScale = 0f;

			manager.RegisterTimeout(cts, 0.1f);

			// Wait in real time (WaitForSeconds would never elapse at timeScale 0).
			yield return new WaitForSecondsRealtime(0.5f);

			Assert.IsTrue(cts.IsCancellationRequested,
				"Timeout should fire based on unscaled time even when Time.timeScale is 0");

			cts.Dispose();
		}
	}
}
