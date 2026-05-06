using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SentienceTakehome.Networking;

public class BattleController : MonoBehaviour
{
    [Header("Grids")]
    public GridManager playerGrid;
    public GridManager opponentGrid;

    public GameUIController ui;

    public GameObject rematchButton;

    private Board playerBoard;
    private Board opponentBoard;

    private bool isPlayerTurn = true;
    private bool gameOver = false;
    private bool isMultiplayer = false;

    private WsBattleshipClient wsClient;

    private int turnCount = 0;
    /// <summary>Enemy ships sunk by the local player (game over Ships sunk line).</summary>
    private int opponentShipsSunk = 0;

    private readonly Color emptyColor = Color.white;
    private readonly Color shipColor = Color.gray;
    private readonly Color hitColor = Color.red;
    private readonly Color missColor = Color.blue;

    // Start is called before the first frame update
    public void StartBattleWithPlayerBoard(Board completedPlayerBoard)
    {
        isMultiplayer = false;
        UnwireMultiplayer();

        playerBoard = completedPlayerBoard;
        opponentBoard = new Board();

        if (playerGrid != null)
        {
            playerGrid.ClearHoverHandlers();
        }

        opponentGrid.GenerateGrid(OnOpponentGridClicked);

        RandomlyPlaceFleet(opponentBoard);

        RenderPlayerShips();

        isPlayerTurn = true;
        gameOver = false;

        ui.SetTurnIndicatorVisible(true);
        ui.SetTurn(true);
        ui.SetFeedback("Battle started. Choose a target.");
        ui.ClearGameOver();
    }

    public void StartMultiplayerWithPlayerBoard(Board completedPlayerBoard, WsBattleshipClient client)
    {
        isMultiplayer = true;
        wsClient = client;
        WireMultiplayer();

        playerBoard = completedPlayerBoard;
        opponentBoard = null;

        if (playerGrid != null)
        {
            playerGrid.ClearHoverHandlers();
        }

        opponentGrid.GenerateGrid(OnOpponentGridClicked);
        RenderPlayerShips();

        // Wait for server BattleStart/Turn messages.
        isPlayerTurn = false;
        gameOver = false;
        turnCount = 0;
        opponentShipsSunk = 0;

        ui.SetTurnIndicatorVisible(false);
        ui.SetTurn(false);
        ui.SetFeedback("Waiting for opponent to confirm fleet...");
        ui.ClearGameOver();

        // Disable firing until server says it's your turn.
        opponentGrid.SetInteractable(false);
    }

    private void OnOpponentGridClicked(Coordinate coord)
    {
        if (gameOver || !isPlayerTurn) return;

        if (isMultiplayer)
        {
            _ = wsClient.FireAt(coord);
            // turn state will be updated by server via Turn message
            return;
        }

        ShotResult result = opponentBoard.FireAt(coord);
        turnCount++;
        ui.SetFeedback($"You fired at {coord}: {result.ResultType}");

        if (result.ResultType == ShotResultType.AlreadyShot ||
            result.ResultType == ShotResultType.Invalid)
        {
            return;
        }

        RenderShotOnOpponentGrid(result);

        if (result.ResultType == ShotResultType.Sunk)
        {
            ui.SetFeedback($"You sunk the opponent's {result.SunkShipType}!");
            opponentShipsSunk++;
        }

        if (opponentBoard.AllShipsSunk)
        {
            gameOver = true;
            ui.SetGameOver("You win!");
            ui.SetTurn(false);
            ui.ShowGameOver("You win!", $"Turns: {turnCount}", $"Ships sunk: {opponentShipsSunk}/5");
            return;
        }

        isPlayerTurn = false;
        Invoke(nameof(OpponentTakeTurn), 0.75f);
    }

    private void OpponentTakeTurn()
    {
        if (isMultiplayer) return;
        if (gameOver) return;

        ui.SetTurn(false);

        Coordinate target = GetOpponentTargetCoordinate(playerBoard);
        ShotResult result = playerBoard.FireAt(target);

        RenderShotOnPlayerGrid(result);
        ui.SetFeedback($"Enemy fired at {target}: {result.ResultType}");

        if (result.ResultType == ShotResultType.Sunk)
        {
            ui.SetFeedback($"Opponent sunk your {result.SunkShipType}");
        }

        if (playerBoard.AllShipsSunk)
        {
            gameOver = true;
            ui.SetFeedback("Opponent wins!");
            ui.ShowGameOver("Opponent wins!", $"Turns: {turnCount}", $"Ships sunk: {opponentShipsSunk}/5");
            return;
        }

        isPlayerTurn = true;
    }

    private void WireMultiplayer()
    {
        if (wsClient == null) return;
        wsClient.BattleStart += OnWsBattleStart;
        wsClient.Turn += OnWsTurn;
        wsClient.FireResult += OnWsFireResult;
        wsClient.IncomingFire += OnWsIncomingFire;
        wsClient.GameOver += OnWsGameOver;
        wsClient.OpponentDisconnected += OnWsOpponentDisconnected;
    }

    private void UnwireMultiplayer()
    {
        if (wsClient == null) return;
        wsClient.BattleStart -= OnWsBattleStart;
        wsClient.Turn -= OnWsTurn;
        wsClient.FireResult -= OnWsFireResult;
        wsClient.IncomingFire -= OnWsIncomingFire;
        wsClient.GameOver -= OnWsGameOver;
        wsClient.OpponentDisconnected -= OnWsOpponentDisconnected;
    }

    private void OnWsBattleStart(WsBattleStart msg)
    {
        if (!isMultiplayer) return;
        ui.SetFeedback("Battle started!");
    }

    private void OnWsTurn(WsTurn msg)
    {
        if (!isMultiplayer) return;
        isPlayerTurn = msg.Yours;
        ui.SetTurnIndicatorVisible(true);
        ui.SetTurn(isPlayerTurn);
        opponentGrid.SetInteractable(isPlayerTurn);
        if (isPlayerTurn)
        {
            ui.SetFeedback("Your turn. Choose a target.");
        }
        else
        {
            ui.SetFeedback("Enemy turn...");
        }
    }

    private void OnWsFireResult(WsFireResult msg)
    {
        if (!isMultiplayer) return;
        turnCount++;
        var coord = new Coordinate(msg.Row, msg.Col);
        if (msg.Result == "Miss")
        {
            opponentGrid.SetCellColor(coord, missColor);
        }
        else
        {
            opponentGrid.SetCellColor(coord, hitColor);
        }

        if (msg.Result == "Sunk")
        {
            opponentShipsSunk++;
            ui.SetFeedback($"You sunk the opponent's {msg.SunkShipType}!");
        }
    }

    private void OnWsIncomingFire(WsIncomingFire msg)
    {
        if (!isMultiplayer) return;
        var coord = new Coordinate(msg.Row, msg.Col);
        if (msg.Result == "Miss")
        {
            playerGrid.SetCellColor(coord, missColor);
        }
        else
        {
            playerGrid.SetCellColor(coord, hitColor);
        }

        if (msg.Result == "Sunk")
        {
            ui.SetFeedback($"Opponent sunk your {msg.SunkShipType}");
            if (!string.IsNullOrEmpty(msg.SunkShipType) &&
                System.Enum.TryParse(msg.SunkShipType, true, out ShipType sunkType))
            {
                playerGrid.DimPlayerHullForSunkShip(sunkType, coord);
            }
        }
    }

    private void OnWsGameOver(WsGameOver msg)
    {
        if (!isMultiplayer) return;
        gameOver = true;
        opponentGrid.SetInteractable(false);
        var youWin = wsClient != null && wsClient.PlayerIndex.HasValue && wsClient.PlayerIndex.Value == msg.WinnerPlayerIndex;
        if (youWin)
        {
            ui.ShowGameOver("You win!", $"Turns: {turnCount}", $"Ships sunk: {opponentShipsSunk}/5");
        }
        else
        {
            ui.ShowGameOver("Opponent wins!", $"Turns: {turnCount}", $"Ships sunk: {opponentShipsSunk}/5");
        }
    }

    private void OnWsOpponentDisconnected()
    {
        if (!isMultiplayer) return;
        gameOver = true;
        opponentGrid.SetInteractable(false);
        ui.ShowGameOver("Opponent disconnected", $"Turns: {turnCount}", $"Ships sunk: {opponentShipsSunk}/5");
    }

    private void RenderShotOnOpponentGrid(ShotResult result)
    {
        if (result.ResultType == ShotResultType.Miss)
        {
            opponentGrid.SetCellColor(result.Coordinate, missColor);
        }
        else if (result.ResultType == ShotResultType.Hit ||
            result.ResultType == ShotResultType.Sunk)
        {
            opponentGrid.SetCellColor(result.Coordinate, hitColor);
        }
    }

    private void RenderShotOnPlayerGrid(ShotResult result)
    {
        if (result.ResultType == ShotResultType.Miss)
        {
            playerGrid.SetCellColor(result.Coordinate, missColor);
        }
        else if (result.ResultType == ShotResultType.Hit ||
                 result.ResultType == ShotResultType.Sunk)
        {
            playerGrid.SetCellColor(result.Coordinate, hitColor);
        }

        if (result.ResultType == ShotResultType.Sunk && result.SunkShipType.HasValue)
        {
            playerGrid.DimPlayerHullForSunkShip(result.SunkShipType.Value, result.Coordinate);
        }
    }

    private void RenderPlayerShips()
    {
        playerGrid.ClearBattleDecorations();
        for (var r = 0; r < BattleshipRules.BoardSize; r++)
        {
            for (var c = 0; c < BattleshipRules.BoardSize; c++)
            {
                var coord = new Coordinate(r, c);
                playerGrid.SetCellColor(coord, playerBoard.HasShipAt(coord) ? shipColor : emptyColor);
            }
        }

        playerGrid.RenderShipHullsFromBoard(playerBoard);
    }

    private void PlaceTestPlayerShips()
    {
        playerBoard.PlaceShip(ShipType.Carrier, new Coordinate(0, 0), Orientation.Horizontal);
        playerBoard.PlaceShip(ShipType.Battleship, new Coordinate(2, 0), Orientation.Horizontal);
        playerBoard.PlaceShip(ShipType.Cruiser, new Coordinate(4, 0), Orientation.Horizontal);
        playerBoard.PlaceShip(ShipType.Submarine, new Coordinate(6, 0), Orientation.Horizontal);
        playerBoard.PlaceShip(ShipType.Destroyer, new Coordinate(8, 0), Orientation.Horizontal);
    }

    private void RandomlyPlaceFleet(Board board)
    {
        foreach (ShipType shipType in BattleshipRules.FleetOrder)
        {
            bool placed = false;

            while (!placed)
            {
                int row = Random.Range(0, BattleshipRules.BoardSize);
                int col = Random.Range(0, BattleshipRules.BoardSize);

                Orientation orientation = Random.value > 0.5f
                    ? Orientation.Horizontal
                    : Orientation.Vertical;

                placed = board.PlaceShip(shipType, new Coordinate(row, col), orientation);
            }
        }
    }

    /// <summary>
    /// Hunt (checkerboard) until a ship is hit, then target by extending lines and probing neighbors.
    /// </summary>
    private Coordinate GetOpponentTargetCoordinate(Board board)
    {
        var unsunkHits = new HashSet<Coordinate>();
        foreach (var ship in board.Ships)
        {
            if (ship.IsSunk)
            {
                continue;
            }

            foreach (var hit in ship.Hits)
            {
                unsunkHits.Add(hit);
            }
        }

        if (unsunkHits.Count > 0)
        {
            Coordinate? lineShot = TryPickLineExtensionTarget(board, unsunkHits);
            if (lineShot.HasValue)
            {
                return lineShot.Value;
            }

            Coordinate? adjacentShot = TryPickAdjacentToHits(board, unsunkHits);
            if (adjacentShot.HasValue)
            {
                return adjacentShot.Value;
            }
        }

        return PickHuntCoordinate(board);
    }

    private static Coordinate? TryPickLineExtensionTarget(Board board, HashSet<Coordinate> unsunkHits)
    {
        var candidates = new HashSet<Coordinate>();
        foreach (var hit in unsunkHits)
        {
            foreach (var dir in OrthogonalDirections)
            {
                var neighbor = new Coordinate(hit.Row + dir.dr, hit.Col + dir.dc);
                if (!unsunkHits.Contains(neighbor))
                {
                    continue;
                }

                var far = hit;
                var step = new Coordinate(far.Row + dir.dr, far.Col + dir.dc);
                while (unsunkHits.Contains(step))
                {
                    far = step;
                    step = new Coordinate(far.Row + dir.dr, far.Col + dir.dc);
                }

                var extend = step;
                if (BattleshipRules.IsWithinBounds(extend) && !board.WasShotAt(extend))
                {
                    candidates.Add(extend);
                }
            }
        }

        return PickRandomOrNull(candidates);
    }

    private static Coordinate? TryPickAdjacentToHits(Board board, HashSet<Coordinate> unsunkHits)
    {
        var candidates = new HashSet<Coordinate>();
        foreach (var hit in unsunkHits)
        {
            foreach (var dir in OrthogonalDirections)
            {
                var n = new Coordinate(hit.Row + dir.dr, hit.Col + dir.dc);
                if (BattleshipRules.IsWithinBounds(n) && !board.WasShotAt(n))
                {
                    candidates.Add(n);
                }
            }
        }

        return PickRandomOrNull(candidates);
    }

    private Coordinate PickHuntCoordinate(Board board)
    {
        var parityFirst = new List<Coordinate>();
        var remainder = new List<Coordinate>();

        for (int row = 0; row < BattleshipRules.BoardSize; row++)
        {
            for (int col = 0; col < BattleshipRules.BoardSize; col++)
            {
                var coord = new Coordinate(row, col);
                if (board.WasShotAt(coord))
                {
                    continue;
                }

                if ((row + col) % 2 == 0)
                {
                    parityFirst.Add(coord);
                }
                else
                {
                    remainder.Add(coord);
                }
            }
        }

        if (parityFirst.Count > 0)
        {
            return parityFirst[Random.Range(0, parityFirst.Count)];
        }

        return remainder[Random.Range(0, remainder.Count)];
    }

    private static Coordinate? PickRandomOrNull(HashSet<Coordinate> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        int i = Random.Range(0, candidates.Count);
        foreach (var c in candidates)
        {
            if (i == 0)
            {
                return c;
            }

            i--;
        }

        return null;
    }

    private static readonly (int dr, int dc)[] OrthogonalDirections =
    {
        (-1, 0), (1, 0), (0, -1), (0, 1)
    };

    public void Rematch()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        ui.HideStartPanel();
        ui.ShowMainPanel();
    }

    public void ReturntoMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void OnDestroy()
    {
        UnwireMultiplayer();
    }
}
