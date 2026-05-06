import type { GameMode, GameRow, GameStatus, GameStore } from "./types.js";

type RowInternal = Omit<GameRow, "id"> & { id: string };

function nowIso(): string {
  return new Date().toISOString();
}

export class InMemoryGameStore implements GameStore {
  private nextId = 1n;
  private readonly byRoom = new Map<string, RowInternal>();
  private readonly byId = new Map<string, RowInternal>();

  async createGame(args: {
    roomCode: string;
    mode: GameMode;
    status: GameStatus;
    currentTurn: number | null;
    playerOneTokenHash: string;
    state: unknown;
  }): Promise<GameRow> {
    const id = (this.nextId++).toString();
    const t = nowIso();
    const row: RowInternal = {
      id,
      room_code: args.roomCode,
      mode: args.mode,
      status: args.status,
      current_turn: args.currentTurn,
      player_one_token_hash: args.playerOneTokenHash,
      player_two_token_hash: null,
      state_json: args.state,
      winner_index: null,
      created_at: t,
      updated_at: t,
      completed_at: null,
    };
    this.byRoom.set(args.roomCode, row);
    this.byId.set(id, row);
    return row;
  }

  async attachSecondPlayer(args: {
    roomCode: string;
    playerTwoTokenHash: string;
    status: GameStatus;
    state: unknown;
  }): Promise<GameRow | null> {
    const row = this.byRoom.get(args.roomCode);
    if (!row) return null;
    row.player_two_token_hash = args.playerTwoTokenHash;
    row.status = args.status;
    row.state_json = args.state;
    row.updated_at = nowIso();
    return row;
  }

  async getGameByRoomCode(roomCode: string): Promise<GameRow | null> {
    return this.byRoom.get(roomCode) ?? null;
  }

  async updateGameState(args: {
    roomId: string;
    status?: GameStatus;
    currentTurn?: number | null;
    winnerIndex?: number | null;
    completedAt?: Date | null;
    state: unknown;
  }): Promise<void> {
    const row = this.byId.get(args.roomId);
    if (!row) return;
    if (args.status !== undefined) row.status = args.status;
    if (args.currentTurn !== undefined) row.current_turn = args.currentTurn;
    if (args.winnerIndex !== undefined) row.winner_index = args.winnerIndex;
    if (args.completedAt !== undefined) {
      row.completed_at = args.completedAt ? args.completedAt.toISOString() : null;
    }
    row.state_json = args.state;
    row.updated_at = nowIso();
  }

  async insertMove(_args: {
    gameId: string;
    playerIndex: number;
    targetRow: number;
    targetCol: number;
    result: "Miss" | "Hit" | "Sunk";
    sunkShipType: string | null;
  }): Promise<void> {
    // Intentionally no-op for takehome simplicity.
  }
}

