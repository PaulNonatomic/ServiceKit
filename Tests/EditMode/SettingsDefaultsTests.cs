using Nonatomic.ServiceKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
	/// <summary>
	/// Verifies sensible out-of-the-box ServiceKitSettings defaults.
	/// </summary>
	[TestFixture]
	public class SettingsDefaultsTests
	{
		[Test]
		public void DebugLogging_DefaultsToFalse()
		{
			var settings = ScriptableObject.CreateInstance<ServiceKitSettings>();
			try
			{
				Assert.IsFalse(settings.DebugLogging,
					"DebugLogging should default to false so a fresh install is not noisy");
			}
			finally
			{
				Object.DestroyImmediate(settings);
			}
		}
	}
}
