const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const path = require("node:path");
const { hashPassword, verifyPassword } = require("./passwords");

function tokenHash(token) {
  return crypto.createHash("sha256").update(token).digest("hex");
}

function playerData(row) {
  return {
    userId: row.user_id, displayName: row.display_name,
    level: row.level, xp: row.xp, coins: row.coins, selectedWeapon: row.selected_weapon,
    bestScore: row.best_score, bestSkills: row.best_skills || [], bestRunId: row.best_run_id,
    lastLoginAt: row.last_login_at, updatedAt: row.updated_at,
  };
}

class PlayerStore {
  constructor(db) { this.db = db; }

  async initialize() {
    await this.db.query(await fs.readFile(path.join(__dirname, "schema.sql"), "utf8"));
    await this.db.query("DELETE FROM sessions WHERE expires_at <= NOW()");
    this.dummyPasswordHash = await hashPassword(crypto.randomBytes(32).toString("hex"));
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
      `SELECT p.user_id, p.display_name, p.is_guest, a.username FROM sessions s
       JOIN players p ON p.user_id = s.user_id
       LEFT JOIN accounts a ON a.user_id = p.user_id
       WHERE s.token_hash = $1 AND s.expires_at > NOW()`,
      [tokenHash(token)],
    );
    if (!rows[0]) return null;
    return {
      id: rows[0].user_id, username: rows[0].username || (rows[0].is_guest ? rows[0].user_id : "player"),
      displayName: rows[0].display_name, isGuest: rows[0].is_guest,
    };
  }

  async register(username, password, displayName) {
    const passwordHash = await hashPassword(password);
    const id = `user_${crypto.randomUUID()}`;
    // A single statement rolls back the player insert if a concurrent signup wins.
    await this.db.query(
      `WITH new_player AS (
         INSERT INTO players (user_id, display_name, is_guest, coins)
         VALUES ($1, $2, FALSE, 100) RETURNING user_id
       ) INSERT INTO accounts (username, user_id, password_hash)
         SELECT $3, user_id, $4 FROM new_player`,
      [id, displayName, username, passwordHash],
    );
    return this.createSession({ id, displayName, isGuest: false });
  }

  async login(username, password) {
    if (username === "player") {
      const expected = crypto.createHash("sha256").update(process.env.DEMO_PASSWORD || "1234").digest();
      const supplied = crypto.createHash("sha256").update(password).digest();
      if (!crypto.timingSafeEqual(expected, supplied)) return null;
      return this.createSession({ id: "user_demo", displayName: "Player", isGuest: false });
    }
    const { rows } = await this.db.query(
      "SELECT a.password_hash, p.user_id, p.display_name FROM accounts a JOIN players p ON p.user_id = a.user_id WHERE a.username = $1",
      [username],
    );
    const matches = await verifyPassword(password, rows[0]?.password_hash || this.dummyPasswordHash);
    if (!rows[0] || !matches) return null;
    return this.createSession({ id: rows[0].user_id, displayName: rows[0].display_name, isGuest: false });
  }

  async revokeSession(token) {
    await this.db.query("DELETE FROM sessions WHERE token_hash = $1", [tokenHash(token)]);
  }

  async getRank(userId) {
    const { rows } = await this.db.query(
      `SELECT rank FROM (
         SELECT user_id, ROW_NUMBER() OVER (ORDER BY best_score DESC, best_score_at ASC, user_id ASC) AS rank
         FROM players WHERE best_score > 0
       ) ranked WHERE user_id = $1`, [userId],
    );
    return rows[0] ? Number(rows[0].rank) : null;
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

  async saveScore(userId, score, skills = [], runId = null) {
    // Keep the score and its build atomic. Only newer choices in that SAME run
    // may enrich a tied record; another run or stale retry cannot replace it.
    const revision = skills.reduce((total, skill) => total + skill.level, 0);
    const { rows } = await this.db.query(
      `UPDATE players SET
         best_score_at = CASE WHEN $2 > best_score THEN NOW() ELSE best_score_at END,
         best_skills = CASE WHEN $2 > best_score OR
           ($2 = best_score AND $4::text IS NOT NULL AND best_run_id = $4::text AND $5 > best_skill_revision)
           THEN $3::jsonb ELSE best_skills END,
         best_skill_revision = CASE WHEN $2 > best_score OR
           ($2 = best_score AND $4::text IS NOT NULL AND best_run_id = $4::text AND $5 > best_skill_revision)
           THEN $5 ELSE best_skill_revision END,
         best_run_id = CASE WHEN $2 > best_score THEN $4::text ELSE best_run_id END,
         best_score = GREATEST(best_score, $2), updated_at = NOW()
       WHERE user_id = $1 RETURNING *`,
      [userId, score, JSON.stringify(skills), runId, revision],
    );
    return playerData(rows[0]);
  }

  async leaderboard() {
    const { rows } = await this.db.query(
      `SELECT display_name, is_guest, best_score, best_skills FROM players WHERE best_score > 0
       ORDER BY best_score DESC, best_score_at ASC, user_id ASC LIMIT 10`,
    );
    return rows.map((row, index) => ({
      rank: index + 1, displayName: row.display_name, bestScore: row.best_score, isGuest: row.is_guest,
      bestSkills: row.best_skills || [],
    }));
  }
}

module.exports = { PlayerStore };
