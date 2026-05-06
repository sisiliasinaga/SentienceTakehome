using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShipType
{
    Carrier,
    Battleship,
    Cruiser,
    Submarine,
    Destroyer
}

public enum Orientation
{
    Horizontal,
    Vertical
}

public enum ShotResultType
{
    Miss,
    Hit,
    Sunk,
    AlreadyShot,
    Invalid
}