import { Pool } from "pg";
import type { GameMode, GameRow, GameStatus, GameStore } from "./types.js";

function toRow(r: Record<string, unknown>): GameRow {
  return r as unknown as GameRow;
}

export class PostgresGameStore implements GameStore {
  private readonly pool: Pool;

  constructor(connectionString: string) {
    this.pool = new Pool({ connectionString });
  }

  async createGame(args: {
    roomCode: string;
    mode: GameMode;
    status: GameStatus;
    currentTurn: number | null;
    playerOneTokenHash: string;
    state: unknown;
  }): Promise<GameRow> {
    const q = await this.pool.query(
      `insert into games
        (room_code, mode, status, current_turn, player_one_token_hash, state_json)
       values ($1,$2,$3,$4,$5,$6)
       returning
         id::text as id,
         room_code, mode, status, current_turn,
         player_one_token_hash, player_two_token_hash,
         state_json, winner_index,
         created_at::text as created_at,
         updated_at::text as updated_at,
         completed_at::text as completed_at`,
      [
        args.roomCode,
        args.mode,
        args.status,
        args.currentTurn,
        args.playerOneTokenHash,
        args.state,
      ],
    );
    return toRow(q.rows[0] as Record<string, unknown>);
  }

  async attachSecondPlayer(args: {
    roomCode: string;
    playerTwoTokenHash: string;
    status: GameStatus;
    state: unknown;
  }): Promise<GameRow | null> {
    const q = await this.pool.query(
      `update games
         set player_two_token_hash = $2,
             status = $3,
             state_json = $4,
             updated_at = now()
       where room_code = $1
       returning
         id::text as id,
         room_code, mode, status, current_turn,
         player_one_token_hash, player_two_token_hash,
         state_json, winner_index,
         created_at::text as created_at,
         updated_at::text as updated_at,
         completed_at::text as completed_at`,
      [args.roomCode, args.playerTwoTokenHash, args.status, args.state],
    );
    return q.rows.length ? toRow(q.rows[0] as Record<string, unknown>) : null;
  }

  async getGameByRoomCode(roomCode: string): Promise<GameRow | null> {
    const q = await this.pool.query(
      `select
         id::text as id,
         room_code, mode, status, current_turn,
         player_one_token_hash, player_two_token_hash,
         state_json, winner_index,
         created_at::text as created_at,
         updated_at::text as updated_at,
         completed_at::text as completed_at
       from games where room_code = $1`,
      [roomCode],
    );
    return q.rows.length ? toRow(q.rows[0] as Record<string, unknown>) : null;
  }

  async updateGameState(args: {
    roomId: string;
    status?: GameStatus;
    currentTurn?: number | null;
    winnerIndex?: number | null;
    completedAt?: Date | null;
    state: unknown;
  }): Promise<void> {
    // Keep it simple: write full row fields we care about with COALESCE.
    await this.pool.query(
      `update games
         set status = coalesce($2, status),
             current_turn = coalesce($3, current_turn),
             winner_index = coalesce($4, winner_index),
             completed_at = $5,
             state_json = $6,
             updated_at = now()
       where id = $1::bigint`,
      [
        args.roomId,
        args.status ?? null,
        args.currentTurn ?? null,
        args.winnerIndex ?? null,
        args.completedAt === undefined ? null : args.completedAt,
        args.state,
      ],
    );
  }

  async insertMove(args: {
    gameId: string;
    playerIndex: number;
    targetRow: number;
    targetCol: number;
    result: "Miss" | "Hit" | "Sunk";
    sunkShipType: string | null;
  }): Promise<void> {
    await this.pool.query(
      `insert into moves
        (game_id, player_index, target_row, target_col, result, sunk_ship_type)
       values ($1::bigint,$2,$3,$4,$5,$6)`,
      [
        args.gameId,
        args.playerIndex,
        args.targetRow,
        args.targetCol,
        args.result,
        args.sunkShipType,
      ],
    );
  }
}

