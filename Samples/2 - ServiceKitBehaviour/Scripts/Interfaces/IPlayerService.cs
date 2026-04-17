namespace ServiceKitSamples.ServiceKitBehaviourExample
{
	/// <summary>
	/// Service interface for player-related functionality.
	/// </summary>
	public interface IPlayerService
	{
		string PlayerName { get; }
		int Health { get; }
		int MaxHealth { get; }

		void TakeDamage(int amount);
		void Heal(int amount);
		void SetPlayerName(string name);
	}
}
