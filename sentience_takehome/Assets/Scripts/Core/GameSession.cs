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

        // Not persisted: used to prevent placement UI from re-initializing during refresh resume.
        public static bool AutoResumeInFlight = false;

        private const string PrefRoomCode = "SentienceTakehome.RoomCode";
        private const string PrefPlayerToken = "SentienceTakehome.PlayerToken";
        private const string PrefMode = "SentienceTakehome.GameMode";

        public static void SaveToPrefs()
        {
            UnityEngine.PlayerPrefs.SetInt(PrefMode, (int)Mode);
            UnityEngine.PlayerPrefs.SetString(PrefRoomCode, RoomCode ?? "");
            UnityEngine.PlayerPrefs.SetString(PrefPlayerToken, PlayerToken ?? "");
            UnityEngine.PlayerPrefs.Save();
        }

        public static void LoadFromPrefs()
        {
            if (UnityEngine.PlayerPrefs.HasKey(PrefMode))
            {
                Mode = (GameMode)UnityEngine.PlayerPrefs.GetInt(PrefMode, (int)GameMode.VsAI);
            }
            RoomCode = UnityEngine.PlayerPrefs.GetString(PrefRoomCode, "");
            PlayerToken = UnityEngine.PlayerPrefs.GetString(PrefPlayerToken, "");
            if (string.IsNullOrWhiteSpace(RoomCode)) RoomCode = null;
            if (string.IsNullOrWhiteSpace(PlayerToken)) PlayerToken = null;
        }

        public static void ClearMultiplayerSession()
        {
            RoomCode = null;
            PlayerToken = null;
            AutoResumeInFlight = false;
            UnityEngine.PlayerPrefs.DeleteKey(PrefRoomCode);
            UnityEngine.PlayerPrefs.DeleteKey(PrefPlayerToken);
            UnityEngine.PlayerPrefs.Save();
        }
    }
}

