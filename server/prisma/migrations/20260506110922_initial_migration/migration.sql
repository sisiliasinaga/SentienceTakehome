-- CreateEnum
CREATE TYPE "GameMode" AS ENUM ('ai', 'multiplayer');

-- CreateEnum
CREATE TYPE "GameStatus" AS ENUM ('waiting', 'placement', 'battle', 'completed');

-- CreateTable
CREATE TABLE "games" (
    "id" BIGSERIAL NOT NULL,
    "room_code" TEXT NOT NULL,
    "mode" "GameMode" NOT NULL DEFAULT 'multiplayer',
    "status" "GameStatus" NOT NULL,
    "current_turn" INTEGER,
    "player_one_token_hash" TEXT NOT NULL,
    "player_two_token_hash" TEXT,
    "state_json" JSONB NOT NULL,
    "winner_index" INTEGER,
    "created_at" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(6) NOT NULL,
    "completed_at" TIMESTAMPTZ(6),

    CONSTRAINT "games_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "moves" (
    "id" BIGSERIAL NOT NULL,
    "game_id" BIGINT NOT NULL,
    "player_index" INTEGER NOT NULL,
    "target_row" INTEGER NOT NULL,
    "target_col" INTEGER NOT NULL,
    "result" TEXT NOT NULL,
    "sunk_ship_type" TEXT,
    "created_at" TIMESTAMPTZ(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "moves_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "games_room_code_key" ON "games"("room_code");

-- CreateIndex
CREATE INDEX "games_status_idx" ON "games"("status");

-- CreateIndex
CREATE INDEX "games_mode_idx" ON "games"("mode");

-- CreateIndex
CREATE INDEX "moves_game_id_idx" ON "moves"("game_id");

-- AddForeignKey
ALTER TABLE "moves" ADD CONSTRAINT "moves_game_id_fkey" FOREIGN KEY ("game_id") REFERENCES "games"("id") ON DELETE CASCADE ON UPDATE CASCADE;
