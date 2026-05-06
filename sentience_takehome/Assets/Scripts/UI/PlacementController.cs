using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using SentienceTakehome.Networking;
using SentienceTakehome;
using System.Threading.Tasks;

public class PlacementController : MonoBehaviour
{
    public GridManager gridManager;
    public BattleController battleController;
    public GameUIController ui;
    public WsBattleshipClient wsClient;

    [Header("Mode")]
    [Tooltip("If true, Confirm Fleet submits to server and waits for opponent. If false, starts a local AI battle.")]
    public bool useMultiplayer = false;

    [Header("Placement Buttons")]
    public GameObject confirmFleetButton;
    public GameObject resetFleetButton;
    public GameObject autoPlaceButton;

    private Board playerBoard;

    private bool allShipsPlaced = false;

    private ShipType currentShip = ShipType.Carrier;
    private Orientation currentOrientation = Orientation.Horizontal;

    private readonly List<WsFleetShip> placedFleet = new();
    private readonly List<Coordinate> previewCoordinates = new();

    private readonly Color emptyColor = Color.white;
    private readonly Color shipColor = Color.gray;
    private readonly Color validPreviewColor = Color.green;
    private readonly Color invalidPreviewColor = Color.red;

    /// <summary>Cell under the pointer; used to refresh preview when orientation changes (R).</summary>
    private Coordinate? lastHoveredCell;

    /// <summary>Prevents double-toggle when R is seen via GetKeyDown, inputString, and OnGUI in one frame.</summary>
    private int _rotateConsumedAtFrame = -1;

    private void OnEnable()
    {
        // This object often persists while panels are toggled.
        // Re-apply mode whenever the placement panel is shown.
        useMultiplayer = GameSession.Mode == GameMode.Multiplayer;

        if (useMultiplayer)
        {
            _ = EnsureConnectedForMultiplayer();
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        // When the mainPanel opens, use the mode chosen in StartPanel.
        useMultiplayer = GameSession.Mode == GameMode.Multiplayer;

        ui.HideGameOver();
        ui.SetTurnIndicatorVisible(false);
        ui.SetFleetPlacementInfoVisible(true);

        playerBoard = new Board();
        gridManager.GenerateGrid(OnCellClicked, OnCellHovered, OnHoverExitGrid);
        confirmFleetButton.SetActive(false);

        ui.SetCurrentShip(currentShip);
        ui.SetOrientation(currentOrientation);
        ui.SetFeedback("Place your fleet.");
        ui.ClearGameOver();
    }

    private async Task EnsureConnectedForMultiplayer()
    {
        if (wsClient == null)
        {
            ui.SetFeedback("Multiplayer selected, but wsClient is not assigned.");
            return;
        }

        if (wsClient.IsConnected)
        {
            return;
        }

        ui.SetFeedback("Connecting to server...");

        try
        {
            await wsClient.Connect(wsClient.serverUrl);

            // If we already have a room + token (e.g., returning from UI panels), resume.
            if (!string.IsNullOrEmpty(GameSession.RoomCode) && !string.IsNullOrEmpty(GameSession.PlayerToken))
            {
                await wsClient.Resume(GameSession.RoomCode, GameSession.PlayerToken);
            }
        }
        catch (System.Exception e)
        {
            ui.SetFeedback($"Could not connect: {e.Message}");
        }
    }

    // Update is called once per frame
    private void Update()
    {
        if (allShipsPlaced)
        {
            return;
        }

        // Right-click, R via Input Manager, or R via typed character (WebGL / some builds).
        if (Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.R) ||
            InputStringHasRotateLetter())
        {
            TryRotateFromUser();
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || allShipsPlaced)
        {
            return;
        }

        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown || e.keyCode != KeyCode.R)
        {
            return;
        }

        TryRotateFromUser();
    }

    private static bool InputStringHasRotateLetter()
    {
        var s = Input.inputString;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == 'r' || c == 'R')
            {
                return true;
            }
        }

        return false;
    }

    private void TryRotateFromUser()
    {
        if (_rotateConsumedAtFrame == Time.frameCount)
        {
            return;
        }

        _rotateConsumedAtFrame = Time.frameCount;

        var underPointer = TryGetPlacementCellUnderPointer();
        if (underPointer.HasValue)
        {
            lastHoveredCell = underPointer;
        }

        ToggleOrientation();
    }

    private Coordinate? TryGetPlacementCellUnderPointer()
    {
        if (EventSystem.current == null || gridManager == null || gridManager.gridParent == null)
        {
            return null;
        }

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);

        foreach (var hit in hits)
        {
            var cell = hit.gameObject.GetComponent<GridCell>();
            if (cell != null && cell.transform.IsChildOf(gridManager.gridParent))
            {
                return new Coordinate(cell.Row, cell.Col);
            }
        }

        return null;
    }

    private void OnCellClicked(Coordinate coord)
    {
        bool placed = playerBoard.PlaceShip(currentShip, coord, currentOrientation);

        Debug.Log($"Placing {currentShip} at {coord} facing {currentOrientation}");

        if (placed)
        {
            placedFleet.Add(new WsFleetShip
            {
                ShipType = currentShip.ToString(),
                Row = coord.Row,
                Col = coord.Col,
                Orientation = currentOrientation.ToString()
            });
            ClearPreview();
            gridManager.ClearBattleDecorations();
            gridManager.RenderShipHullsFromBoard(playerBoard);
            RefreshPlacementCellUnderlays();
            ui.SetFeedback($"Placed {currentShip}");
            AdvanceToNextShip();
        }
        else
        {
            ui.SetFeedback($"Cannot place {currentShip} there.");
        }
    }

    private void OnCellHovered(Coordinate coord)
    {
        lastHoveredCell = coord;
        if (allShipsPlaced)
        {
            ClearPreview();
            return;
        }

        RefreshPlacementHoverVisuals(coord);
    }

    private void OnHoverExitGrid()
    {
        lastHoveredCell = null;
        ClearPreview();
    }

    private void ClearPreview()
    {
        ClearPreviewCellTints();
        gridManager.HidePlacementPreview();
    }

    private void ClearPreviewCellTints()
    {
        foreach (var pc in previewCoordinates)
        {
            gridManager.SetCellColor(pc, playerBoard.HasShipAt(pc) ? shipColor : emptyColor);
        }

        previewCoordinates.Clear();
    }

    private void RefreshPlacementHoverVisuals(Coordinate coord)
    {
        ClearPreviewCellTints();

        bool valid = playerBoard.CanPlaceShip(currentShip, coord, currentOrientation);
        var previewColor = valid ? validPreviewColor : invalidPreviewColor;
        var coords = BattleshipRules.GetShipCoordinates(coord, currentShip, currentOrientation);
        foreach (var pc in coords)
        {
            if (!BattleshipRules.IsWithinBounds(pc))
            {
                continue;
            }

            previewCoordinates.Add(pc);
            gridManager.SetCellColor(pc, previewColor);
        }

        gridManager.UpdatePlacementPreview(currentShip, coord, currentOrientation, valid);
    }

    private void RefreshPlacementCellUnderlays()
    {
        for (var r = 0; r < BattleshipRules.BoardSize; r++)
        {
            for (var c = 0; c < BattleshipRules.BoardSize; c++)
            {
                var coord = new Coordinate(r, c);
                gridManager.SetCellColor(coord, playerBoard.HasShipAt(coord) ? shipColor : emptyColor);
            }
        }
    }

    private void ToggleOrientation()
    {
        currentOrientation = currentOrientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
        Debug.Log($"Orientation changed to {currentOrientation}");

        ui.SetOrientation(currentOrientation);

        if (allShipsPlaced || !lastHoveredCell.HasValue)
        {
            return;
        }

        RefreshPlacementHoverVisuals(lastHoveredCell.Value);
    }

    private void AdvanceToNextShip()
    {
        if (currentShip == ShipType.Destroyer)
        {
            ui.SetFeedback("Fleet ready. Battle started!");
            allShipsPlaced = true;
            ClearPreview();
            confirmFleetButton.SetActive(true);
            return;
        }

        currentShip = currentShip switch
        {
            ShipType.Carrier => ShipType.Battleship,
            ShipType.Battleship => ShipType.Cruiser,
            ShipType.Cruiser => ShipType.Submarine,
            ShipType.Submarine => ShipType.Destroyer,
            _ => currentShip
        };

        ui.SetCurrentShip(currentShip);

        Debug.Log($"Next ship to place: {currentShip}");
    }

    public void ConfirmFleet()
    {
        // Don't rely on stale inspector state; always use latest session mode.
        useMultiplayer = GameSession.Mode == GameMode.Multiplayer;

        if (!allShipsPlaced)
        {
            ui.SetFeedback("You must place all ships before confirming.");
            return;
        }

        confirmFleetButton.SetActive(false);
        resetFleetButton.SetActive(false);
        autoPlaceButton.SetActive(false);

        if (!useMultiplayer)
        {
            ui.SetFleetPlacementInfoVisible(false);
            // Remove placement hover; GridCell delegates still fire if this script is only disabled.
            ClearPreview();
            gridManager.ClearHoverHandlers();
            ui.SetFeedback("Fleet confirmed. Starting AI battle...");
            battleController.StartBattleWithPlayerBoard(playerBoard);
            gameObject.SetActive(false);
            return;
        }

        if (wsClient == null || !wsClient.IsConnected)
        {
            ui.SetFeedback("Not connected to server. Cannot start multiplayer.");
            confirmFleetButton.SetActive(true);
            resetFleetButton.SetActive(true);
            autoPlaceButton.SetActive(true);
            return;
        }

        ui.SetFleetPlacementInfoVisible(false);
        ClearPreview();
        gridManager.ClearHoverHandlers();
        ui.SetFeedback("Fleet confirmed. Submitting to server...");
        _ = wsClient.SubmitFleet(placedFleet.ToArray());
        _ = wsClient.ReadyUp();
        battleController.StartMultiplayerWithPlayerBoard(playerBoard, wsClient);
        ui.SetFeedback("Waiting for opponent...");
        gameObject.SetActive(false);
    }

    private void RenderPlacedFleet()
    {
        gridManager.ClearBattleDecorations();
        gridManager.RenderShipHullsFromBoard(playerBoard);
        RefreshPlacementCellUnderlays();
    }

    public void ResetFleet()
    {
        lastHoveredCell = null;
        ClearPreview();

        playerBoard = new Board();
        placedFleet.Clear();

        currentShip = ShipType.Carrier;
        currentOrientation = Orientation.Horizontal;
        allShipsPlaced = false;

        gridManager.ClearBattleDecorations();
        gridManager.ClearGrid(emptyColor);

        if (confirmFleetButton != null)
        {
            confirmFleetButton.SetActive(false);
        }

        ui.SetCurrentShip(currentShip);
        ui.SetOrientation(currentOrientation);
        ui.SetFleetPlacementInfoVisible(true);
        ui.SetFeedback("Fleet reset.Place your carrier.");
    }

    public void AutoPlaceFleet()
    {
        lastHoveredCell = null;
        ClearPreview();

        playerBoard = new Board();
        placedFleet.Clear();

        foreach (ShipType shipType in BattleshipRules.FleetOrder)
        {
            bool placed = false;
            int attempts = 0;

            while (!placed && attempts < 1000)
            {
                int randomRow = Random.Range(0, 10);
                int randomCol = Random.Range(0, 10);

                Orientation orientation = Random.value > 0.5f ? Orientation.Horizontal : Orientation.Vertical;

                placed = playerBoard.PlaceShip(shipType, new Coordinate(randomRow, randomCol), orientation);
                if (placed)
                {
                    placedFleet.Add(new WsFleetShip
                    {
                        ShipType = shipType.ToString(),
                        Row = randomRow,
                        Col = randomCol,
                        Orientation = orientation.ToString()
                    });
                }

                attempts++;
            }

            if (!placed)
            {
                ui.SetFeedback($"Failed to auto-place {shipType}. Try again.");
                ResetFleet();
                return;
            }
        }

        gridManager.ClearGrid(emptyColor);
        RenderPlacedFleet();

        allShipsPlaced = true;
        ClearPreview();

        if (confirmFleetButton != null)
        {
            confirmFleetButton.SetActive(true);
        }

        ui.SetCurrentShip(ShipType.Destroyer);
        ui.SetFeedback("Fleet auto-placed. Confirm when ready.");
    }
}
