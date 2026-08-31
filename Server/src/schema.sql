CREATE TABLE IF NOT EXISTS players (
    user_id TEXT PRIMARY KEY,
    display_name VARCHAR(32) NOT NULL,
    is_guest BOOLEAN NOT NULL DEFAULT FALSE,
    level INTEGER NOT NULL DEFAULT 1 CHECK (level >= 1),
    xp INTEGER NOT NULL DEFAULT 0 CHECK (xp >= 0),
    coins INTEGER NOT NULL DEFAULT 0 CHECK (coins >= 0),
    selected_weapon VARCHAR(64) NOT NULL DEFAULT 'Rifle',
    best_score INTEGER NOT NULL DEFAULT 0 CHECK (best_score >= 0),
    best_score_at TIMESTAMPTZ,
    last_login_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS players_leaderboard_idx
    ON players (best_score DESC, best_score_at ASC, user_id ASC);
CREATE TABLE IF NOT EXISTS sessions (
    token_hash TEXT PRIMARY KEY,
    user_id TEXT NOT NULL REFERENCES players(user_id) ON DELETE CASCADE,
    expires_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX IF NOT EXISTS sessions_expiry_idx ON sessions (expires_at);
