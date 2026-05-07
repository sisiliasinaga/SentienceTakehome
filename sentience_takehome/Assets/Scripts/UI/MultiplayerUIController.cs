using UnityEngine;
using SentienceTakehome.Networking;
using SentienceTakehome;

public class MultiplayerUIController : MonoBehaviour
{
    [Header("References")]
    public GameUIController ui;
    public WsBattleshipClient wsClient;

    private void OnEnable()
    {
        if (ui == null || wsClient == null)
        {
            // In WebGL refresh flows, ensure we attach to the shared websocket client.
            if (wsClient == null)
            {
                wsClient = SentienceTakehome.Networking.WsBattleshipClient.Instance;
            }
            if (ui == null || wsClient == null)
            {
                return;
            }
        }

        wsClient.RoomCreated += OnRoomCreated;
        wsClient.Matched += OnMatched;
        wsClient.Resumed += OnResumed;

        if (ui.createGameButton != null)
        {
            ui.createGameButton.onClick.AddListener(OnCreateGameClicked);
        }

        if (ui.joinGameButton != null)
        {
            ui.joinGameButton.onClick.AddListener(OnJoinGameClicked);
        }

        if (ui.joinCodeInputField != null)
        {
            ui.joinCodeInputField.onSubmit.AddListener(OnJoinCodeSubmitted);
        }

        // If the player refreshed mid-game, auto-reconnect and resume.
        TryAutoResume();
    }

    private void OnDisable()
    {
        if (ui == null || wsClient == null)
        {
            return;
        }

        wsClient.RoomCreated -= OnRoomCreated;
        wsClient.Matched -= OnMatched;
        wsClient.Resumed -= OnResumed;

        if (ui.createGameButton != null)
        {
            ui.createGameButton.onClick.RemoveListener(OnCreateGameClicked);
        }

        if (ui.joinGameButton != null)
        {
            ui.joinGameButton.onClick.RemoveListener(OnJoinGameClicked);
        }

        if (ui.joinCodeInputField != null)
        {
            ui.joinCodeInputField.onSubmit.RemoveListener(OnJoinCodeSubmitted);
        }
    }

    private async void TryAutoResume()
    {
        // Only attempt if we have a persisted session.
        if (string.IsNullOrEmpty(GameSession.RoomCode) || string.IsNullOrEmpty(GameSession.PlayerToken))
        {
            return;
        }

        GameSession.Mode = GameMode.Multiplayer;

        // Show something immediately so it doesn't look like a "new game".
        ui.ShowMultiplayerPanel();
        if (ui.gameStatusText != null)
        {
            ui.gameStatusText.text = "Reconnecting...";
        }

        try
        {
            if (!wsClient.IsConnected)
            {
                await wsClient.Connect(wsClient.serverUrl);
            }

            await wsClient.Resume(GameSession.RoomCode, GameSession.PlayerToken);
            await wsClient.RequestState();
            // OnResumed (and then GameState) will transition into the right panel.
        }
        catch (System.Exception e)
        {
            if (ui.gameStatusText != null)
            {
                ui.gameStatusText.text = $"Reconnect failed: {e.Message}";
            }
        }
    }

    private void OnResumed(WsResumed msg)
    {
        // Resume succeeded; go to main panel where PlacementController/BattleController
        // will react to the incoming GameState snapshot.
        ui.ShowMainPanel();
        ui.SetFeedback("Reconnected. Restoring game state...");
    }

    private async void OnCreateGameClicked()
    {
        GameSession.Mode = GameMode.Multiplayer;

        ui.ShowCreateGamePanel();
        if (ui.gameStatusText != null)
        {
            ui.gameStatusText.text = "Creating game...";
        }
        if (ui.joinCodeText != null)
        {
            ui.joinCodeText.text = "Game Code: ...";
        }

        try
        {
            if (!wsClient.IsConnected)
            {
                if (ui.gameStatusText != null)
                {
                    ui.gameStatusText.text = "Connecting to server...";
                }
                await wsClient.Connect(wsClient.serverUrl);
            }

            if (ui.gameStatusText != null)
            {
                ui.gameStatusText.text = "Creating game...";
            }
            await wsClient.CreateRoom();
        }
        catch (System.Exception e)
        {
            if (ui.gameStatusText != null)
            {
                ui.gameStatusText.text = $"Connection failed: {e.Message}";
            }
        }
    }

    private void OnJoinGameClicked()
    {
        GameSession.Mode = GameMode.Multiplayer;
        ui.ShowJoinGamePanel();
        if (ui.joinGameStatusText != null)
        {
            ui.joinGameStatusText.text = "Enter a code to join.";
        }
        if (ui.joinCodeInputField != null)
        {
            ui.joinCodeInputField.Select();
            ui.joinCodeInputField.ActivateInputField();
        }
    }

    // Called when user presses Enter in the join code input.
    private void OnJoinCodeSubmitted(string code)
    {
        _ = JoinRoomFlow(code);
    }

    // Optional: you can also wire a "Join" button to call this from the Inspector.
    public void ConfirmJoinFromButton()
    {
        var code = ui != null && ui.joinCodeInputField != null ? ui.joinCodeInputField.text : null;
        _ = JoinRoomFlow(code);
    }

    private async System.Threading.Tasks.Task JoinRoomFlow(string code)
    {
        if (ui == null || wsClient == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            if (ui.joinGameStatusText != null)
            {
                ui.joinGameStatusText.text = "Please enter a room code.";
            }
            return;
        }

        if (ui.joinGameStatusText != null)
        {
            ui.joinGameStatusText.text = "Connecting to server...";
        }

        try
        {
            if (!wsClient.IsConnected)
            {
                await wsClient.Connect(wsClient.serverUrl);
            }

            if (ui.joinGameStatusText != null)
            {
                ui.joinGameStatusText.text = "Joining game...";
            }

            await wsClient.JoinRoom(code.Trim());
            // Wait for OnMatched callback to switch panels.
        }
        catch (System.Exception e)
        {
            if (ui.joinGameStatusText != null)
            {
                ui.joinGameStatusText.text = $"Join failed: {e.Message}";
            }
        }
    }

    private void OnRoomCreated(WsRoomCreated msg)
    {
        // Persist for reconnect later.
        GameSession.RoomCode = msg.Code;
        GameSession.PlayerToken = msg.PlayerToken;
        GameSession.SaveToPrefs();

        if (ui.joinCodeText != null)
        {
            ui.joinCodeText.text = $"Game Code: {msg.Code}";
        }
        if (ui.gameStatusText != null)
        {
            ui.gameStatusText.text = "Share this code with your opponent.";
        }
    }

    private void OnMatched(WsMatch msg)
    {
        GameSession.Mode = GameMode.Multiplayer;
        GameSession.RoomCode = msg.Code;
        GameSession.PlayerToken = msg.PlayerToken;
        GameSession.SaveToPrefs();

        // Once matched, go to the actual game placement panel.
        ui.ShowMainPanel();
        ui.SetFeedback("Connected. Place your fleet.");
    }
}

