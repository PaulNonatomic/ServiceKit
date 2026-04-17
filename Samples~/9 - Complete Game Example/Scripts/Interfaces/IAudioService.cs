namespace ServiceKitSamples.CompleteGameExample
{
	/// <summary>
	/// Multi-type audio service interfaces.
	/// </summary>
	public interface IAudioService
	{
		float MasterVolume { get; set; }
		void SetMute(bool muted);
	}

	public interface IMusicService
	{
		void PlayMusic(string trackName);
		void StopMusic();
		float MusicVolume { get; set; }
	}

	public interface ISfxService
	{
		void PlaySfx(string sfxName);
		float SfxVolume { get; set; }
	}
}
