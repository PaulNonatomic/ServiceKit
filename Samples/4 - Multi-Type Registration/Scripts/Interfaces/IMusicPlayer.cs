namespace ServiceKitSamples.MultiTypeExample
{
	/// <summary>
	/// Music-specific playback interface.
	/// </summary>
	public interface IMusicPlayer
	{
		float MusicVolume { get; set; }
		void PlayMusic(string trackName);
		void StopMusic();
		void FadeToTrack(string trackName, float duration);
		string CurrentTrack { get; }
	}
}
