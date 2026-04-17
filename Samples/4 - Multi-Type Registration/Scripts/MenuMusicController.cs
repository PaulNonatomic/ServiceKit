using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.MultiTypeExample
{
	/// <summary>
	/// Example of a UI component that only needs music control.
	/// By depending on IMusicPlayer (not the full audio manager),
	/// this component has a minimal, focused dependency.
	/// </summary>
	[Service(typeof(MenuMusicController))]
	public class MenuMusicController : ServiceKitBehaviour
	{
		[InjectService] private IMusicPlayer _musicPlayer;

		protected override void InitializeService()
		{
			// This component only knows about music - clean separation
			_musicPlayer.PlayMusic("MenuAmbience");
		}

		public void OnMenuOpen()
		{
			_musicPlayer.MusicVolume = 0.5f;
		}

		public void OnMenuClose()
		{
			_musicPlayer.MusicVolume = 1.0f;
		}
	}
}
