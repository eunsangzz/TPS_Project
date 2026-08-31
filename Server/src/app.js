const express = require("express");
const crypto = require("node:crypto");

function isInteger(value, minimum = 0) {
  return Number.isInteger(value) && value >= minimum && value <= 2147483647;
}

function createApp(store) {
  const app = express();
  app.disable("x-powered-by");
  app.use(express.json({ limit: "8kb" }));
  app.use((req, res, next) => { res.set("Cache-Control", "no-store"); next(); });

  async function requireAuth(req, res, next) {
    const header = req.get("Authorization") || "";
    const token = header.startsWith("Bearer ") ? header.slice(7) : "";
    req.user = token && token.length <= 256 ? await store.authenticate(token) : null;
    if (!req.user) return res.status(401).json({ error: "invalid or expired token" });
    next();
  }

  app.get("/ping", async (req, res) => {
    await store.db.query("SELECT 1");
    res.json({ message: "pong", storage: "postgresql", serverTime: new Date().toISOString() });
  });
  app.post("/auth/login", async (req, res) => {
    if (typeof req.body?.username !== "string" || typeof req.body?.password !== "string") {
      return res.status(400).json({ error: "username and password are required" });
    }
    if (req.body.username.trim() !== "player" || req.body.password !== (process.env.DEMO_PASSWORD || "1234")) {
      return res.status(401).json({ error: "invalid username or password" });
    }
    res.json(await store.createSession({ id: "user_demo", displayName: "Player", isGuest: false }));
  });
  app.post("/auth/guest", async (req, res) => {
    const resumeToken = req.body?.resumeToken;
    if (typeof resumeToken === "string" && resumeToken.length <= 256 && resumeToken) {
      const user = await store.authenticate(resumeToken);
      if (user?.isGuest) return res.json({ token: resumeToken, user });
      return res.status(401).json({ error: "guest session expired; create a new guest to continue" });
    }
    const name = req.body?.displayName;
    if (typeof name !== "string" || !name.trim() || name.trim().length > 32 || /[<>\x00-\x1f]/.test(name)) {
      return res.status(400).json({ error: "guest name must be 1-32 characters without markup or control characters" });
    }
    res.json(await store.createSession({ id: `guest_${crypto.randomUUID()}`, displayName: name.trim(), isGuest: true }));
  });
  app.get("/auth/me", requireAuth, (req, res) => {
    res.json({ token: req.get("Authorization").slice(7), user: req.user });
  });
  app.get("/player-data", requireAuth, async (req, res) => {
    res.json({ playerData: await store.getPlayer(req.user.id) });
  });
  app.put("/player-data", requireAuth, async (req, res) => {
    const body = req.body || {};
    for (const field of ["level", "xp", "coins"]) {
      if (body[field] !== undefined && !isInteger(body[field], field === "level" ? 1 : 0)) {
        return res.status(400).json({ error: `invalid ${field}` });
      }
    }
    if (body.selectedWeapon !== undefined &&
        (typeof body.selectedWeapon !== "string" || !body.selectedWeapon.trim() || body.selectedWeapon.length > 64)) {
      return res.status(400).json({ error: "invalid selectedWeapon" });
    }
    res.json({ playerData: await store.savePlayer(req.user.id, body) });
  });
  app.post("/scores", requireAuth, async (req, res) => {
    if (!isInteger(req.body?.score)) return res.status(400).json({ error: "score must be a non-negative 32-bit integer" });
    res.json({ playerData: await store.saveScore(req.user.id, req.body.score) });
  });
  app.get("/leaderboard", async (req, res) => {
    res.json({ entries: await store.leaderboard() });
  });
  app.use((error, req, res, next) => {
    if (error.type === "entity.parse.failed") return res.status(400).json({ error: "invalid JSON" });
    if (error.type === "entity.too.large") return res.status(413).json({ error: "request too large" });
    console.error("Request failed:", error.code || "database error");
    res.status(503).json({ error: "database unavailable; please retry" });
  });
  return app;
}

module.exports = { createApp };
