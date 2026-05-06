using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Ship
{
    public ShipType Type { get; }
    public int Length { get; }
    public List<Coordinate> Coordinates { get; }
    public HashSet<Coordinate> Hits { get; }

    public bool IsSunk => Hits.Count >= Length;

    public Ship(ShipType type, List<Coordinate> coordinates)
    {
        Type = type;
        Length = BattleshipRules.GetShipLength(type);
        Coordinates = coordinates;
        Hits = new HashSet<Coordinate>();
    }

    public bool Occupies(Coordinate coordinate)
    {
        return Coordinates.Contains(coordinate);
    }

    public void RegisterHit(Coordinate coordinate)
    {
        if (Occupies(coordinate))
        {
            Hits.Add(coordinate);
        }
    }
}
