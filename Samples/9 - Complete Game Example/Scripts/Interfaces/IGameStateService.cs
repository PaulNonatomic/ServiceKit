using System;

namespace ServiceKitSamples.CompleteGameExample
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver
    }

    /// <summary>
    /// Manages overall game state.
    /// </summary>
    public interface IGameStateService
    {
        GameState CurrentState { get; }
        event Action<GameState, GameState> OnStateChanged;
        void SetState(GameState newState);
        void StartGame();
        void PauseGame();
        void ResumeGame();
        void EndGame();
    }
}
