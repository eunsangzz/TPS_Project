const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const path = require("node:path");

function tokenHash(token) {
  return crypto.createHash("sha256").update(token).digest("hex");
}

function playerData(row) {
  return {
    userId: row.user_id, displayName: row.display_name,
    level: row.level, xp: row.xp, coins: row.coins, selectedWeapon: row.selected_weapon,
    bestScore: row.best_score, lastLoginAt: row.last_login_at, updatedAt: row.updated_at,
  };
}

class PlayerStore {
  constructor(db) { this.db = db; }

  async initialize() {
    await this.db.query(await fs.readFile(path.join(__dirname, "schema.sql"), "utf8"));
    await this.db.query("DELETE FROM sessions WHERE expires_at <= NOW()");
  }

  async createSession({ id, displayName, isGuest }) {
    await this.db.query(
      `INSERT INTO players (user_id, display_name, is_guest, coins)
       VALUES ($1, $2, $3, $4)
       ON CONFLICT (user_id) DO UPDATE SET last_login_at = NOW()`,
      [id, displayName, isGuest, isGuest ? 0 : 100],
    );
    const token = crypto.randomBytes(32).toString("hex");
    await this.db.query(
      "INSERT INTO sessions (token_hash, user_id, expires_at) VALUES ($1, $2, NOW() + INTERVAL '30 days')",
      [tokenHash(token), id],
    );
    return { token, user: await this.authenticate(token) };
  }

  async authenticate(token) {
    const { rows } = await this.db.query(
      `SELECT p.user_id, p.display_name, p.is_guest FROM sessions s
       JOIN players p ON p.user_id = s.user_id
       WHERE s.token_hash = $1 AND s.expires_at > NOW()`,
      [tokenHash(token)],
    );
    if (!rows[0]) return null;
    return {
      id: rows[0].user_id, username: rows[0].is_guest ? rows[0].user_id : "player",
      displayName: rows[0].display_name, isGuest: rows[0].is_guest,
    };
  }

  async getPlayer(userId) {
    const { rows } = await this.db.query("SELECT * FROM players WHERE user_id = $1", [userId]);
    return playerData(rows[0]);
  }

  async savePlayer(userId, fields) {
    const { rows } = await this.db.query(
      `UPDATE players SET
        level = COALESCE($2, level), xp = COALESCE($3, xp), coins = COALESCE($4, coins),
        selected_weapon = COALESCE($5, selected_weapon), updated_at = NOW()
       WHERE user_id = $1 RETURNING *`,
      [userId, fields.level ?? null, fields.xp ?? null, fields.coins ?? null, fields.selectedWeapon ?? null],
    );
    return playerData(rows[0]);
  }

  async saveScore(userId, score) {
    // One SQL update prevents retries or concurrent requests lowering a record.
    const { rows } = await this.db.query(
      `UPDATE players SET
         best_score_at = CASE WHEN $2 > best_score THEN NOW() ELSE best_score_at END,
         best_score = GREATEST(best_score, $2), updated_at = NOW()
       WHERE user_id = $1 RETURNING *`,
      [userId, score],
    );
    return playerData(rows[0]);
  }

  async leaderboard() {
    const { rows } = await this.db.query(
      `SELECT display_name, is_guest, best_score FROM players WHERE best_score > 0
       ORDER BY best_score DESC, best_score_at ASC, user_id ASC LIMIT 10`,
    );
    return rows.map((row, index) => ({
      rank: index + 1, displayName: row.display_name, bestScore: row.best_score, isGuest: row.is_guest,
    }));
  }
}

module.exports = { PlayerStore };
