using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.ServiceKitBehaviourExample
{
	/// <summary>
	/// A MonoBehaviour-based service using ServiceKitBehaviour.
	///
	/// Key features demonstrated:
	/// - [Service] attribute for type registration
	/// - Automatic lifecycle management (register on Awake, unregister on Destroy)
	/// - InitializeService() hook for post-injection setup
	/// </summary>
	[Service(typeof(IPlayerService))]
	public class PlayerService : ServiceKitBehaviour, IPlayerService
	{
		[SerializeField] private string _defaultPlayerName = "Hero";
		[SerializeField] private int _maxHealth = 100;

		private string _playerName;
		private int _health;

		public string PlayerName => _playerName;
		public int Health => _health;
		public int MaxHealth => _maxHealth;

		/// <summary>
		/// Called after dependencies are injected but before the service is marked ready.
		/// Use this for initialization that depends on injected services.
		/// </summary>
		protected override void InitializeService()
		{
			_playerName = _defaultPlayerName;
			_health = _maxHealth;

			Debug.Log($"[PlayerService] Initialized: {_playerName} with {_health}/{_maxHealth} HP");
		}

		public void TakeDamage(int amount)
		{
			_health = Mathf.Max(0, _health - amount);
			Debug.Log($"[PlayerService] {_playerName} took {amount} damage. Health: {_health}/{_maxHealth}");

			if (_health <= 0)
			{
				Debug.Log($"[PlayerService] {_playerName} has been defeated!");
			}
		}

		public void Heal(int amount)
		{
			_health = Mathf.Min(_maxHealth, _health + amount);
			Debug.Log($"[PlayerService] {_playerName} healed for {amount}. Health: {_health}/{_maxHealth}");
		}

		public void SetPlayerName(string name)
		{
			_playerName = name;
			Debug.Log($"[PlayerService] Player name set to: {_playerName}");
		}
	}
}
