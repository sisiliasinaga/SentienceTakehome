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
        Debug.Log("=== Placement Validation Tests ===");

        var board = new Board();

        bool valid = board.PlaceShip(
            ShipType.Destroyer,
            new Coordinate(0, 0),
            Orientation.Horizontal
        );

        bool overlap = board.PlaceShip(
            ShipType.Submarine,
            new Coordinate(0, 1),
            Orientation.Horizontal
        );

        bool outOfBoundsHorizontal = board.PlaceShip(
            ShipType.Carrier,
            new Coordinate(0, 7),
            Orientation.Horizontal
        );

        bool outOfBoundsVertical = board.PlaceShip(
            ShipType.Battleship,
            new Coordinate(8, 0),
            Orientation.Vertical
        );

        Debug.Log($"Valid placement should be true: {valid}");
        Debug.Log($"Overlap placement should be false: {overlap}");
        Debug.Log($"Out of bounds horizontal should be false: {outOfBoundsHorizontal}");
        Debug.Log($"Out of bounds vertical should be false: {outOfBoundsVertical}");
    }

    private void TestFiringResults()
    {
        Debug.Log("=== Firing Result Tests ===");

        var board = new Board();

        board.PlaceShip(
            ShipType.Destroyer,
            new Coordinate(0, 0),
            Orientation.Horizontal
        );

        ShotResult miss = board.FireAt(new Coordinate(5, 5));
        ShotResult hit = board.FireAt(new Coordinate(0, 0));
        ShotResult alreadyShot = board.FireAt(new Coordinate(0, 0));
        ShotResult invalid = board.FireAt(new Coordinate(10, 10));

        Debug.Log($"Miss should be Miss: {miss.ResultType}");
        Debug.Log($"Hit should be Hit: {hit.ResultType}");
        Debug.Log($"Already shot should be AlreadyShot: {alreadyShot.ResultType}");
        Debug.Log($"Invalid should be Invalid: {invalid.ResultType}");
    }

    private void TestSunkDetection()
    {
        Debug.Log("=== Sunk Detection Tests ===");

        var board = new Board();

        board.PlaceShip(
            ShipType.Destroyer,
            new Coordinate(0, 0),
            Orientation.Horizontal
        );

        ShotResult firstHit = board.FireAt(new Coordinate(0, 0));
        ShotResult secondHit = board.FireAt(new Coordinate(0, 1));

        Debug.Log($"First hit should be Hit: {firstHit.ResultType}");
        Debug.Log($"Second hit should be Sunk: {secondHit.ResultType}");
        Debug.Log($"Sunk ship should be Destroyer: {secondHit.SunkShipType}");
        Debug.Log($"All ships sunk should be true: {board.AllShipsSunk}");
    }
}