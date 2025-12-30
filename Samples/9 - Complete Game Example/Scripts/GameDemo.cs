using Nonatomic.ServiceKit;
using UnityEngine;

namespace ServiceKitSamples.CompleteGameExample
{
    /// <summary>
    /// Demo controller to simulate gameplay.
    /// Provides public methods that can be called from Inspector buttons or other scripts.
    /// </summary>
    public class GameDemo : MonoBehaviour
    {
        [SerializeField] private ServiceKitLocator _serviceKitLocator;

        private IGameStateService _gameState;
        private IPlayerService _player;
        private ISaveService _save;

        private async void Start()
        {
            // Get services
            _gameState = await _serviceKitLocator.GetServiceAsync<IGameStateService>();
            _player = await _serviceKitLocator.GetServiceAsync<IPlayerService>();
            _save = await _serviceKitLocator.GetServiceAsync<ISaveService>();

            Debug.Log("[GameDemo] Ready - call methods from Inspector or scripts");
        }

        [ContextMenu("Start New Game")]
        public void StartNewGame()
        {
            Debug.Log("\n--- Starting New Game ---");
            _gameState.StartGame();
        }

        [ContextMenu("Simulate Gameplay")]
        public void SimulateGameplay()
        {
            Debug.Log("\n--- Simulating Gameplay ---");

            if (_gameState.CurrentState != GameState.Playing)
            {
                Debug.LogWarning("Not in playing state. Call StartNewGame first.");
                return;
            }

            // Simulate collecting items
            _player.AddScore(100);
            _player.AddScore(50);

            // Simulate taking damage
            _player.TakeDamage(25);

            // Simulate healing
            _player.Heal(10);

            // More points
            _player.AddScore(200);

            Debug.Log($"Current state: Health={_player.Health}, Score={_player.Score}");
        }

        [ContextMenu("Take Heavy Damage")]
        public void TakeHeavyDamage()
        {
            Debug.Log("\n--- Taking Heavy Damage ---");
            _player.TakeDamage(50);
        }

        [ContextMenu("Kill Player")]
        public void KillPlayer()
        {
            Debug.Log("\n--- Killing Player ---");
            _player.TakeDamage(_player.Health + 1);
        }

        [ContextMenu("Pause Game")]
        public void PauseGame()
        {
            Debug.Log("\n--- Pausing ---");
            _gameState.PauseGame();
        }

        [ContextMenu("Resume Game")]
        public void ResumeGame()
        {
            Debug.Log("\n--- Resuming ---");
            _gameState.ResumeGame();
        }

        [ContextMenu("Save Game")]
        public void SaveGame()
        {
            Debug.Log("\n--- Saving Game ---");
            _save.SaveGame();
        }

        [ContextMenu("Load Game")]
        public void LoadGame()
        {
            Debug.Log("\n--- Loading Game ---");
            _save.LoadGame();
        }

        [ContextMenu("Delete Save")]
        public void DeleteSave()
        {
            Debug.Log("\n--- Deleting Save ---");
            _save.DeleteSave();
        }

        [ContextMenu("Return to Menu")]
        public void ReturnToMenu()
        {
            Debug.Log("\n--- Returning to Menu ---");
            _gameState.SetState(GameState.MainMenu);
        }

        [ContextMenu("Run Full Demo")]
        public void RunFullDemo()
        {
            Debug.Log("\n========== FULL DEMO ==========\n");

            // Start game
            StartNewGame();

            // Play
            SimulateGameplay();

            // Save
            SaveGame();

            // Take damage
            TakeHeavyDamage();

            // Load (restores health)
            LoadGame();

            // More gameplay
            _player.AddScore(500);

            // Kill player (triggers game over)
            KillPlayer();

            Debug.Log("\n========== DEMO COMPLETE ==========");
        }
    }
}
