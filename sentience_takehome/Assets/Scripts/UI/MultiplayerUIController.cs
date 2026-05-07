using UnityEngine;
using SentienceTakehome.Networking;
using SentienceTakehome;

public class MultiplayerUIController : MonoBehaviour
{
    [Header("References")]
    public GameUIController ui;
    public WsBattleshipClient wsClient;

    private bool _autoResumeInFlight = false;

    private void OnEnable()
    {
        // Always prefer the shared websocket client (survives refresh/reload flows).
        var shared = SentienceTakehome.Networking.WsBattleshipClient.Instance;
        if (shared != null && wsClient != shared)
        {
            wsClient = shared;
        }

        if (ui == null || wsClient == null)
        {
            return;
        }

        wsClient.RoomCreated += OnRoomCreated;
        wsClient.Matched += OnMatched;
        wsClient.Resumed += OnResumed;
        wsClient.Error += OnWsError;

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
        wsClient.Error -= OnWsError;

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

        Debug.Log($"[auto-resume] attempting code={GameSession.RoomCode} tokenPrefix={(GameSession.PlayerToken.Length > 6 ? GameSession.PlayerToken.Substring(0, 6) : GameSession.PlayerToken)}");
        GameSession.Mode = GameMode.Multiplayer;

        _autoResumeInFlight = true;
        GameSession.AutoResumeInFlight = true;
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
            // Stay on whatever panel the app is currently showing (usually StartPanel).
            // A resume failure is not fatal; user can start/join a new game normally.
            Debug.Log($"[auto-resume] failed: {e.Message}");
        }
        finally
        {
            _autoResumeInFlight = false;
            GameSession.AutoResumeInFlight = false;
        }
    }

    private void OnResumed(WsResumed msg)
    {
        // Resume succeeded; go to main panel where PlacementController/BattleController
        // will react to the incoming GameState snapshot.
        Debug.Log($"[auto-resume] resumed playerIndex={msg.PlayerIndex} code={msg.Code}");
        ui.ShowMainPanel();
        ui.SetFeedback("Reconnected. Restoring game state...");
    }

    private void OnWsError(WsError err)
    {
        if (err == null) return;

        // Only auto-clear session during the auto-resume attempt. Otherwise, errors can
        // arrive during normal UI actions (like opening Join panel) and would cause
        // surprising panel navigation.
        if (!_autoResumeInFlight)
        {
            return;
        }

        if (err.Code == "RoomNotFound" || err.Code == "BadToken")
        {
            GameSession.ClearMultiplayerSession();
            ui.ShowStartPanel();
        }
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

