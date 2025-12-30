using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
    /// <summary>
    /// Unified audio manager implementing multiple interfaces.
    /// Demonstrates: Multi-type registration with [Service] attribute.
    /// </summary>
    [Service(typeof(IAudioService), typeof(IMusicService), typeof(ISfxService))]
    public class AudioManager : ServiceBehaviour, IAudioService, IMusicService, ISfxService
    {
        private static AudioManager _instance;

        [SerializeField] private float _masterVolume = 1f;
        [SerializeField] private float _musicVolume = 0.7f;
        [SerializeField] private float _sfxVolume = 1f;

        private bool _isMuted;
        private string _currentTrack;

        // IAudioService
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                Debug.Log($"[AudioManager] Master volume: {_masterVolume}");
            }
        }

        // IMusicService
        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                Debug.Log($"[AudioManager] Music volume: {_musicVolume}");
            }
        }

        // ISfxService
        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                Debug.Log($"[AudioManager] SFX volume: {_sfxVolume}");
            }
        }

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void InitializeService()
        {
            Debug.Log("[AudioManager] Initialized (registered as IAudioService, IMusicService, ISfxService)");
        }

        public void SetMute(bool muted)
        {
            _isMuted = muted;
            Debug.Log($"[AudioManager] Mute: {_isMuted}");
        }

        public void PlayMusic(string trackName)
        {
            if (_isMuted) return;

            _currentTrack = trackName;
            var volume = _masterVolume * _musicVolume;
            Debug.Log($"[AudioManager] Playing music: {trackName} (volume: {volume:F2})");
        }

        public void StopMusic()
        {
            Debug.Log($"[AudioManager] Stopping music: {_currentTrack}");
            _currentTrack = null;
        }

        public void PlaySfx(string sfxName)
        {
            if (_isMuted) return;

            var volume = _masterVolume * _sfxVolume;
            Debug.Log($"[AudioManager] Playing SFX: {sfxName} (volume: {volume:F2})");
        }

        protected override void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            base.OnDestroy();
        }
    }
}
