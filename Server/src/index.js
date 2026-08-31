const express = require("express");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const app = express();
const port = process.env.PORT || 3000;

app.use(express.json());

const users = new Map();
const sessions = new Map();
const playerDataByUserId = new Map();
const dataDirectory = path.join(__dirname, "..", "data");
const playerDataFile = path.join(dataDirectory, "player-data.json");

const demoUser = {
  id: "user_demo",
  username: "player",
  password: "1234",
  displayName: "Player",
  isGuest: false,
};

users.set(demoUser.username, demoUser);

loadPlayerData();

function createSession(user) {
  const token = crypto.randomUUID();
  const session = {
    token,
    userId: user.id,
    username: user.username,
    displayName: user.displayName,
    isGuest: user.isGuest,
    createdAt: new Date().toISOString(),
  };

  sessions.set(token, session);
  ensurePlayerData(session);
  return session;
}

function ensurePlayerData(session) {
  if (playerDataByUserId.has(session.userId)) {
    return playerDataByUserId.get(session.userId);
  }

  const playerData = {
    userId: session.userId,
    displayName: session.displayName,
    level: 1,
    xp: 0,
    coins: session.isGuest ? 0 : 100,
    selectedWeapon: "Rifle",
    lastLoginAt: new Date().toISOString(),
  };

  playerDataByUserId.set(session.userId, playerData);
  savePlayerData();
  return playerData;
}

function loadPlayerData() {
  if (!fs.existsSync(playerDataFile)) {
    return;
  }

  const raw = fs.readFileSync(playerDataFile, "utf8");
  const records = JSON.parse(raw);

  for (const record of records) {
    if (record.userId) {
      playerDataByUserId.set(record.userId, record);
    }
  }
}

function savePlayerData() {
  fs.mkdirSync(dataDirectory, { recursive: true });
  const records = Array.from(playerDataByUserId.values());
  fs.writeFileSync(playerDataFile, JSON.stringify(records, null, 2), "utf8");
}

function authenticate(req, res) {
  const authHeader = req.header("Authorization") || "";
  const token = authHeader.startsWith("Bearer ") ? authHeader.slice(7) : "";
  const session = sessions.get(token);

  if (!session) {
    res.status(401).json({ error: "invalid or expired token" });
    return null;
  }

  return session;
}

function sendAuthResponse(res, session) {
  res.json({
    token: session.token,
    user: {
      id: session.userId,
      username: session.username,
      displayName: session.displayName,
      isGuest: session.isGuest,
    },
  });
}

app.get("/ping", (req, res) => {
  res.json({
    message: "pong",
    serverTime: new Date().toISOString(),
  });
});

app.post("/auth/guest", (req, res) => {
  const displayName = req.body?.displayName?.trim() || "Guest";
  const guestId = `guest_${crypto.randomUUID()}`;
  const guestUser = {
    id: guestId,
    username: guestId,
    displayName,
    isGuest: true,
  };

  const session = createSession(guestUser);
  sendAuthResponse(res, session);
});

app.post("/auth/login", (req, res) => {
  const username = req.body?.username?.trim();
  const password = req.body?.password;

  if (!username || !password) {
    res.status(400).json({ error: "username and password are required" });
    return;
  }

  const user = users.get(username);
  if (!user || user.password !== password) {
    res.status(401).json({ error: "invalid username or password" });
    return;
  }

  const session = createSession(user);
  sendAuthResponse(res, session);
});

app.get("/auth/me", (req, res) => {
  const session = authenticate(req, res);
  if (!session) {
    return;
  }

  sendAuthResponse(res, session);
});

app.get("/player-data", (req, res) => {
  const session = authenticate(req, res);
  if (!session) {
    return;
  }

  const playerData = ensurePlayerData(session);
  res.json({ playerData });
});

app.put("/player-data", (req, res) => {
  const session = authenticate(req, res);
  if (!session) {
    return;
  }

  const current = ensurePlayerData(session);
  const body = req.body || {};
  const next = {
    ...current,
    displayName: typeof body.displayName === "string" ? body.displayName.trim() || current.displayName : current.displayName,
    level: Number.isInteger(body.level) ? Math.max(1, body.level) : current.level,
    xp: Number.isInteger(body.xp) ? Math.max(0, body.xp) : current.xp,
    coins: Number.isInteger(body.coins) ? Math.max(0, body.coins) : current.coins,
    selectedWeapon: typeof body.selectedWeapon === "string" ? body.selectedWeapon.trim() || current.selectedWeapon : current.selectedWeapon,
    updatedAt: new Date().toISOString(),
  };

  playerDataByUserId.set(session.userId, next);
  savePlayerData();
  res.json({ playerData: next });
});

app.listen(port, () => {
  console.log(`TPS server is running at http://localhost:${port}`);
});
