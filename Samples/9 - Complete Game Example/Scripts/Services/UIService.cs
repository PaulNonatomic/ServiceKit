using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
    /// <summary>
    /// UI management service.
    /// Demonstrates: Service dependencies, UI coordination.
    /// </summary>
    [Service(typeof(IUIService))]
    public class UIService : ServiceBehaviour, IUIService
    {
        private static UIService _instance;

        [InjectService] private IPlayerService _playerService;
        [InjectService] private ISfxService _sfxService;

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
            Debug.Log("[UIService] Initialized");

            // Subscribe to player events
            _playerService.OnHealthChanged += UpdateHealth;
            _playerService.OnScoreChanged += UpdateScore;
        }

        public void ShowMainMenu()
        {
            Debug.Log("[UIService] Showing Main Menu");
            _sfxService.PlaySfx("MenuOpen");
        }

        public void ShowGameplay()
        {
            Debug.Log("[UIService] Showing Gameplay HUD");
            UpdateHealth(_playerService.Health, _playerService.MaxHealth);
            UpdateScore(_playerService.Score);
        }

        public void ShowPauseMenu()
        {
            Debug.Log("[UIService] Showing Pause Menu");
            _sfxService.PlaySfx("Pause");
        }

        public void ShowGameOver(int finalScore)
        {
            Debug.Log($"[UIService] Showing Game Over - Final Score: {finalScore}");
            _sfxService.PlaySfx("GameOver");
        }

        public void UpdateHealth(int health, int maxHealth)
        {
            Debug.Log($"[UIService] Health display: {health}/{maxHealth}");
        }

        private void UpdateHealth(int health)
        {
            UpdateHealth(health, _playerService.MaxHealth);
        }

        public void UpdateScore(int score)
        {
            Debug.Log($"[UIService] Score display: {score}");
        }

        protected override void OnDestroy()
        {
            if (_playerService != null)
            {
                _playerService.OnHealthChanged -= UpdateHealth;
                _playerService.OnScoreChanged -= UpdateScore;
            }

            if (_instance == this)
            {
                _instance = null;
            }
            base.OnDestroy();
        }
    }
}
