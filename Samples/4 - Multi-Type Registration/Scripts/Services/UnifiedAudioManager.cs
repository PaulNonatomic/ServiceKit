using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.MultiTypeExample
{
    /// <summary>
    /// A unified audio manager that implements multiple interfaces.
    ///
    /// This demonstrates registering a single service instance under multiple types,
    /// allowing different parts of the codebase to access it through the interface
    /// most appropriate for their needs.
    ///
    /// Using the [Service] attribute with multiple types:
    /// - Single instance is created
    /// - Accessible via any of the registered types
    /// - All types point to the same object
    /// </summary>
    [Service(typeof(IAudioPlayer), typeof(IMusicPlayer), typeof(ISoundEffects))]
    public class UnifiedAudioManager : ServiceBehaviour, IAudioPlayer, IMusicPlayer, ISoundEffects
    {
        [SerializeField] private float _masterVolume = 1.0f;
        [SerializeField] private float _musicVolume = 0.8f;
        [SerializeField] private float _sfxVolume = 1.0f;

        private string _currentTrack;

        // IAudioPlayer implementation
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                Debug.Log($"[AudioManager] Master volume set to: {_masterVolume}");
            }
        }

        public void StopAll()
        {
            Debug.Log("[AudioManager] Stopping all audio");
            _currentTrack = null;
        }

        // IMusicPlayer implementation
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                Debug.Log($"[AudioManager] Music volume set to: {_musicVolume}");
            }
        }

        public string CurrentTrack => _currentTrack;

        public void PlayMusic(string trackName)
        {
            _currentTrack = trackName;
            var effectiveVolume = _masterVolume * _musicVolume;
            Debug.Log($"[AudioManager] Playing music: {trackName} at volume {effectiveVolume:F2}");
        }

        public void StopMusic()
        {
            Debug.Log($"[AudioManager] Stopping music: {_currentTrack}");
            _currentTrack = null;
        }

        public void FadeToTrack(string trackName, float duration)
        {
            Debug.Log($"[AudioManager] Fading from '{_currentTrack}' to '{trackName}' over {duration}s");
            _currentTrack = trackName;
        }

        // ISoundEffects implementation
        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                Debug.Log($"[AudioManager] SFX volume set to: {_sfxVolume}");
            }
        }

        public void PlaySfx(string sfxName)
        {
            var effectiveVolume = _masterVolume * _sfxVolume;
            Debug.Log($"[AudioManager] Playing SFX: {sfxName} at volume {effectiveVolume:F2}");
        }

        public void PlaySfxAtPosition(string sfxName, Vector3 position)
        {
            var effectiveVolume = _masterVolume * _sfxVolume;
            Debug.Log($"[AudioManager] Playing 3D SFX: {sfxName} at {position} volume {effectiveVolume:F2}");
        }

        protected override void InitializeService()
        {
            Debug.Log("[AudioManager] Initialized - registered as IAudioPlayer, IMusicPlayer, and ISoundEffects");
        }
    }
}
