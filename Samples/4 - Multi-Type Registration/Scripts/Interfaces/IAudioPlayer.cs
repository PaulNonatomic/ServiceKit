namespace ServiceKitSamples.MultiTypeExample
{
	/// <summary>
	/// General audio playback interface.
	/// </summary>
	public interface IAudioPlayer
	{
		float MasterVolume { get; set; }
		void StopAll();
	}
}
