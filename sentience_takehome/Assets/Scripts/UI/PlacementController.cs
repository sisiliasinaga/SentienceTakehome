using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SentienceTakehome.Networking;
using SentienceTakehome;

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

    private readonly List<Coordinate> previewCoordinates = new();
    private readonly List<WsFleetShip> placedFleet = new();

    private readonly Color emptyColor = Color.white;
    private readonly Color shipColor = Color.gray;
    private readonly Color validPreviewColor = Color.green;
    private readonly Color invalidPreviewColor = Color.red;

    // Start is called before the first frame update
    private void Start()
    {
        // When the mainPanel opens, use the mode chosen in StartPanel.
        useMultiplayer = GameSession.Mode == GameMode.Multiplayer;

        ui.HideGameOver();

        playerBoard = new Board();
        gridManager.GenerateGrid(OnCellClicked, OnCellHovered, ClearPreview);
        confirmFleetButton.SetActive(false);

        ui.SetCurrentShip(currentShip);
        ui.SetOrientation(currentOrientation);
        ui.SetFeedback("Place your fleet.");
        ui.ClearGameOver();
    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleOrientation();
        }
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
            ColorPlacedShip(coord);
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
        ClearPreview();

        previewCoordinates.Clear();

        var coords = BattleshipRules.GetShipCoordinates(coord, currentShip, currentOrientation);
        bool valid = playerBoard.CanPlaceShip(currentShip, coord, currentOrientation);
        Color previewColor = valid ? validPreviewColor : invalidPreviewColor;

        foreach (var previewCoord in coords)
        {
            if (!BattleshipRules.IsWithinBounds(previewCoord))
            {
                continue;
            }

            previewCoordinates.Add(previewCoord);
            gridManager.SetCellColor(previewCoord, previewColor);
        }
    }

    private void ClearPreview()
    {
        foreach (var coord in previewCoordinates)
        {
            if (playerBoard.HasShipAt(coord))
            {
                gridManager.SetCellColor(coord, shipColor);
            }
            else
            {
                gridManager.SetCellColor(coord, emptyColor);
            }
        }
        previewCoordinates.Clear();
    }

    private void ColorPlacedShip(Coordinate start)
    {
        var coords = BattleshipRules.GetShipCoordinates(start, currentShip, currentOrientation);

        foreach (var coord in coords)
        {
            gridManager.SetCellColor(coord, shipColor);
        }
    }

    private void ToggleOrientation()
    {
        currentOrientation = currentOrientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
        Debug.Log($"Orientation changed to {currentOrientation}");

        ui.SetOrientation(currentOrientation);
    }

    private void AdvanceToNextShip()
    {
        if (currentShip == ShipType.Destroyer)
        {
            ui.SetFeedback("Fleet ready. Battle started!");
            allShipsPlaced = true;
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

        ui.SetFeedback("Fleet confirmed. Submitting to server...");
        _ = wsClient.SubmitFleet(placedFleet.ToArray());
        _ = wsClient.ReadyUp();
        ui.SetFeedback("Waiting for opponent...");
        gameObject.SetActive(false);
    }

    private void RenderPlacedFleet()
    {
        foreach (var coord in playerBoard.GetShipCoordinates())
        {
            gridManager.SetCellColor(coord, shipColor);
        }
    }

    public void ResetFleet()
    {
        ClearPreview();

        playerBoard = new Board();
        placedFleet.Clear();

        currentShip = ShipType.Carrier;
        currentOrientation = Orientation.Horizontal;
        allShipsPlaced = false;

        gridManager.ClearGrid(emptyColor);

        if (confirmFleetButton != null)
        {
            confirmFleetButton.SetActive(false);
        }

        ui.SetCurrentShip(currentShip);
        ui.SetOrientation(currentOrientation);
        ui.SetFeedback("Fleet reset.Place your carrier.");
    }

    public void AutoPlaceFleet()
    {
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

        if (confirmFleetButton != null)
        {
            confirmFleetButton.SetActive(true);
        }

        ui.SetCurrentShip(ShipType.Destroyer);
        ui.SetFeedback("Fleet auto-placed. Confirm when ready.");
    }
}
