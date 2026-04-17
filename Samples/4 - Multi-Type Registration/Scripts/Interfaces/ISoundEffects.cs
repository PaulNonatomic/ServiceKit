namespace ServiceKitSamples.MultiTypeExample
{
	/// <summary>
	/// Sound effects playback interface.
	/// </summary>
	public interface ISoundEffects
	{
		float SfxVolume { get; set; }
		void PlaySfx(string sfxName);
		void PlaySfxAtPosition(string sfxName, UnityEngine.Vector3 position);
	}
}
