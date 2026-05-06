export type GameStatus = "waiting" | "placement" | "battle" | "completed";
export type GameMode = "ai" | "multiplayer";

export type MoveResult = "Miss" | "Hit" | "Sunk";

export interface GameRow {
  id: string; // stringified bigint
  room_code: string;
  mode: GameMode;
  status: GameStatus;
  current_turn: number | null;
  player_one_token_hash: string;
  player_two_token_hash: string | null;
  state_json: unknown;
  winner_index: number | null;
  created_at: string;
  updated_at: string;
  completed_at: string | null;
}

export interface MoveRow {
  id: string;
  game_id: string;
  player_index: number;
  target_row: number;
  target_col: number;
  result: MoveResult;
  sunk_ship_type: string | null;
  created_at: string;
}

export interface GameStore {
  createGame(args: {
    roomCode: string;
    mode: GameMode;
    status: GameStatus;
    currentTurn: number | null;
    playerOneTokenHash: string;
    state: unknown;
  }): Promise<GameRow>;

  attachSecondPlayer(args: {
    roomCode: string;
    playerTwoTokenHash: string;
    status: GameStatus;
    state: unknown;
  }): Promise<GameRow | null>;

  getGameByRoomCode(roomCode: string): Promise<GameRow | null>;

  updateGameState(args: {
    roomId: string;
    status?: GameStatus;
    currentTurn?: number | null;
    winnerIndex?: number | null;
    completedAt?: Date | null;
    state: unknown;
  }): Promise<void>;

  insertMove(args: {
    gameId: string;
    playerIndex: number;
    targetRow: number;
    targetCol: number;
    result: MoveResult;
    sunkShipType: string | null;
  }): Promise<void>;
}

