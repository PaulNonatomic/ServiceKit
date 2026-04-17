using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.MultiTypeExample
{
	/// <summary>
	/// Demonstrates consuming the same service through different interfaces.
	/// Each consumer only sees the API relevant to its needs.
	/// </summary>
	[Service(typeof(AudioConsumerDemo))]
	public class AudioConsumerDemo : ServiceKitBehaviour
	{
		// Each field injects the SAME service instance, but through different interfaces
		[InjectService] private IAudioPlayer _audioPlayer;
		[InjectService] private IMusicPlayer _musicPlayer;
		[InjectService] private ISoundEffects _soundEffects;

		protected override void InitializeService()
		{
			Debug.Log("[AudioConsumerDemo] Demonstrating multi-type registration...\n");

			// Verify all references point to the same instance
			Debug.Log($"All interfaces reference same instance: {ReferenceEquals(_audioPlayer, _musicPlayer) && ReferenceEquals(_musicPlayer, _soundEffects)}");

			DemonstrateAudioPlayer();
			DemonstrateMusicPlayer();
			DemonstrateSoundEffects();
		}

		private void DemonstrateAudioPlayer()
		{
			Debug.Log("\n--- IAudioPlayer Demo ---");

			// Settings system might use IAudioPlayer for master volume
			_audioPlayer.MasterVolume = 0.75f;
			Debug.Log($"Master volume: {_audioPlayer.MasterVolume}");
		}

		private void DemonstrateMusicPlayer()
		{
			Debug.Log("\n--- IMusicPlayer Demo ---");

			// Menu system might use IMusicPlayer for background music
			_musicPlayer.MusicVolume = 0.6f;
			_musicPlayer.PlayMusic("MainTheme");
			Debug.Log($"Current track: {_musicPlayer.CurrentTrack}");
			_musicPlayer.FadeToTrack("BattleTheme", 2.0f);
		}

		private void DemonstrateSoundEffects()
		{
			Debug.Log("\n--- ISoundEffects Demo ---");

			// Gameplay systems might use ISoundEffects
			_soundEffects.SfxVolume = 0.9f;
			_soundEffects.PlaySfx("ButtonClick");
			_soundEffects.PlaySfxAtPosition("Explosion", new Vector3(10, 0, 5));
		}
	}
}
