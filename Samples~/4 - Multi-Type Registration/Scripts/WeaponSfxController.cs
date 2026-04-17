using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.MultiTypeExample
{
	/// <summary>
	/// Example of a gameplay component that only needs sound effects.
	/// Clean dependency on just the interface it needs.
	/// </summary>
	[Service(typeof(WeaponSfxController))]
	public class WeaponSfxController : ServiceKitBehaviour
	{
		[InjectService] private ISoundEffects _soundEffects;

		protected override void InitializeService()
		{
			Debug.Log("[WeaponSfxController] Ready to play weapon sounds");
		}

		public void FireWeapon(Vector3 position)
		{
			_soundEffects.PlaySfxAtPosition("GunShot", position);
		}

		public void ReloadWeapon()
		{
			_soundEffects.PlaySfx("Reload");
		}
	}
}
