-- games
create table if not exists games (
  id bigserial primary key,
  room_code text not null unique,
  status text not null,
  current_turn int null,
  player_one_token_hash text not null,
  player_two_token_hash text null,
  state_json jsonb not null,
  winner_index int null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  completed_at timestamptz null
);

-- moves
create table if not exists moves (
  id bigserial primary key,
  game_id bigint not null references games(id) on delete cascade,
  player_index int not null,
  target_row int not null,
  target_col int not null,
  result text not null,
  sunk_ship_type text null,
  created_at timestamptz not null default now()
);

create index if not exists idx_moves_game_id on moves(game_id);
create index if not exists idx_games_status on games(status);

