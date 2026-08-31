const express = require("express");
const crypto = require("node:crypto");
const path = require("node:path");
const cookieParser = require("cookie-parser");
const helmet = require("helmet");
const { rateLimit } = require("express-rate-limit");
const { parseScoreSkills } = require("./score-skills");

function validName(value) {
  return typeof value === "string" && value.trim().length >= 1 && value.trim().length <= 32 && !/[<>\x00-\x1f\x7f]/.test(value);
}

function isInteger(value, minimum = 0) {
  return Number.isInteger(value) && value >= minimum && value <= 2147483647;
}

function createApp(store, { authLimit = 30 } = {}) {
  const app = express();
  app.disable("x-powered-by");
  app.set("trust proxy", 1);
  app.use(helmet({ contentSecurityPolicy: { directives: { "upgrade-insecure-requests": null } } }));
  app.use(cookieParser());
  app.use(express.json({ limit: "8kb" }));
  app.use((req, res, next) => { res.set("Cache-Control", "no-store"); next(); });
  const authLimiter = rateLimit({ windowMs: 15 * 60 * 1000, limit: authLimit,
    standardHeaders: "draft-7", legacyHeaders: false,
    message: { error: "too many authentication attempts", code: "RATE_LIMITED" } });
  app.use(["/auth/login", "/auth/register", "/auth/guest", "/web/auth/login", "/web/auth/register"], authLimiter);

  async function login(req, res) {
    if (typeof req.body?.username !== "string" || typeof req.body?.password !== "string" ||
        req.body.username.length > 24 || req.body.password.length > 128 || !req.body.password) {
      res.status(400).json({ error: "invalid credentials format", code: "INVALID_CREDENTIALS" });
      return null;
    }
    const session = await store.login(req.body.username.trim().toLowerCase(), req.body.password);
    if (!session) res.status(401).json({ error: "invalid username or password", code: "INVALID_CREDENTIALS" });
    return session;
  }

  async function register(req, res) {
    const { username, password, displayName } = req.body || {};
    const normalized = typeof username === "string" ? username.trim().toLowerCase() : "";
    if (!/^[a-z0-9_]{3,24}$/.test(normalized)) {
      res.status(400).json({ error: "username must be 3-24 letters, digits or underscores", code: "INVALID_USERNAME" });
      return null;
    }
    if (typeof password !== "string" || password.length < 12 || password.length > 128) {
      res.status(400).json({ error: "password must be 12-128 characters", code: "INVALID_PASSWORD" });
      return null;
    }
    if (!validName(displayName)) {
      res.status(400).json({ error: "display name must be 1-32 characters without markup", code: "INVALID_NAME" });
      return null;
    }
    if (normalized === "player") {
      res.status(409).json({ error: "username already in use", code: "USERNAME_TAKEN" });
      return null;
    }
    try {
      return await store.register(normalized, password, displayName.trim());
    } catch (error) {
      if (error.code !== "23505") throw error;
      res.status(409).json({ error: "username already in use", code: "USERNAME_TAKEN" });
      return null;
    }
  }

  const cookieName = "tps_session";
  function cookieOptions(req) {
    return { httpOnly: true, secure: req.secure || process.env.RENDER === "true", sameSite: "strict", path: "/" };
  }
  function requireSameOrigin(req, res, next) {
    if (!req.is("application/json") || req.get("origin") !== `${req.protocol}://${req.get("host")}`) {
      return res.status(403).json({ error: "same-origin JSON request required", code: "INVALID_ORIGIN" });
    }
    next();
  }
  async function requireWebAuth(req, res, next) {
    const token = req.cookies[cookieName];
    req.user = typeof token === "string" && token.length <= 256 ? await store.authenticate(token) : null;
    if (!req.user) return res.status(401).json({ error: "session expired", code: "SESSION_EXPIRED" });
    next();
  }
  for (const [route, handler] of [["login", login], ["register", register]]) {
    app.post(`/web/auth/${route}`, requireSameOrigin, async (req, res) => {
      const session = await handler(req, res);
      if (!session) return;
      res.cookie(cookieName, session.token, { ...cookieOptions(req), maxAge: 30 * 24 * 60 * 60 * 1000 });
      res.status(route === "register" ? 201 : 200).json({ user: session.user });
    });
  }
  app.get("/web/me", requireWebAuth, async (req, res) => {
    res.json({ user: req.user, playerData: await store.getPlayer(req.user.id), rank: await store.getRank(req.user.id) });
  });
  app.post("/web/auth/logout", requireSameOrigin, async (req, res) => {
    const token = req.cookies[cookieName];
    if (typeof token === "string" && token.length <= 256) await store.revokeSession(token);
    res.clearCookie(cookieName, cookieOptions(req));
    res.json({ success: true });
  });

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
    const session = await login(req, res);
    if (session) res.json(session);
  });
  app.post("/auth/register", async (req, res) => {
    const session = await register(req, res);
    if (session) res.status(201).json(session);
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
    const skills = parseScoreSkills(req.body.skills);
    // Unity JsonUtility serializes an unset legacy run ID as an empty string.
    const runId = req.body.runId === "" ? null : req.body.runId ?? null;
    if (skills === null || (runId !== null && (typeof runId !== "string" || !/^[a-f0-9]{32}$/.test(runId)))) {
      return res.status(400).json({ error: "invalid skill records or runId" });
    }
    res.json({ playerData: await store.saveScore(req.user.id, req.body.score, skills, runId) });
  });
  app.get("/leaderboard", async (req, res) => {
    res.json({ entries: await store.leaderboard() });
  });
  app.get("/assets/lucide.js", (req, res) => res.sendFile(require.resolve("lucide/dist/umd/lucide.js")));
  app.use(express.static(path.join(__dirname, "..", "public"), { etag: false }));
  app.use((error, req, res, next) => {
    if (error.type === "entity.parse.failed") return res.status(400).json({ error: "invalid JSON" });
    if (error.type === "entity.too.large") return res.status(413).json({ error: "request too large" });
    console.error("Request failed:", error.code || "database error");
    res.status(503).json({ error: "database unavailable; please retry" });
  });
  return app;
}

module.exports = { createApp };
