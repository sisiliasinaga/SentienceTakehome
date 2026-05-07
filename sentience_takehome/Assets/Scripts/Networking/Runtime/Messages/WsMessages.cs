using System;

namespace SentienceTakehome.Networking
{
    [Serializable]
    public class WsEnvelope
    {
        public string Op;
    }

    [Serializable]
    public class WsError
    {
        public string Op;
        public string Code;
        public string Detail;
    }

    // ---- Client -> Server ----

    [Serializable]
    public class WsJoin
    {
        public string Op = "Join";
    }

    [Serializable]
    public class WsPlaceShip
    {
        public string Op = "PlaceShip";
        public string ShipType;      // "Carrier" ...
        public int Row;
        public int Col;
        public string Orientation;   // "Horizontal" | "Vertical"
    }

    [Serializable]
    public class WsFleetShip
    {
        public string ShipType;
        public int Row;
        public int Col;
        public string Orientation;
    }

    [Serializable]
    public class WsSubmitFleet
    {
        public string Op = "SubmitFleet";
        public WsFleetShip[] Ships;
    }

    [Serializable]
    public class WsReady
    {
        public string Op = "Ready";
    }

    [Serializable]
    public class WsFire
    {
        public string Op = "Fire";
        public int Row;
        public int Col;
    }

    [Serializable]
    public class WsCreateRoom
    {
        public string Op = "CreateRoom";
    }

    [Serializable]
    public class WsJoinRoom
    {
        public string Op = "JoinRoom";
        public string Code;
    }

    [Serializable]
    public class WsResume
    {
        public string Op = "Resume";
        public string Code;
        public string PlayerToken;
    }

    [Serializable]
    public class WsGetState
    {
        public string Op = "GetState";
    }

    // ---- Server -> Client ----

    [Serializable]
    public class WsRoomCreated
    {
        public string Op;
        public string Code;
        public string PlayerToken;
    }

    [Serializable]
    public class WsQueued
    {
        public string Op;
        public int Position;
    }

    [Serializable]
    public class WsMatch
    {
        public string Op;
        public string RoomId;
        public string Code;
        public string PlayerToken;
        public int PlayerIndex; // 0 or 1
        public string Phase;    // "Placement"
    }

    [Serializable]
    public class WsShipPlaced
    {
        public string Op;
        public bool Ok;
        public string ShipType;
        public int Row;
        public int Col;
        public string Orientation;
        public bool AllShipsPlaced;
    }

    [Serializable]
    public class WsReadyAck
    {
        public string Op;
        public int PlayerIndex;
    }

    [Serializable]
    public class WsFleetSubmitted
    {
        public string Op;
        public bool Ok;
        public string Detail; // nullable when Ok==true
        public bool AllShipsPlaced;
    }

    [Serializable]
    public class WsBattleStart
    {
        public string Op;
        public int FirstPlayerIndex;
    }

    [Serializable]
    public class WsTurn
    {
        public string Op;
        public bool Yours;
    }

    [Serializable]
    public class WsFireResult
    {
        public string Op;
        public int Row;
        public int Col;
        public string Result;        // "Miss"|"Hit"|"Sunk"
        public string SunkShipType;  // null or ship name
    }

    [Serializable]
    public class WsIncomingFire
    {
        public string Op;
        public int Row;
        public int Col;
        public string Result;
        public string SunkShipType;
    }

    [Serializable]
    public class WsFireRejected
    {
        public string Op;
        public string Reason; // "AlreadyShot"|"Invalid"
        public int Row;
        public int Col;
    }

    [Serializable]
    public class WsGameOver
    {
        public string Op;
        public int WinnerPlayerIndex;
    }

    [Serializable]
    public class WsOpponentDisconnected
    {
        public string Op;
    }

    [Serializable]
    public class WsResumed
    {
        public string Op;
        public string RoomId;
        public string Code;
        public int PlayerIndex;
    }

    [Serializable]
    public class WsGameState
    {
        public string Op;
        public string RoomId;
        public string Code;
        public string Phase;
        public int YourIndex;
        public bool? YourTurn; // null if not in battle
        public int? WinnerPlayerIndex; // set when Phase=="Ended"
        public bool YouReady;
        public bool OpponentReady;
        public bool OpponentConnected;
        // NOTE: Unity JsonUtility doesn't reliably handle nested arrays (string[][]).
        // Use flat row-major arrays length 100 (r*10+c).
        public string[] YourGridFlat;
        public string[] OpponentGridFlat;

        // Placement-phase rehydration (so refresh can restore your fleet layout).
        public WsFleetShip[] YourFleet; // nullable unless Phase=="Placement"
        public bool? AllShipsPlaced;    // nullable unless Phase=="Placement"
    }
}
