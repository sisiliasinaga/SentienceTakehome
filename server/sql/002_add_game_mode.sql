-- Add mode to distinguish AI vs multiplayer.
-- mode: 'ai' | 'multiplayer'

alter table games
  add column if not exists mode text not null default 'multiplayer';

create index if not exists idx_games_mode on games(mode);

