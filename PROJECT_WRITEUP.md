# Sentience Take-Home: Battleship — Approach & Implementation

This document summarizes the problem framing, architecture, and tradeoffs for the Battleship game: a **Unity** client (including **WebGL**) against a **Node.js** WebSocket server with **PostgreSQL** persistence.

---

## Problem & Goals

Build a playable Battleship experience with:

- **Local vs AI** and **multiplayer** (room codes, matchmaking on the server).
- **Authoritative server**: fleet validation, turns, and shot resolution happen on the backend; clients render state and send intents (place fleet, fire).
- **Reconnect-friendly** sessions where practical (tokens + snapshots while the room still exists in memory / DB).
- **Durable games** (optional): store game rows and moves when `DATABASE_URL` is set, using **Prisma** for schema and migrations.

---

## Key Engineering Decisions

Some of the major implementation decisions were:

- Use a server-authoritative multiplayer model to reduce cheating opportunities.
- Separate the Unity WebGL frontend from the Node.js multiplayer backend.
- Use WebSockets instead of polling for low-latency turn synchronization.
- Use PostgreSQL + Prisma for persistence and game history durability.
- Keep the networking protocol lightweight and JsonUtility-compatible for Unity.
- Prioritize feature completeness and stable synchronization before UI polish.

---

## High-Level Architecture

| Layer | Role |
|--------|------|
| **Unity (`sentience_takehome/`)** | UI, grids, placement, battle rendering, `WsBattleshipClient` for JSON over WebSocket. Uses `JsonUtility`-friendly message shapes (`Op`, PascalCase fields). |
| **Server (`server/`)** | HTTP health + `/rules`; WebSocket on `/ws`. `Matchmaker` / `Room` own phases, boards, and broadcasts. |
| **PostgreSQL + Prisma** | `games` + `moves` tables; in-memory rooms still required for live play unless fully rebuilt from DB. |

**Authority model**: The server holds each player’s board, validates placement and shots, and emits **FireResult** / **IncomingFire** / **Turn** / **GameOver**. The Unity client keeps a local `Board` for the player’s own ships (placement + display) and does **not** simulate the opponent’s board in multiplayer.

The backend is hosted using Render, and the main program is hosted using Vercel.

---

## Why Unity / My Spike

I intentionally chose Unity via WebGL deployment because my background is in interactive real-time systems and game-adjacent development. I wanted the project to demonstrate both my engineering skills and my experience building interactive experiences. Using Unity also allowed me to approach the project more like a real-time system rather than a traditional CRUD-style web app. Since multiplayer game state, responsiveness, and visual feedback are core parts of the user experience, I wanted to leverage the rendering functionality and state-driven gameplay that a game engine like Unity provides. That being said, I would describe my spike as using game-engine tooling to build production-oriented interactive systems and real-time multiplayer experiences.

It is worth noting that with the use of Unity, this introduced challenges regarding WebGL deployment, networking synchronization in multiplayer mode, game state synchronization, and backend integration between Unity and Node.js. Prior to building this game, I was unfamiliar with deploying Unity WebGL applications and handling websocket connections across WebGL/browser environments.

---

## Server Design

### Transport & process

- **Stack**: Node, `ws`, TypeScript (`tsx` / `tsc` build).
- **Entry**: `server/src/index.ts` — HTTP server + `WebSocketServer({ path: "/ws" })`.
- **Health**: `GET /` or `/health` → `ok`; `GET /rules` → JSON with board size, fleet order, WebSocket path.

### Room lifecycle

- **Phases**: `Placement` → `Battle` → `Ended`.
- **Room**: Two slots (player indices 0 and 1), room **code**, hashed **player tokens**, WebSocket sockets, and `Board` instances per player.
- **Messages** (conceptually): create/join room, submit fleet, ready, fire; server replies with match info, turn updates, shot results, game over, disconnect notices.
- **Persistence**: `GameStore` abstraction; Prisma-backed implementation when `DATABASE_URL` is configured, with migrations under `server/prisma/`.

### Anti-Cheating Considerations

The server:
- stores the canonical board states
- validates ship placement
- validates all incoming shots
- computes hit/miss/sunk results
- determines win conditions

During multiplayer mode, the Unity client never has direct access to the opponent's full board state during gameplay. Only shot results are transmitted back to clients.

### Considerations (server)

- **Idempotency & validation**: Invalid moves should be rejected with clear errors; fleet must match configured ship types and non-overlapping placement.
- **Disconnects**: If one socket drops, the other can receive **OpponentDisconnected**; empty room cleanup vs keeping DB row updated is a product choice.
- **Deployment**: Build outputs `dist/`; production should run `prisma generate` and `prisma migrate deploy` before `node dist/index.js` when using Postgres.

---

## Unity Client Design

### Networking

- **Desktop / non-WebGL**: `System.Net.WebSockets` (or equivalent path in `WsBattleshipClient`).
- **WebGL**: **NativeWebSocket** UPM package — WebGL cannot use `System.Net.WebSockets`; the client switches implementation per platform.
- **Threading**: Incoming messages are marshaled to the **main thread** before invoking C# events (UI-safe).

### Session & UI flow

- **`GameSession`**: Tracks mode (vs AI vs multiplayer), room code, player token.
- **Panels**: Start → Multiplayer sub-flow (create/join) or AI → **Main** (placement + battle on the same scene).
- **`PlacementController`**: Grid setup, fleet placement, `SubmitFleet` / `ReadyUp` over WS for multiplayer, or handoff to `BattleController` for AI.
- **`BattleController`**: Own grid vs opponent grid, turn gating, `FireAt` in multiplayer, local AI opponent for single-player.

### Grids & visuals

- **`GridManager` / `GridCell`**: Layout under a `GridLayoutGroup`; **ship hulls** render on a **sibling overlay** (`ShipAndShotOverlay`) so sprites sit above cells without participating in the grid layout. `CanvasGroup.blocksRaycasts = false` on the overlay so cells still receive hover/click.
- **Placement**: Green/red **cell** preview plus semi-transparent **ghost hull**; **R** / **right-click** rotate (with `EventSystem.RaycastAll` fallback when hover events are flaky over overlays).
- **Battle**: Cell colors for ocean / ships / hits / misses; **sunk** hulls dim via tracked `HullRecord` instances.

### AI Player Design

The AI implementation uses a hunt-and-target strategy: during "hunt" mode, te AI searches for potential ship locations while avoiding duplicate shots, and after a successful hit is made, the AI enteres "target" mode and prioritizes hitting adjacent cells to finish the discovered ship. This approach significantly improves shot efficiency over purely random selection while remaining lightweight and understandable.

### UI polish

- **Turn indicator** and **fleet info** (current ship / orientation) hidden during placement and waiting; shown when battle is actually underway (multiplayer: on first **Turn** message).
- Orientation text includes a short **rotate** hint for discoverability.

### Considerations (client)

- **`GameSession.Mode`** must be set whenever the user enters multiplayer UI paths, or the client may treat multiplayer as AI.
- **Reconnect**: Depends on server still having the room and valid **Resume** + token flow; DB alone does not replace live WebSocket state without extra snapshot replay logic.
- **`using System` vs `UnityEngine.Random`**: Avoid a blanket `using System;` in scripts that use `Random.Range` — it conflicts with `System.Random`. Prefer `System.Enum.TryParse` fully qualified or an alias.

---

## Protocol & Data Shape

- **JSON** messages with an **`Op`** discriminator and **PascalCase** field names to align with Unity’s `JsonUtility`.
- Server and client must stay in lockstep on operation names and payloads (e.g. **FireResult** vs **IncomingFire**, **Turn**, **GameOver**).

---

## Testing & Operations

- **Local server**: `npm run dev` or `npm start` from `server/` after `npm run build`.
- **Unity**: Point `WsBattleshipClient.serverUrl` at `ws://<host>:8080/ws` (or deployed host).
- **Database**: Set `DATABASE_URL`, run Prisma migrations before relying on persistence.

---

## AI-Assisted Development Workflow

While much of the UI was done manually through the Unity Editor, Cursor was heavily used through the development of the Unity project's scripts and the corresponding server, along with some light assistance from ChatGPT.

### Cursor

- Used to build defined, scoped-out features that I planned beforehand. Much of this code was still reviewed manually afterward.
- Debugged and fixed more straightforward/simple bugs.
- Used for iteration speed as a collaborative tooling assistant.

### ChatGPT

- Assisted with learning unfamiliar Unity/WebGL deployment workflow.
- Found the ship sprites I use during fleet placement.

---

## Scalability Considerations

- More compact board representations (bitmask or typed arrays)
- Efficient spatial indexing for large boards
- Event batching to reduce websocket message frequency
- Horizontal scaling of websocket servers with shared room state
- Redis-style synchronization between server instances
- Persistent matchmaking/session storage externalized from process memory

For the current scope of Battleship, these optimizations are unnecessary, but these would allow the project to be developed into a larger-scale multiplayer system if need be.

---

## Challenges Encountered

- WebGL websocket differences vs desktop runtime
- Unity overlay/raycast issues during placement previews
- Deployment coordination between Render + Vercel
- Persistence integration with PostgreSQL/Prisma
- Synchronizing multiplayer turn state cleanly between clients

---

## What I’d Extend Next

- Drag-and-drop ship placement.
- Improved responsive/mobile-friendly layout
- Additional UI polish and animation
- Sound design and feedback effects
- Ranked matchmaking / lobby browser
- Automated tests for `Board` / shot rules on the server.
- Full **rehydrate room from DB** on server restart for true long-lived rooms.
- Spectator mode or replay from `moves` table.
- Input rebinding and localization for UI strings.

---

## Repository Layout (Reference)

```
SentienceTakehome/
├── server/                 # Node WebSocket + HTTP API
│   ├── src/              # room.ts, rules, store, board logic
│   └── prisma/           # schema & migrations
├── sentience_takehome/     # Unity project
│   └── Assets/Scripts/   # UI, Core (Board, Rules), Networking
└── PROJECT_WRITEUP.md    # This file
```

---
