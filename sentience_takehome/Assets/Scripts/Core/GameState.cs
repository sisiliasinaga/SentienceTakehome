using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public enum PlayerTurn
{
    Player1,
    Player2
}

public enum GamePhase
{
    Menu,
    Placement,
    Battle,
    GameOver
}

public class GameState
{
    public Board Player1Board { get; private set; }
    public Board Player2Board { get; private set; }

    public PlayerTurn CurrentTurn { get; private set; }
    public GamePhase Phase { get; private set; }
    public PlayerTurn? Winner { get; private set; }

    public GameState()
    {
        Player1Board = new Board();
        Player2Board = new Board();
        CurrentTurn = PlayerTurn.Player1;
        Phase = GamePhase.Placement;
        Winner = null;
    }

    public bool TryPlaceShip(PlayerTurn player,
        ShipType type, Coordinate start, Orientation orientation)
    {
        var board = GetBoard(player);
        return board.PlaceShip(type, start, orientation);
    }

    public bool CanStartBattle()
    {
        return Player1Board.AllShipsPlaced && Player2Board.AllShipsPlaced;
    }

    public void StartBattle()
    {
        if (CanStartBattle())
        {
            Phase = GamePhase.Battle;
            CurrentTurn = PlayerTurn.Player1; // Player 1 starts
        }
    }

    public ShotResult Fire(Coordinate coordinate)
    {
        if (Phase != GamePhase.Battle)
            return new ShotResult(ShotResultType.Invalid, coordinate);

        var opponent = CurrentTurn == PlayerTurn.Player1 ? PlayerTurn.Player2 : PlayerTurn.Player1;
        var opponentBoard = GetBoard(opponent);
        var result = opponentBoard.FireAt(coordinate);

        if (result.ResultType == ShotResultType.Hit ||
            result.ResultType == ShotResultType.Miss ||
            result.ResultType == ShotResultType.Sunk)
        {
            if (opponentBoard.AllShipsSunk)
            {
                Winner = CurrentTurn;
                Phase = GamePhase.GameOver;
            }
            else
            {
                SwitchTurn();
            }
        }

        return result;
    }

    private Board GetBoard(PlayerTurn player)
    {
        return player == PlayerTurn.Player1 ? Player1Board : Player2Board;
    }

    private void SwitchTurn()
    {
        CurrentTurn = CurrentTurn == PlayerTurn.Player1 ? PlayerTurn.Player2 : PlayerTurn.Player1;
    }
}
