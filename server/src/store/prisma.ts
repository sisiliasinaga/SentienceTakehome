import { PrismaClient } from "@prisma/client";
import type { GameMode, GameRow, GameStatus, GameStore, MoveResult } from "./types.js";

function toGameRow(g: any): GameRow {
  return {
    id: g.id.toString(),
    room_code: g.roomCode,
    mode: g.mode,
    status: g.status,
    current_turn: g.currentTurn,
    player_one_token_hash: g.playerOneTokenHash,
    player_two_token_hash: g.playerTwoTokenHash,
    state_json: g.stateJson,
    winner_index: g.winnerIndex,
    created_at: g.createdAt.toISOString(),
    updated_at: g.updatedAt.toISOString(),
    completed_at: g.completedAt ? g.completedAt.toISOString() : null,
  };
}

export class PrismaGameStore implements GameStore {
  private readonly prisma = new PrismaClient();

  async createGame(args: {
    roomCode: string;
    mode: GameMode;
    status: GameStatus;
    currentTurn: number | null;
    playerOneTokenHash: string;
    state: unknown;
  }): Promise<GameRow> {
    const g = await this.prisma.game.create({
      data: {
        roomCode: args.roomCode,
        mode: args.mode as any,
        status: args.status as any,
        currentTurn: args.currentTurn,
        playerOneTokenHash: args.playerOneTokenHash,
        stateJson: args.state as any,
      },
    });
    return toGameRow(g);
  }

  async attachSecondPlayer(args: {
    roomCode: string;
    playerTwoTokenHash: string;
    status: GameStatus;
    state: unknown;
  }): Promise<GameRow | null> {
    try {
      const g = await this.prisma.game.update({
        where: { roomCode: args.roomCode },
        data: {
          playerTwoTokenHash: args.playerTwoTokenHash,
          status: args.status as any,
          stateJson: args.state as any,
        },
      });
      return toGameRow(g);
    } catch {
      return null;
    }
  }

  async getGameByRoomCode(roomCode: string): Promise<GameRow | null> {
    const g = await this.prisma.game.findUnique({ where: { roomCode } });
    return g ? toGameRow(g) : null;
  }

  async updateGameState(args: {
    roomId: string;
    status?: GameStatus;
    currentTurn?: number | null;
    winnerIndex?: number | null;
    completedAt?: Date | null;
    state: unknown;
  }): Promise<void> {
    const data: Record<string, unknown> = {
      stateJson: args.state as any,
    };
    if (args.status !== undefined) data.status = args.status as any;
    if (args.currentTurn !== undefined) data.currentTurn = args.currentTurn;
    if (args.winnerIndex !== undefined) data.winnerIndex = args.winnerIndex;
    if (args.completedAt !== undefined) data.completedAt = args.completedAt;

    await this.prisma.game.update({
      where: { id: BigInt(args.roomId) },
      data: data as any,
    });
  }

  async insertMove(args: {
    gameId: string;
    playerIndex: number;
    targetRow: number;
    targetCol: number;
    result: MoveResult;
    sunkShipType: string | null;
  }): Promise<void> {
    await this.prisma.move.create({
      data: {
        gameId: BigInt(args.gameId),
        playerIndex: args.playerIndex,
        targetRow: args.targetRow,
        targetCol: args.targetCol,
        result: args.result,
        sunkShipType: args.sunkShipType,
      },
    });
  }
}

