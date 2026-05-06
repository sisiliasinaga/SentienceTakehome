Unity Networking (Plain WebSocket)

- Add WsBattleshipClient (MonoBehaviour) to any GameObject.
- Set serverUrl to: ws://<host>:<port>/ws

WebGL support

- WebGL cannot use System.Net.WebSockets. This project uses the NativeWebSocket package on WebGL builds.
- Install it in Unity via Package Manager:
  - Window -> Package Manager -> '+' -> Add package from git URL...
  - Use: https://github.com/endel/NativeWebSocket.git#upm
- After installing, WebGL builds will automatically use NativeWebSocket (no code changes needed).

Typical flow:
1) await client.Connect(url)
2) await client.JoinQueue()
3) For each ship placement: await client.PlaceShip(shipType, startCoord, orientation)
4) await client.ReadyUp()
5) During battle:
   - when Turn.Yours == true, allow click -> await client.FireAt(coord)

All events are invoked on Unity main thread.

NOTE: System.Net.WebSockets is not supported on WebGL builds.
