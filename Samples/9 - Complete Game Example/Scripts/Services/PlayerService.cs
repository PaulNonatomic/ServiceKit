using System;
using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
    /// <summary>
    /// Player state management.
    /// Demonstrates: ISaveable pattern, events, service tags.
    /// </summary>
    [Service(typeof(IPlayerService))]
    public class PlayerService : ServiceBehaviour, IPlayerService
    {
        private static PlayerService _instance;

        [InjectService] private ISfxService _sfxService;

        [SerializeField] private int _startingHealth = 100;

        public string PlayerName { get; private set; } = "Player";
        public int Health { get; private set; }
        public int MaxHealth => _startingHealth;
        public int Score { get; private set; }
        public bool IsAlive => Health > 0;

        public event Action<int> OnHealthChanged;
        public event Action<int> OnScoreChanged;
        public event Action OnPlayerDied;

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
            Debug.Log("[PlayerService] Initialized");
            Reset();
        }

        public void Initialize(string playerName)
        {
            PlayerName = playerName;
            Debug.Log($"[PlayerService] Player name set to: {PlayerName}");
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            Health = Mathf.Max(0, Health - amount);
            Debug.Log($"[PlayerService] Took {amount} damage. Health: {Health}/{MaxHealth}");

            _sfxService.PlaySfx("Hurt");
            OnHealthChanged?.Invoke(Health);

            if (!IsAlive)
            {
                _sfxService.PlaySfx("Death");
                OnPlayerDied?.Invoke();
            }
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;

            Health = Mathf.Min(MaxHealth, Health + amount);
            Debug.Log($"[PlayerService] Healed {amount}. Health: {Health}/{MaxHealth}");

            _sfxService.PlaySfx("Heal");
            OnHealthChanged?.Invoke(Health);
        }

        public void AddScore(int points)
        {
            Score += points;
            Debug.Log($"[PlayerService] +{points} points. Score: {Score}");

            _sfxService.PlaySfx("Score");
            OnScoreChanged?.Invoke(Score);
        }

        public void Reset()
        {
            Health = MaxHealth;
            Score = 0;
            PlayerName = "Player";
            Debug.Log("[PlayerService] Reset to initial state");
        }

        // Save data for ISaveService
        public object GetSaveData()
        {
            return new PlayerSaveData
            {
                PlayerName = PlayerName,
                Health = Health,
                Score = Score
            };
        }

        public void LoadSaveData(object data)
        {
            if (data is PlayerSaveData saveData)
            {
                PlayerName = saveData.PlayerName;
                Health = saveData.Health;
                Score = saveData.Score;
                Debug.Log($"[PlayerService] Loaded: {PlayerName}, Health: {Health}, Score: {Score}");
            }
        }

        protected override void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            base.OnDestroy();
        }

        [Serializable]
        public class PlayerSaveData
        {
            public string PlayerName;
            public int Health;
            public int Score;
        }
    }
}
