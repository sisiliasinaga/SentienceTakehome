import type { GameStore } from "./types.js";
import { InMemoryGameStore } from "./inMemory.js";
import { PrismaGameStore } from "./prisma.js";

export function createGameStore(): GameStore {
  const url = process.env.DATABASE_URL;
  if (url && url.trim().length > 0) {
    return new PrismaGameStore();
  }
  return new InMemoryGameStore();
}

