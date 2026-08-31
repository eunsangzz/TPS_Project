const { test } = require("node:test");
const assert = require("node:assert/strict");
const { mkdtemp, rm } = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const { PGlite } = require("@electric-sql/pglite");
const { PlayerStore } = require("../src/player-store");
const { createApp } = require("../src/app");

test("PostgreSQL-backed API", async (t) => {
  const directory = await mkdtemp(path.join(os.tmpdir(), "tps-db-test-"));
  let db;
  let server;
  let baseUrl;
  async function start() {
    db = new PGlite(directory);
    // PGlite uses the PostgreSQL engine but requires exec for multi-statement DDL.
    const adapter = { query: (sql, args) => sql.includes("CREATE TABLE") ? db.exec(sql) : db.query(sql, args) };
    const store = new PlayerStore(adapter);
    await store.initialize();
    server = createApp(store).listen(0, "127.0.0.1");
    await new Promise((resolve) => server.once("listening", resolve));
    baseUrl = `http://127.0.0.1:${server.address().port}`;
  }
  async function stop() {
    await new Promise((resolve) => server.close(resolve));
    await db.close();
  }
  t.after(async () => { await stop(); await rm(directory, { recursive: true, force: true }); });
  await start();
  async function request(route, { method = "GET", token, body } = {}) {
    const response = await fetch(baseUrl + route, {
      method,
      headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    return { status: response.status, body: await response.json() };
  }
  let token;
  let guest;

  await t.test("login, database health, validation and authentication", async () => {
    assert.equal((await request("/ping")).body.storage, "postgresql");
    assert.deepEqual((await request("/leaderboard")).body.entries, []);
    assert.equal((await request("/scores", { method: "POST", body: { score: 10 } })).status, 401);
    assert.equal((await request("/auth/login", { method: "POST", body: { username: {}, password: "x" } })).status, 400);
    assert.equal((await request("/auth/login", { method: "POST", body: { username: "player", password: "wrong" } })).status, 401);
    const login = await request("/auth/login", { method: "POST", body: { username: "player", password: process.env.DEMO_PASSWORD || "1234" } });
    assert.equal(login.status, 200);
    token = login.body.token;
    const data = (await request("/player-data", { token })).body.playerData;
    assert.equal(data.bestScore, 0);
    assert.equal(data.coins, 100);
    for (const score of [-1, 1.5, "100", null, 2147483648]) {
      assert.equal((await request("/scores", { method: "POST", token, body: { score } })).status, 400);
    }
  });

  await t.test("higher score wins across retries, concurrent saves and profile updates", async () => {
    const saved = await request("/scores", { method: "POST", token, body: { score: 500 } });
    assert.equal(saved.body.playerData.bestScore, 500);
    const results = await Promise.all([300, 1000, 500, 1000, 0].map((score) => request("/scores", { method: "POST", token, body: { score } })));
    assert.ok(results.every((result) => result.status === 200));
    const profile = await request("/player-data", { method: "PUT", token, body: { coins: 220, level: 3, bestScore: 1, userId: "someone_else" } });
    assert.equal(profile.body.playerData.bestScore, 1000);
    assert.equal(profile.body.playerData.coins, 220);
    assert.equal(profile.body.playerData.userId, "user_demo");
    assert.equal((await request("/player-data", { method: "PUT", token, body: { coins: -1 } })).status, 400);
  });

  await t.test("guest identity resumes, scores remain isolated, leaderboard is public", async () => {
    guest = (await request("/auth/guest", { method: "POST", body: { displayName: "Tester" } })).body;
    await request("/scores", { method: "POST", token: guest.token, body: { score: 2000, userId: "user_demo" } });
    const resumed = await request("/auth/guest", { method: "POST", body: { resumeToken: guest.token } });
    assert.equal(resumed.body.user.id, guest.user.id);
    assert.equal((await request("/player-data", { token })).body.playerData.bestScore, 1000);
    const entries = (await request("/leaderboard")).body.entries;
    assert.deepEqual(entries.map((entry) => entry.bestScore), [2000, 1000]);
    assert.deepEqual(entries.map((entry) => entry.rank), [1, 2]);
    assert.ok(entries.every((entry) => !entry.userId && !entry.token));
    assert.equal((await request("/auth/guest", { method: "POST", body: { resumeToken: "expired", displayName: "Tester" } })).status, 401);
    assert.equal((await request("/auth/guest", { method: "POST", body: { displayName: "<b>Fake</b>" } })).status, 400);
  });

  await t.test("scores, profile and sessions survive closing and reopening database/server", async () => {
    await stop();
    await start();
    const data = (await request("/player-data", { token })).body.playerData;
    assert.equal(data.bestScore, 1000);
    assert.equal(data.coins, 220);
    assert.equal(data.level, 3);
    assert.equal((await request("/player-data", { token: guest.token })).body.playerData.bestScore, 2000);
    assert.equal((await request("/leaderboard")).body.entries[0].bestScore, 2000);
  });

  await t.test("top ten is bounded, ties are deterministic and zero scores are excluded", async () => {
    for (let i = 0; i < 12; i++) {
      const account = (await request("/auth/guest", { method: "POST", body: { displayName: `Guest ${i}` } })).body;
      await request("/scores", { method: "POST", token: account.token, body: { score: 100 } });
    }
    const first = (await request("/leaderboard")).body.entries;
    assert.equal(first.length, 10);
    assert.deepEqual(first, (await request("/leaderboard")).body.entries);
    assert.equal(first[0].displayName, "Tester");
    assert.equal(first[9].rank, 10);
  });

  await t.test("expired session cannot read or write scores", async () => {
    await db.query("UPDATE sessions SET expires_at = NOW() - INTERVAL '1 day'");
    assert.equal((await request("/player-data", { token })).status, 401);
    assert.equal((await request("/scores", { method: "POST", token, body: { score: 9999 } })).status, 401);
    assert.equal((await request("/leaderboard")).status, 200);
  });
});
