using UnityEngine;

public class CoreRulesTester : MonoBehaviour
{
    private void Start()
    {
        TestPlacementValidation();
        TestFiringResults();
        TestSunkDetection();
    }

    private void TestPlacementValidation()
    {
        var board = new Board();

        board.PlaceShip(ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);
        board.PlaceShip(ShipType.Submarine, new Coordinate(0, 1), Orientation.Horizontal);
        board.PlaceShip(ShipType.Carrier, new Coordinate(0, 7), Orientation.Horizontal);
        board.PlaceShip(ShipType.Battleship, new Coordinate(8, 0), Orientation.Vertical);
    }

    private void TestFiringResults()
    {
        var board = new Board();
        board.PlaceShip(ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);

        board.FireAt(new Coordinate(5, 5));
        board.FireAt(new Coordinate(0, 0));
        board.FireAt(new Coordinate(0, 0));
        board.FireAt(new Coordinate(10, 10));
    }

    private void TestSunkDetection()
    {
        var board = new Board();
        board.PlaceShip(ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);

        board.FireAt(new Coordinate(0, 0));
        board.FireAt(new Coordinate(0, 1));
    }
}
