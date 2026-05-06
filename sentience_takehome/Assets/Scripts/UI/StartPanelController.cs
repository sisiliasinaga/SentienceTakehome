using UnityEngine;
using SentienceTakehome;

public class StartPanelController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject startPanel;
    public GameObject mainPanel;

    [Header("Main Panel Controllers")]
    public PlacementController placementController;

    public GameUIController ui;

    public void PlayVsAI()
    {
        GameSession.Mode = GameMode.VsAI;
        ui.HideStartPanel();
        ui.ShowMainPanel();
    }

    public void PlayMultiplayer()
    {
        GameSession.Mode = GameMode.Multiplayer;
        // RoomCode/PlayerToken will be filled once networking connects/creates.
        ui.HideStartPanel();
        ui.ShowMultiplayerPanel();
    }
}

