using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridTester : MonoBehaviour
{
    public GridManager gridManager;

    // Start is called before the first frame update
    private void Start()
    {
        gridManager.GenerateGrid(OnCellClicked);
    }

    private void OnCellClicked(Coordinate coord)
    {
        // Visual feedback
        gridManager.SetCellColor(coord, Color.red);
    }
}
