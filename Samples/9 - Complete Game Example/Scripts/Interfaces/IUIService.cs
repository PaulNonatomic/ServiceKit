namespace ServiceKitSamples.CompleteGameExample
{
    /// <summary>
    /// UI management service.
    /// </summary>
    public interface IUIService
    {
        void ShowMainMenu();
        void ShowGameplay();
        void ShowPauseMenu();
        void ShowGameOver(int finalScore);
        void UpdateHealth(int health, int maxHealth);
        void UpdateScore(int score);
    }
}
