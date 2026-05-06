using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BattleshipRules
{
    public const int BoardSize = 10;

    public static readonly ShipType[] FleetOrder =
    {
        ShipType.Carrier,
        ShipType.Battleship,
        ShipType.Cruiser,
        ShipType.Submarine,
        ShipType.Destroyer
    };

    public static int GetShipLength(ShipType type)
    {
        return type switch
        {
            ShipType.Carrier => 5,
            ShipType.Battleship => 4,
            ShipType.Cruiser => 3,
            ShipType.Submarine => 3,
            ShipType.Destroyer => 2,
            _ => throw new System.ArgumentException("Invalid ship type")
        };
    }

    public static bool IsWithinBounds(Coordinate coordinate)
    {
        return coordinate.Row >= 0 && coordinate.Row < BoardSize &&
               coordinate.Col >= 0 && coordinate.Col < BoardSize;
    }

    public static List<Coordinate> GetShipCoordinates(
        Coordinate start, ShipType ship, Orientation orientation)
    {
        var coordinates = new List<Coordinate>();
        int length = GetShipLength(ship);
        for (int i = 0; i < length; i++)
        {
            int row = start.Row + (orientation == Orientation.Vertical ? i : 0);
            int col = start.Col + (orientation == Orientation.Horizontal ? i : 0);
            coordinates.Add(new Coordinate(row, col));
        }
        return coordinates;
    }
}
