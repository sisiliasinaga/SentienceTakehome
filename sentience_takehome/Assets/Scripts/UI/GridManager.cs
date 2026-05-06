using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public GridCell cellPrefab;
    public Transform gridParent;

    private GridCell[,] cells = new GridCell[10, 10];

    public void GenerateGrid(System.Action<Coordinate> onCellClick,
        System.Action<Coordinate> onCellHover = null,
        System.Action onCellHoverExit = null)
    {
        for (int row = 0; row < 10; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                var cell = Instantiate(cellPrefab, gridParent);
                cell.Init(row, col, onCellClick, onCellHover, onCellHoverExit);
                cells[row, col] = cell;
            }
        }
    }

    public void SetCellColor(Coordinate coord, Color color)
    {
        cells[coord.Row, coord.Col].SetColor(color);
    }

    public Color GetCellColor(Coordinate coord)
    {
        return cells[coord.Row, coord.Col].GetColor();
    }

    public void ClearGrid(Color color)
    {
        for (int row = 0; row < BattleshipRules.BoardSize; row++)
        {
            for (int col = 0; col < BattleshipRules.BoardSize; col++)
            {
                SetCellColor(new Coordinate(row, col), color);
            }
        }
    }
}
