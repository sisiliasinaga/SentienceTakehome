using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices.WindowsRuntime;

[Serializable]
public struct Coordinate : IEquatable<Coordinate>
{
    public int Row;
    public int Col;

    public Coordinate(int row, int col)
    {
        Row = row;
        Col = col;
    }

    public bool Equals(Coordinate other)
    {
        return Row == other.Row && Col == other.Col;
    }

    public override bool Equals(object obj)
    {
        return obj is Coordinate other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Row, Col);
    }

    public override string ToString()
    { 
        return $"({Row}, {Col})";
    }
}
