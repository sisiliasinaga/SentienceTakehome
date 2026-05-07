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

    private WsGameState _lastSnapshot;
    private Coroutine _pendingHullRender;

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
        wsClient.OpponentReconnected += OnWsOpponentReconnected;
        wsClient.GameState += OnWsGameState;
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
        wsClient.OpponentReconnected -= OnWsOpponentReconnected;
        wsClient.GameState -= OnWsGameState;
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
        // Don't end the match locally; allow the opponent to refresh and resume.
        // The server keeps the room alive and will send a GameState snapshot on resume.
        opponentGrid.SetInteractable(false);
        ui.SetTurn(false);
        ui.SetTurnIndicatorVisible(true);
        ui.SetFeedback("Opponent disconnected. Waiting for them to reconnect...");
    }

    private void OnWsOpponentReconnected()
    {
        if (!isMultiplayer) return;
        // A GameState snapshot should follow; keep UI neutral until then.
        ui.SetFeedback("Opponent reconnected. Syncing...");
    }

    private void OnWsGameState(WsGameState msg)
    {
        if (!isMultiplayer) return;
        _lastSnapshot = msg;
        var dbg = $"[sync] GameState Phase={msg.Phase} Code={msg.Code} CurrentTurn={msg.CurrentTurnIndex} You={msg.YourIndex}";
        if (ui != null)
        {
            ui.SetFeedback(dbg);
        }
        Debug.Log(dbg);
        ApplySnapshot(msg);
    }

    /// <summary>
    /// Rehydrate the battle UI from a server snapshot (used on reconnect/refresh).
    /// </summary>
    public void StartMultiplayerFromSnapshot(WsBattleshipClient client, WsGameState snapshot)
    {
        isMultiplayer = true;

        var shared = SentienceTakehome.Networking.WsBattleshipClient.Instance;
        wsClient = shared != null ? shared : client;
        WireMultiplayer();

        playerBoard = null;
        opponentBoard = null;

        if (playerGrid != null)
        {
            playerGrid.ClearHoverHandlers();
        }

        opponentGrid.GenerateGrid(OnOpponentGridClicked);

        ui.SetTurnIndicatorVisible(true);
        ui.SetFleetPlacementInfoVisible(false);
        ui.ClearGameOver();

        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(WsGameState snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        // Render ship hull sprites (your own fleet) if provided.
        TryRenderHullOverlaysFromFleet(snapshot);

        // Reconstruct basic counters for UI.
        turnCount = CountOpponentShotsTaken(snapshot);
        opponentShipsSunk = CountSunkShips(snapshot?.OpponentGridFlat);

        // Phase/turn gating.
        isPlayerTurn = snapshot.CurrentTurnIndex >= 0
            ? snapshot.CurrentTurnIndex == snapshot.YourIndex
            : snapshot.YourTurn;
        opponentGrid.SetInteractable(isPlayerTurn && snapshot.Phase == "Battle");
        ui.SetTurn(isPlayerTurn);

        // Render grids from snapshot.
        RenderFromSnapshot(snapshot);

        if (snapshot.Phase == "Ended")
        {
            gameOver = true;
            opponentGrid.SetInteractable(false);
            var youWin = snapshot.WinnerPlayerIndex.HasValue &&
                         wsClient != null &&
                         wsClient.PlayerIndex.HasValue &&
                         wsClient.PlayerIndex.Value == snapshot.WinnerPlayerIndex.Value;
            ui.ShowGameOver(youWin ? "You win!" : "Opponent wins!", $"Turns: {turnCount}", $"Ships sunk: {opponentShipsSunk}/5");
            return;
        }

        gameOver = false;
        if (snapshot.Phase == "Placement")
        {
            ui.SetTurnIndicatorVisible(false);
            ui.SetFeedback("Reconnected. Finish fleet placement.");
            return;
        }

        ui.SetTurnIndicatorVisible(true);
        ui.SetFeedback(isPlayerTurn ? "Your turn. Choose a target." : "Enemy turn...");
    }

    private void TryRenderHullOverlaysFromFleet(WsGameState snapshot)
    {
        if (playerGrid == null || snapshot?.YourFleet == null || snapshot.YourFleet.Length == 0)
        {
            Debug.Log("[sync] No YourFleet received; cannot render hull sprites.");
            return;
        }

        var b = new Board();
        int okCount = 0;
        foreach (var s in snapshot.YourFleet)
        {
            if (s == null) continue;
            if (!System.Enum.TryParse(s.ShipType, true, out ShipType shipType)) continue;
            if (!System.Enum.TryParse(s.Orientation, true, out Orientation orientation))
            {
                orientation = Orientation.Horizontal;
            }

            if (b.PlaceShip(shipType, new Coordinate(s.Row, s.Col), orientation))
            {
                okCount++;
            }
            else
            {
                Debug.Log($"[sync] Failed PlaceShip {shipType} at ({s.Row},{s.Col}) {orientation}");
            }
        }

        Debug.Log($"[sync] Rendering hulls from fleet: placed {okCount}/{snapshot.YourFleet.Length}");

        // Important: after refresh/reconnect, UI layout may not be settled yet.
        // Defer hull rendering by a frame so GridCell RectTransforms report correct corners.
        if (_pendingHullRender != null)
        {
            StopCoroutine(_pendingHullRender);
        }
        _pendingHullRender = StartCoroutine(RenderHullsNextFrame(b));
    }

    private IEnumerator RenderHullsNextFrame(Board board)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (playerGrid == null) yield break;
        playerGrid.ClearBattleDecorations();
        playerGrid.RenderShipHullsFromBoard(board);
        _pendingHullRender = null;
    }

    private void RenderFromSnapshot(WsGameState snapshot)
    {
        if (playerGrid == null || opponentGrid == null)
        {
            return;
        }

        // Player grid flat: "Empty"|"Ship"|"Miss"|"Hit"|"Sunk" (len 100)
        if (snapshot.YourGridFlat != null && snapshot.YourGridFlat.Length >= 100)
        {
            for (var r = 0; r < 10; r++)
            {
                for (var c = 0; c < 10; c++)
                {
                    var idx = (r * 10) + c;
                    var cell = snapshot.YourGridFlat[idx];
                    playerGrid.SetCellColor(new Coordinate(r, c), ColorForOwnCell(cell));
                }
            }
        }

        // Opponent grid flat: "Unknown"|"Miss"|"Hit"|"Sunk" (len 100)
        if (snapshot.OpponentGridFlat != null && snapshot.OpponentGridFlat.Length >= 100)
        {
            for (var r = 0; r < 10; r++)
            {
                for (var c = 0; c < 10; c++)
                {
                    var idx = (r * 10) + c;
                    var cell = snapshot.OpponentGridFlat[idx];
                    opponentGrid.SetCellColor(new Coordinate(r, c), ColorForOpponentCell(cell));
                }
            }
        }
    }

    private Color ColorForOwnCell(string cell)
    {
        return cell switch
        {
            "Ship" => shipColor,
            "Miss" => missColor,
            "Hit" => hitColor,
            "Sunk" => hitColor,
            _ => emptyColor,
        };
    }

    private Color ColorForOpponentCell(string cell)
    {
        return cell switch
        {
            "Miss" => missColor,
            "Hit" => hitColor,
            "Sunk" => hitColor,
            // "Unknown" or anything else stays neutral.
            _ => emptyColor,
        };
    }

    private static int CountOpponentShotsTaken(WsGameState snapshot)
    {
        if (snapshot?.OpponentGridFlat == null) return 0;
        int count = 0;
        foreach (var cell in snapshot.OpponentGridFlat)
        {
            if (!string.IsNullOrEmpty(cell) && cell != "Unknown")
            {
                count++;
            }
        }
        return count;
    }

    private static int CountSunkShips(string[] opponentGridFlat)
    {
        if (opponentGridFlat == null || opponentGridFlat.Length < 100) return 0;

        var visited = new bool[10, 10];
        int ships = 0;

        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                if (visited[r, c]) continue;
                if (opponentGridFlat[(r * 10) + c] != "Sunk") continue;

                ships++;
                FloodFillSunk(opponentGridFlat, visited, r, c);
            }
        }

        return ships;
    }

    private static void FloodFillSunk(string[] gridFlat, bool[,] visited, int sr, int sc)
    {
        var stack = new Stack<(int r, int c)>();
        stack.Push((sr, sc));
        visited[sr, sc] = true;

        while (stack.Count > 0)
        {
            var (r, c) = stack.Pop();
            foreach (var (dr, dc) in OrthogonalDirections)
            {
                int nr = r + dr;
                int nc = c + dc;
                if (nr < 0 || nc < 0 || nr >= 10 || nc >= 10) continue;
                if (visited[nr, nc]) continue;
                if (gridFlat[(nr * 10) + nc] != "Sunk") continue;
                visited[nr, nc] = true;
                stack.Push((nr, nc));
            }
        }
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

    public void ReturntoMainMenu()
    {
        SentienceTakehome.GameSession.ClearMultiplayerSession();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        UnwireMultiplayer();
    }
}
