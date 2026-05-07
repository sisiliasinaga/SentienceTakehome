import { randomUUID } from "crypto";
import { createHash } from "crypto";
import type { WebSocket } from "ws";
import { Board } from "./board.js";
import {
  isOrientationName,
  isShipTypeName,
  type OrientationName,
  type ShipTypeName,
} from "./rules.js";
import type { GameStatus, GameStore } from "./store/types.js";

type Phase = "Placement" | "Battle" | "Ended";

function send(ws: WebSocket, payload: Record<string, unknown>): void {
  ws.send(JSON.stringify(payload));
}

function err(ws: WebSocket | null | undefined, code: string, detail: string): void {
  if (!ws || ws.readyState !== 1) return;
  send(ws, { Op: "Error", Code: code, Detail: detail });
}

function wsIsOpen(ws: WebSocket | null | undefined): ws is WebSocket {
  return !!ws && ws.readyState === 1;
}

function normalizeCode(code: string): string {
  return code.trim().toUpperCase();
}

function generatePlayerToken(): string {
  return randomUUID();
}

function hashToken(token: string): string {
  return createHash("sha256").update(token, "utf8").digest("hex");
}

export class Room {
  readonly Id = randomUUID();
  readonly Code: string;
  readonly GameId: string;
  private boards: [Board, Board] = [new Board(), new Board()];
  private readonly ready: [boolean, boolean] = [false, false];
  private phase: Phase = "Placement";
  private currentTurn: 0 | 1 = 0;
  private winnerIndex: 0 | 1 | null = null;
  private readonly playerTokens: [string, string];

  constructor(
    code: string,
    gameId: string,
    playerTokens: [string, string],
    readonly sockets: [WebSocket | null, WebSocket | null],
    private readonly onEmpty: () => void,
    private readonly store: GameStore,
  ) {
    this.Code = normalizeCode(code);
    this.GameId = gameId;
    this.playerTokens = playerTokens;
    const [ws0, ws1] = sockets;
    for (const [slot, ws] of [
      [0 as const, ws0],
      [1 as const, ws1],
    ] as const) {
      if (!ws) continue;
      ws.on("close", () => this.handleDisconnect(slot));
      send(ws, this.matchPayload(slot));
    }
  }

  matchPayload(slot: 0 | 1): Record<string, unknown> {
    return {
      Op: "Match",
      RoomId: this.Id,
      Code: this.Code,
      PlayerIndex: slot,
      PlayerToken: this.playerTokens[slot],
      Phase: "Placement",
    };
  }

  private persist(status: GameStatus): void {
    const payload: Parameters<GameStore["updateGameState"]>[0] = {
      roomId: this.GameId,
      status,
      state: {
        Phase: this.phase,
        CurrentTurn: this.phase === "Battle" ? this.currentTurn : null,
        Ready: this.ready,
        Boards: {
          Player0: serializeBoard(this.boards[0]),
          Player1: serializeBoard(this.boards[1]),
        },
      },
    };
    if (this.phase === "Battle") {
      payload.currentTurn = this.currentTurn;
    } else {
      payload.currentTurn = null;
    }

    void this.store
      .updateGameState(payload)
      .catch((e) => console.error("[store] updateGameState failed:", e));
  }

  private handleDisconnect(slot: 0 | 1): void {
    const other = (1 - slot) as 0 | 1;
    const otherWs = this.sockets[other];
    this.sockets[slot] = null;
    if (wsIsOpen(otherWs)) {
      send(otherWs, { Op: "OpponentDisconnected" });
    }

    // Persist room to allow reconnect; only delete if both sockets are gone.
    if (!this.sockets[0] && !this.sockets[1]) {
      this.phase = "Ended";
      this.onEmpty();
    }
  }

  private broadcastBoth(payload: Record<string, unknown>): void {
    for (const ws of this.sockets) {
      if (wsIsOpen(ws)) {
        send(ws, payload);
      }
    }
  }

  private startBattleIfReady(): void {
    if (!this.boards[0].AllShipsPlaced || !this.boards[1].AllShipsPlaced) {
      return;
    }
    if (!this.ready[0] || !this.ready[1]) {
      return;
    }
    this.phase = "Battle";
    this.currentTurn = 0;
    this.broadcastBoth({ Op: "BattleStart", FirstPlayerIndex: 0 });
    this.sendTurnState();
    this.persist("battle");
  }

  private sendTurnState(): void {
    for (const [i, ws] of [
      [0 as const, this.sockets[0]],
      [1 as const, this.sockets[1]],
    ] as const) {
      if (wsIsOpen(ws)) {
        send(ws, {
          Op: "Turn",
          Yours: this.phase === "Battle" && this.currentTurn === i,
        });
      }
    }
  }

  tryResume(playerToken: string, ws: WebSocket): 0 | 1 | null {
    const slot: 0 | 1 | null =
      this.playerTokens[0] === playerToken
        ? 0
        : this.playerTokens[1] === playerToken
          ? 1
          : null;
    if (slot === null) return null;

    const prev = this.sockets[slot];
    if (prev && prev !== ws) {
      try {
        prev.close(1000, "replaced");
      } catch {
        // ignore
      }
    }

    this.sockets[slot] = ws;
    ws.on("close", () => this.handleDisconnect(slot));

    // Tell the other player the opponent is back (fresh snapshot clears "waiting" UI).
    const other = (1 - slot) as 0 | 1;
    const otherWs = this.sockets[other];
    if (wsIsOpen(otherWs)) {
      send(otherWs, { Op: "OpponentReconnected" });
      send(otherWs, this.getSnapshotFor(other));
      // Also re-send turn state so their interactable gating is correct.
      this.sendTurnState();
    }
    return slot;
  }

  getSnapshotFor(slot: 0 | 1): Record<string, unknown> {
    const other = (1 - slot) as 0 | 1;
    const ownBoard = this.boards[slot];
    return {
      Op: "GameState",
      RoomId: this.Id,
      Code: this.Code,
      Phase: this.phase,
      YourIndex: slot,
      YourTurn: this.phase === "Battle" ? this.currentTurn === slot : null,
      WinnerPlayerIndex: this.phase === "Ended" ? this.winnerIndex : null,
      YouReady: this.ready[slot],
      OpponentReady: this.ready[other],
      OpponentConnected: this.sockets[other] !== null,
      YourGridFlat: flattenGrid(buildOwnGrid(ownBoard)),
      OpponentGridFlat: flattenGrid(buildOpponentGrid(this.boards[other])),
      // Always include your fleet so the client can render hull sprites after refresh.
      // This never reveals the opponent's ship placements.
      YourFleet: serializeFleetForPlacement(ownBoard),
      AllShipsPlaced: this.phase === "Placement" ? ownBoard.AllShipsPlaced : null,
    };
  }

  handleMessage(slot: 0 | 1, raw: string): void {
    let data: Record<string, unknown>;
    try {
      data = JSON.parse(raw) as Record<string, unknown>;
    } catch {
      err(this.sockets[slot], "BadJson", "Message was not valid JSON");
      return;
    }

    const op = data.Op;
    if (typeof op !== "string") {
      err(this.sockets[slot], "BadRequest", "Missing Op");
      return;
    }

    if (this.phase === "Ended") {
      return;
    }

    switch (op) {
      case "SubmitFleet":
        this.onSubmitFleet(slot, data);
        break;
      case "PlaceShip":
        this.onPlaceShip(slot, data);
        break;
      case "Ready":
        this.onReady(slot);
        break;
      case "Fire":
        this.onFire(slot, data);
        break;
      case "GetState": {
        const ws = this.sockets[slot];
        if (wsIsOpen(ws)) {
          send(ws, this.getSnapshotFor(slot));
        }
        break;
      }
      default:
        err(this.sockets[slot], "UnknownOp", `Unknown Op: ${op}`);
    }
  }

  private onSubmitFleet(slot: 0 | 1, data: Record<string, unknown>): void {
    if (this.phase !== "Placement") {
      err(this.sockets[slot], "BadPhase", "Cannot submit fleet in this phase");
      return;
    }

    const ships = data.Ships;
    if (!Array.isArray(ships)) {
      err(this.sockets[slot], "BadRequest", "SubmitFleet requires Ships[]");
      return;
    }

    const board = new Board();
    for (const s of ships) {
      if (typeof s !== "object" || s === null) {
        err(this.sockets[slot], "BadRequest", "Ships[] must contain objects");
        return;
      }
      const rec = s as Record<string, unknown>;
      const shipType = rec.ShipType;
      const row = rec.Row;
      const col = rec.Col;
      const orientation = rec.Orientation;

      if (
        typeof shipType !== "string" ||
        !isShipTypeName(shipType) ||
        typeof row !== "number" ||
        typeof col !== "number" ||
        typeof orientation !== "string" ||
        !isOrientationName(orientation)
      ) {
        err(this.sockets[slot], "BadRequest", "Invalid fleet ship fields");
        return;
      }

      const ok = board.PlaceShip(
        shipType as ShipTypeName,
        row,
        col,
        orientation as OrientationName,
      );
      if (!ok) {
        const ws = this.sockets[slot];
        if (wsIsOpen(ws)) {
          send(ws, {
            Op: "FleetSubmitted",
            Ok: false,
            Detail: `Failed placing ${shipType} at (${row},${col})`,
            AllShipsPlaced: false,
          });
        }
        return;
      }
    }

    if (!board.AllShipsPlaced) {
      const ws = this.sockets[slot];
      if (wsIsOpen(ws)) {
        send(ws, {
          Op: "FleetSubmitted",
          Ok: false,
          Detail: "Fleet incomplete",
          AllShipsPlaced: false,
        });
      }
      return;
    }

    // Replace any prior placement.
    this.boards[slot] = board;
    this.ready[slot] = false;

    {
      const ws = this.sockets[slot];
      if (wsIsOpen(ws)) {
        send(ws, {
          Op: "FleetSubmitted",
          Ok: true,
          Detail: null,
          AllShipsPlaced: true,
        });
      }
    }

    this.persist("placement");
  }

  private onPlaceShip(slot: 0 | 1, data: Record<string, unknown>): void {
    if (this.phase !== "Placement") {
      err(this.sockets[slot], "BadPhase", "Cannot place ships in this phase");
      return;
    }
    const shipType = data.ShipType;
    const row = data.Row;
    const col = data.Col;
    const orientation = data.Orientation;
    if (
      typeof shipType !== "string" ||
      !isShipTypeName(shipType) ||
      typeof row !== "number" ||
      typeof col !== "number" ||
      typeof orientation !== "string" ||
      !isOrientationName(orientation)
    ) {
      err(this.sockets[slot], "BadRequest", "Invalid PlaceShip fields");
      return;
    }
    const ok = this.boards[slot].PlaceShip(
      shipType as ShipTypeName,
      row,
      col,
      orientation as OrientationName,
    );
    {
      const ws = this.sockets[slot];
      if (wsIsOpen(ws)) {
        send(ws, {
          Op: "ShipPlaced",
          Ok: ok,
          ShipType: shipType,
          Row: row,
          Col: col,
          Orientation: orientation,
          AllShipsPlaced: this.boards[slot].AllShipsPlaced,
        });
      }
    }

    this.persist("placement");
  }

  private onReady(slot: 0 | 1): void {
    if (this.phase !== "Placement") {
      err(this.sockets[slot], "BadPhase", "Ready only valid in placement");
      return;
    }
    if (!this.boards[slot].AllShipsPlaced) {
      err(this.sockets[slot], "FleetIncomplete", "Place all ships before Ready");
      return;
    }
    this.ready[slot] = true;
    {
      const ws = this.sockets[slot];
      if (wsIsOpen(ws)) {
        send(ws, { Op: "ReadyAck", PlayerIndex: slot });
      }
    }
    this.startBattleIfReady();
    this.persist("placement");
  }

  private onFire(slot: 0 | 1, data: Record<string, unknown>): void {
    if (this.phase !== "Battle") {
      err(this.sockets[slot], "BadPhase", "Fire only valid in battle");
      return;
    }
    if (this.currentTurn !== slot) {
      err(this.sockets[slot], "NotYourTurn", "Wait for your turn");
      return;
    }
    const row = data.Row;
    const col = data.Col;
    if (typeof row !== "number" || typeof col !== "number") {
      err(this.sockets[slot], "BadRequest", "Fire requires Row and Col");
      return;
    }

    const target = (1 - slot) as 0 | 1;
    const board = this.boards[target];
    const result = board.FireAt(row, col);

    if (result.ResultType === "Invalid" || result.ResultType === "AlreadyShot") {
      const ws = this.sockets[slot];
      if (wsIsOpen(ws)) {
        send(ws, {
          Op: "FireRejected",
          Reason: result.ResultType,
          Row: row,
          Col: col,
        });
      }
      return;
    }

    const shooter = this.sockets[slot];
    const defender = this.sockets[target];

    if (wsIsOpen(shooter)) {
      send(shooter, {
        Op: "FireResult",
        Row: result.Row,
        Col: result.Col,
        Result: result.ResultType,
        SunkShipType: result.SunkShipType,
      });
    }

    if (wsIsOpen(defender)) {
      send(defender, {
        Op: "IncomingFire",
        Row: result.Row,
        Col: result.Col,
        Result: result.ResultType,
        SunkShipType: result.SunkShipType,
      });
    }

    if (board.AllShipsSunk) {
      this.phase = "Ended";
      this.winnerIndex = slot;
      this.broadcastBoth({
        Op: "GameOver",
        WinnerPlayerIndex: slot,
      });
      void this.store
        .insertMove({
          gameId: this.GameId,
          playerIndex: slot,
          targetRow: row,
          targetCol: col,
          result: result.ResultType,
          sunkShipType: result.SunkShipType,
        })
        .catch((e) => console.error("[store] insertMove failed:", e));
      void this.store
        .updateGameState({
          roomId: this.GameId,
          status: "completed",
          currentTurn: null,
          winnerIndex: slot,
          completedAt: new Date(),
          state: {
            Phase: "Ended",
            WinnerIndex: slot,
            Boards: {
              Player0: serializeBoard(this.boards[0]),
              Player1: serializeBoard(this.boards[1]),
            },
          },
        })
        .catch((e) => console.error("[store] updateGameState failed:", e));
      return;
    }

    this.currentTurn = target;
    this.sendTurnState();

    void this.store
      .insertMove({
        gameId: this.GameId,
        playerIndex: slot,
        targetRow: row,
        targetCol: col,
        result: result.ResultType,
        sunkShipType: result.SunkShipType,
      })
      .catch((e) => console.error("[store] insertMove failed:", e));

    this.persist("battle");
  }
}

type PendingRoom = {
  Code: string;
  Host: WebSocket;
  HostToken: string;
  HostTokenHash: string;
  GameId: string;
  CreatedAtMs: number;
};

function generateRoomCode(): string {
  // Unambiguous-ish alphabet: no 0/O, 1/I.
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  const len = 6;
  let out = "";
  for (let i = 0; i < len; i++) {
    out += alphabet[Math.floor(Math.random() * alphabet.length)];
  }
  return out;
}

/** Create-room + join-by-code matchmaking for 1v1 matches. */
export class Matchmaker {
  constructor(private readonly store: GameStore) {}

  private readonly rooms = new Map<WebSocket, { room: Room; slot: 0 | 1 }>();
  private readonly roomById = new Map<string, Room>();
  private readonly roomByCode = new Map<string, Room>();
  private readonly pendingByCode = new Map<string, PendingRoom>();

  /** Backwards compat: treat Join as CreateRoom (host). */
  private async createRoom(ws: WebSocket): Promise<void> {
    if (this.rooms.has(ws)) {
      err(ws, "AlreadyMatched", "Already in a room");
      return;
    }
    if (this.findPendingBySocket(ws)) {
      err(ws, "AlreadyHosting", "Already hosting a room");
      return;
    }

    let code = generateRoomCode();
    while (this.pendingByCode.has(code)) {
      code = generateRoomCode();
    }

    const hostToken = generatePlayerToken();
    const hostTokenHash = hashToken(hostToken);

    const game = await this.store.createGame({
      roomCode: normalizeCode(code),
      mode: "multiplayer",
      status: "waiting",
      currentTurn: null,
      playerOneTokenHash: hostTokenHash,
      state: { Phase: "WaitingForOpponent" },
    });

    const pending: PendingRoom = {
      Code: code,
      Host: ws,
      HostToken: hostToken,
      HostTokenHash: hostTokenHash,
      GameId: game.id,
      CreatedAtMs: Date.now(),
    };

    this.pendingByCode.set(code, pending);

    send(ws, { Op: "RoomCreated", Code: code, PlayerToken: hostToken });
  }

  private async joinRoom(ws: WebSocket, codeRaw: unknown): Promise<void> {
    if (this.rooms.has(ws)) {
      err(ws, "AlreadyMatched", "Already in a room");
      return;
    }
    if (this.findPendingBySocket(ws)) {
      err(ws, "AlreadyHosting", "Cannot join while hosting");
      return;
    }
    if (typeof codeRaw !== "string") {
      err(ws, "BadRequest", "JoinRoom requires Code");
      return;
    }
    const code = normalizeCode(codeRaw);
    const pending = this.pendingByCode.get(code);
    if (!pending) {
      err(ws, "RoomNotFound", "No room exists for that code");
      return;
    }
    if (pending.Host.readyState !== 1) {
      this.pendingByCode.delete(code);
      err(ws, "RoomNotFound", "Host is no longer connected");
      return;
    }
    if (pending.Host === ws) {
      err(ws, "BadRequest", "Cannot join your own room");
      return;
    }

    this.pendingByCode.delete(code);

    const host = pending.Host;
    const guestToken = generatePlayerToken();
    const guestTokenHash = hashToken(guestToken);

    const updated = await this.store.attachSecondPlayer({
      roomCode: pending.Code,
      playerTwoTokenHash: guestTokenHash,
      status: "placement",
      state: { Phase: "Placement" },
    });
    if (!updated) {
      err(ws, "RoomNotFound", "Room no longer exists");
      return;
    }

    const room = new Room(
      code,
      pending.GameId,
      [pending.HostToken, guestToken],
      [host, ws],
      () => this.detachRoom(room),
      this.store,
    );

    this.rooms.set(host, { room, slot: 0 });
    this.rooms.set(ws, { room, slot: 1 });
    this.roomById.set(room.Id, room);
    this.roomByCode.set(room.Code, room);

    // Send Match again so both sides definitely have token.
    send(host, room.matchPayload(0));
    send(ws, room.matchPayload(1));
  }

  removeFromQueue(ws: WebSocket): void {
    // called on socket close; remove from pending if hosting
    const pending = this.findPendingBySocket(ws);
    if (pending) {
      this.pendingByCode.delete(pending.Code);
    }

    // Remove socket->room mapping if present.
    this.rooms.delete(ws);
  }

  private detachRoom(room: Room): void {
    this.roomById.delete(room.Id);
    this.roomByCode.delete(room.Code);
    for (const client of room.sockets) {
      if (client) {
        this.rooms.delete(client);
      }
    }
  }

  private findPendingBySocket(ws: WebSocket): PendingRoom | undefined {
    for (const p of this.pendingByCode.values()) {
      if (p.Host === ws) {
        return p;
      }
    }
    return undefined;
  }

  getRoom(ws: WebSocket): Room | undefined {
    return this.rooms.get(ws)?.room;
  }

  async handleMessage(ws: WebSocket, raw: string): Promise<void> {
    let data: Record<string, unknown>;
    try {
      data = JSON.parse(raw) as Record<string, unknown>;
    } catch {
      err(ws, "BadJson", "Message was not valid JSON");
      return;
    }
    const op = data.Op;
    if (op === "Join" || op === "CreateRoom") {
      await this.createRoom(ws);
      return;
    }
    if (op === "JoinRoom") {
      await this.joinRoom(ws, data.Code);
      return;
    }

    // Minimal support to store AI games:
    // Client can create a server-stored AI game and then record moves/results.
    if (op === "CreateAIGame") {
      const token = generatePlayerToken();
      const tokenHash = hashToken(token);
      const code = `AI-${generateRoomCode()}`;
      const game = await this.store.createGame({
        roomCode: normalizeCode(code),
        mode: "ai",
        status: "battle",
        currentTurn: 0,
        playerOneTokenHash: tokenHash,
        state: { Phase: "Battle", Mode: "AI" },
      });
      send(ws, { Op: "AIGameCreated", GameId: game.id, RoomCode: game.room_code, PlayerToken: token });
      return;
    }

    if (op === "RecordAIMove") {
      const gameId = data.GameId;
      const playerToken = data.PlayerToken;
      const row = data.Row;
      const col = data.Col;
      const result = data.Result;
      const sunkShipType = data.SunkShipType ?? null;
      if (
        typeof gameId !== "string" ||
        typeof playerToken !== "string" ||
        typeof row !== "number" ||
        typeof col !== "number" ||
        (result !== "Miss" && result !== "Hit" && result !== "Sunk")
      ) {
        err(ws, "BadRequest", "RecordAIMove requires GameId, PlayerToken, Row, Col, Result");
        return;
      }
      // We don't validate token against DB yet; this is enough for takehome storage.
      await this.store.insertMove({
        gameId,
        playerIndex: typeof data.PlayerIndex === "number" ? data.PlayerIndex : 0,
        targetRow: row,
        targetCol: col,
        result,
        sunkShipType: typeof sunkShipType === "string" ? sunkShipType : null,
      });
      send(ws, { Op: "AIMoveRecorded", Ok: true });
      return;
    }

    if (op === "CompleteAIGame") {
      const gameId = data.GameId;
      const winnerIndex = data.WinnerIndex;
      const state = data.State ?? { Phase: "Ended" };
      if (typeof gameId !== "string" || (winnerIndex !== null && typeof winnerIndex !== "number")) {
        err(ws, "BadRequest", "CompleteAIGame requires GameId and WinnerIndex");
        return;
      }
      await this.store.updateGameState({
        roomId: gameId,
        status: "completed",
        currentTurn: null,
        winnerIndex: typeof winnerIndex === "number" ? winnerIndex : null,
        completedAt: new Date(),
        state,
      });
      send(ws, { Op: "AIGameCompleted", Ok: true });
      return;
    }

    if (op === "Resume") {
      const codeRaw = data.Code;
      const tokenRaw = data.PlayerToken;
      if (typeof codeRaw !== "string" || typeof tokenRaw !== "string") {
        err(ws, "BadRequest", "Resume requires Code and PlayerToken");
        return;
      }
      const code = normalizeCode(codeRaw);

      const room = this.roomByCode.get(code);
      if (!room) {
        err(ws, "RoomNotFound", "No active room exists for that code");
        return;
      }

      const slot = room.tryResume(tokenRaw, ws);
      if (slot === null) {
        err(ws, "BadToken", "Invalid PlayerToken for that room");
        return;
      }

      this.rooms.set(ws, { room, slot });
      console.log("[resume]", {
        code: room.Code,
        roomId: room.Id,
        slot,
        phase: (room as any).phase,
      });
      send(ws, { Op: "Resumed", RoomId: room.Id, Code: room.Code, PlayerIndex: slot });
      send(ws, room.getSnapshotFor(slot));
      return;
    }

    const attached = this.rooms.get(ws);
    if (!attached) {
      err(ws, "NotMatched", "CreateRoom or JoinRoom first");
      return;
    }
    attached.room.handleMessage(attached.slot, raw);
  }
}

type OwnCellState = "Empty" | "Ship" | "Miss" | "Hit" | "Sunk";
type OppCellState = "Unknown" | "Miss" | "Hit" | "Sunk";

function buildOwnGrid(board: Board): OwnCellState[][] {
  const size = 10;
  const grid: OwnCellState[][] = Array.from({ length: size }, () =>
    Array.from({ length: size }, () => "Empty" as OwnCellState),
  );

  // Ships first.
  for (const ship of board.Ships) {
    for (const c of ship.Coordinates) {
      if (c.Row >= 0 && c.Row < size && c.Col >= 0 && c.Col < size) {
        grid[c.Row]![c.Col] = "Ship";
      }
    }
  }

  // Misses.
  for (const k of board.ShotsReceived) {
    const [rs, cs] = k.split(",");
    const r = Number(rs);
    const c = Number(cs);
    if (Number.isNaN(r) || Number.isNaN(c)) continue;

    const isShip = board.Ships.some((s) =>
      s.Coordinates.some((cc) => cc.Row === r && cc.Col === c),
    );
    if (!isShip) {
      if (r >= 0 && r < size && c >= 0 && c < size) {
        grid[r]![c] = "Miss";
      }
    }
  }

  // Hits / sunk overwrite ship cells.
  for (const ship of board.Ships) {
    const sunk = ship.Hits.size >= ship.Coordinates.length;
    if (sunk) {
      for (const c of ship.Coordinates) {
        if (c.Row >= 0 && c.Row < size && c.Col >= 0 && c.Col < size) {
          grid[c.Row]![c.Col] = "Sunk";
        }
      }
      continue;
    }

    for (const hitKey of ship.Hits) {
      const [rs, cs] = hitKey.split(",");
      const r = Number(rs);
      const c = Number(cs);
      if (
        !Number.isNaN(r) &&
        !Number.isNaN(c) &&
        r >= 0 &&
        r < size &&
        c >= 0 &&
        c < size
      ) {
        grid[r]![c] = "Hit";
      }
    }
  }

  return grid;
}

function buildOpponentGrid(board: Board): OppCellState[][] {
  const size = 10;
  const grid: OppCellState[][] = Array.from({ length: size }, () =>
    Array.from({ length: size }, () => "Unknown" as OppCellState),
  );

  for (const k of board.ShotsReceived) {
    const [rs, cs] = k.split(",");
    const r = Number(rs);
    const c = Number(cs);
    if (Number.isNaN(r) || Number.isNaN(c)) continue;

    let state: OppCellState = "Miss";
    for (const ship of board.Ships) {
      const occupies = ship.Coordinates.some((cc) => cc.Row === r && cc.Col === c);
      if (!occupies) continue;
      const sunk = ship.Hits.size >= ship.Coordinates.length;
      state = sunk ? "Sunk" : "Hit";
      break;
    }
    if (r >= 0 && r < size && c >= 0 && c < size) {
      grid[r]![c] = state;
    }
  }

  return grid;
}

function flattenGrid<T extends string>(grid: T[][]): T[] {
  const out: T[] = [];
  for (let r = 0; r < grid.length; r++) {
    const row = grid[r] ?? [];
    for (let c = 0; c < row.length; c++) {
      out.push(row[c]!);
    }
  }
  return out;
}

function serializeBoard(board: Board): unknown {
  return {
    Ships: board.Ships.map((s) => ({
      Type: s.Type,
      Coordinates: s.Coordinates,
      Hits: Array.from(s.Hits),
      IsSunk: s.Hits.size >= s.Coordinates.length,
    })),
    ShotsReceived: Array.from(board.ShotsReceived),
    AllShipsPlaced: board.AllShipsPlaced,
    AllShipsSunk: board.AllShipsSunk,
  };
}

function serializeFleetForPlacement(
  board: Board,
): { ShipType: string; Row: number; Col: number; Orientation: "Horizontal" | "Vertical" }[] {
  const ships: {
    ShipType: string;
    Row: number;
    Col: number;
    Orientation: "Horizontal" | "Vertical";
  }[] = [];

  for (const s of board.Ships) {
    if (!s.Coordinates || s.Coordinates.length === 0) continue;

    const allSameRow = s.Coordinates.every((c) => c.Row === s.Coordinates[0]!.Row);
    const orientation: "Horizontal" | "Vertical" = allSameRow ? "Horizontal" : "Vertical";

    // Pick the lexicographically smallest coordinate as the ship "start".
    // This matches the placement API's expected start cell for shipCells().
    let start = s.Coordinates[0]!;
    for (const c of s.Coordinates) {
      if (c.Row < start.Row || (c.Row === start.Row && c.Col < start.Col)) {
        start = c;
      }
    }
    const startRow = start.Row;
    const startCol = start.Col;

    ships.push({
      ShipType: s.Type,
      Row: startRow,
      Col: startCol,
      Orientation: orientation,
    });
  }

  return ships;
}
