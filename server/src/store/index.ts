import type { GameStore } from "./types.js";
import { InMemoryGameStore } from "./inMemory.js";
import { PostgresGameStore } from "./postgres.js";

export function createGameStore(): GameStore {
  const url = process.env.DATABASE_URL;
  if (url && url.trim().length > 0) {
    return new PostgresGameStore(url);
  }
  return new InMemoryGameStore();
}

