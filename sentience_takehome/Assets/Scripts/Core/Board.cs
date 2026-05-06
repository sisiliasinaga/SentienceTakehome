using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShotResult
{
    public ShotResultType ResultType;
    public Coordinate Coordinate;
    public ShipType? SunkShipType;

    public ShotResult(ShotResultType resultType, Coordinate coordinate, ShipType? sunkShipType = null)
    {
        ResultType = resultType;
        Coordinate = coordinate;
        SunkShipType = sunkShipType;
    }
}

public class Board
{
    public List<Ship> Ships { get; } = new();
    public HashSet<Coordinate> ShotsReceived { get; } = new();

    public bool AllShipsPlaced => Ships.Count == BattleshipRules.FleetOrder.Length;
    public bool AllShipsSunk => Ships.Count > 0 && Ships.All(ship => ship.IsSunk);
    
    public bool CanPlaceShip(ShipType type, Coordinate start, Orientation orientation)
    {
        var coordinates = BattleshipRules.GetShipCoordinates(start, type, orientation);

        foreach (var coord in coordinates)
        {
            if (!BattleshipRules.IsWithinBounds(coord) || Ships.Any(ship => ship.Occupies(coord)))
            {
                return false;
            }
        }

        return true;
    }

    public bool PlaceShip(ShipType type, Coordinate start, Orientation orientation)
    {
        if (!CanPlaceShip(type, start, orientation))
        {
            return false;
        }
        if (Ships.Any(ship => ship.Type == type))
        {
            return false;
        }

        var coordinates = BattleshipRules.GetShipCoordinates(start, type, orientation);
        Ships.Add(new Ship(type, coordinates));
        return true;
    }

    public ShotResult FireAt(Coordinate coordinate)
    {
        if (!BattleshipRules.IsWithinBounds(coordinate))
        {
            return new ShotResult(ShotResultType.Invalid, coordinate);
        }

        if (ShotsReceived.Contains(coordinate))
        {
            return new ShotResult(ShotResultType.AlreadyShot, coordinate);
        }

        ShotsReceived.Add(coordinate);

        var hitShip = Ships.FirstOrDefault(ship => ship.Occupies(coordinate));

        if (hitShip == null)
        {
            return new ShotResult(ShotResultType.Miss, coordinate);
        }

        hitShip.RegisterHit(coordinate);

        if (hitShip.IsSunk)
        {
            return new ShotResult(ShotResultType.Sunk, coordinate, hitShip.Type);
        }

        return new ShotResult(ShotResultType.Hit, coordinate);
    }

    public bool HasShipAt(Coordinate coordinate)
    {
        return Ships.Any(ship => ship.Occupies(coordinate));
    }

    public bool WasShotAt(Coordinate coordinate)
    {
        return ShotsReceived.Contains(coordinate);
    }

    public List<Coordinate> GetShipCoordinates()
    {
        var coordinates = new List<Coordinate>();

        foreach (var ship in Ships)
        {
            coordinates.AddRange(ship.Coordinates);
        }

        return coordinates;
    }
}
