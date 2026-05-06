namespace SentienceTakehome
{
    public enum GameMode
    {
        VsAI = 0,
        Multiplayer = 1
    }

    /// <summary>
    /// Lightweight launch context set by StartPanel and read by the game UI.
    /// Static on purpose: it survives panel toggles and is simple for WebGL refresh flows later.
    /// </summary>
    public static class GameSession
    {
        public static GameMode Mode = GameMode.VsAI;

        // Multiplayer-only context (optional)
        public static string RoomCode;
        public static string PlayerToken;
    }
}

