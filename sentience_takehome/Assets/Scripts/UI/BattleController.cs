using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private int turnCount = 0;
    private int playerShipsSunk = 0;
    private int opponentShipsSunk = 0;

    private readonly Color emptyColor = Color.white;
    private readonly Color shipColor = Color.gray;
    private readonly Color hitColor = Color.red;
    private readonly Color missColor = Color.blue;

    // Start is called before the first frame update
    public void StartBattleWithPlayerBoard(Board completedPlayerBoard)
    {
        playerBoard = completedPlayerBoard;
        opponentBoard = new Board();

        opponentGrid.GenerateGrid(OnOpponentGridClicked);

        RandomlyPlaceFleet(opponentBoard);

        RenderPlayerShips();

        isPlayerTurn = true;
        gameOver = false;

        ui.SetTurn(true);
        ui.SetFeedback("Battle started. Choose a target.");
        ui.ClearGameOver();

        Debug.Log("Battle started. Player turn.");
    }

    private void OnOpponentGridClicked(Coordinate coord)
    {
        if (gameOver || !isPlayerTurn) return;

        ShotResult result = opponentBoard.FireAt(coord);
        turnCount++;
        ui.SetFeedback($"You fired at {coord}: {result.ResultType}");

        if (result.ResultType == ShotResultType.AlreadyShot ||
            result.ResultType == ShotResultType.Invalid)
        {
            Debug.Log($"Invalid shot: {result.ResultType}");
            return;
        }

        RenderShotOnOpponentGrid(result);
        Debug.Log($"Player sunk AI's {result.SunkShipType}");

        if (result.ResultType == ShotResultType.Sunk)
        {
            ui.SetFeedback($"You sunk the opponent's {result.SunkShipType}!");
            opponentShipsSunk++;
        }

        if (opponentBoard.AllShipsSunk)
        {
            gameOver = true;
            Debug.Log("Player wins!");
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
        if (gameOver) return;

        ui.SetTurn(false);

        Coordinate target = GetOpponentTargetCoordinate(playerBoard);
        ShotResult result = playerBoard.FireAt(target);

        RenderShotOnPlayerGrid(result);
        ui.SetFeedback($"Enemy fired at {target}: {result.ResultType}");
        Debug.Log($"Opponent fired at {target}: {result.ResultType}");

        if (result.ResultType == ShotResultType.Sunk)
        {
            ui.SetFeedback($"Opponent sunk your {result.SunkShipType}");
            playerShipsSunk++;
        }

        if (playerBoard.AllShipsSunk)
        {
            gameOver = true;
            ui.SetFeedback("Opponent wins!");
            ui.ShowGameOver("Opponent wins!", $"Turns: {turnCount}", $"Ships sunk: {playerShipsSunk}/5");
            return;
        }

        isPlayerTurn = true;
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
    }

    private void RenderPlayerShips()
    {
        foreach (var coord in playerBoard.GetShipCoordinates())
        {
            playerGrid.SetCellColor(coord, shipColor);
        }
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
}
