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
    server = createApp(store, { authLimit: 200 }).listen(0, "127.0.0.1");
    await new Promise((resolve) => server.once("listening", resolve));
    baseUrl = `http://127.0.0.1:${server.address().port}`;
  }
  async function stop() {
    await new Promise((resolve) => server.close(resolve));
    await db.close();
  }
  t.after(async () => { await stop(); await rm(directory, { recursive: true, force: true }); });
  await start();
  async function request(route, { method = "GET", token, body, headers = {} } = {}) {
    const response = await fetch(baseUrl + route, {
      method,
      headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    return { status: response.status, body: await response.json(), headers: response.headers };
  }
  let token;
  let guest;
  let memberToken;
  let webCookie;
  const memberPassword = "Test-only password 482!";

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
    await request("/scores", { method: "POST", token: guest.token, body: { score: 2000, userId: "user_demo", runId: "d".repeat(32), skills: [{ id: "HeavyStrike", level: 2 }] } });
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

  await t.test("web registration stores a password hash and shares scores with Unity login", async () => {
    const registration = await request("/web/auth/register", { method: "POST", headers: { Origin: baseUrl },
      body: { username: "Member_01", password: memberPassword, displayName: "Member" } });
    assert.equal(registration.status, 201);
    assert.equal(registration.body.user.username, "member_01");
    assert.equal(registration.body.token, undefined);
    const cookie = registration.headers.get("set-cookie");
    assert.match(cookie, /HttpOnly/i);
    assert.match(cookie, /SameSite=Strict/i);
    assert.match(cookie, /Path=\/(?:;|$)/i);
    webCookie = cookie.split(";")[0];
    const account = (await db.query("SELECT * FROM accounts WHERE username = $1", ["member_01"])).rows[0];
    assert.notEqual(account.password_hash, memberPassword);
    assert.match(account.password_hash, /^scrypt-v1\$/);
    const login = await request("/auth/login", { method: "POST", body: { username: "MEMBER_01", password: memberPassword } });
    assert.equal(login.status, 200);
    assert.equal(login.body.user.id, registration.body.user.id);
    memberToken = login.body.token;
    await request("/scores", { method: "POST", token: memberToken, body: { score: 350 } });
    const profile = await request("/web/me", { headers: { Cookie: webCookie } });
    assert.equal(profile.body.playerData.bestScore, 350);
    assert.equal(profile.body.rank, 3);
    assert.equal(profile.body.user.username, "member_01");
    assert.equal(JSON.stringify(profile.body).includes("password_hash"), false);
    assert.equal(JSON.stringify(profile.body).includes("token"), false);
    assert.equal((await request("/player-data", { token })).body.playerData.bestScore, 1000);
  });

  await t.test("registration rejects invalid inputs and concurrent duplicates atomically", async () => {
    const body = { username: "member_01", password: memberPassword, displayName: "Member" };
    for (const [patch, status] of [[{ username: "player" }, 409], [{ username: "x" }, 400], [{ password: "1234" }, 400], [{ displayName: "<b>user</b>" }, 400], [{}, 409]]) {
      assert.equal((await request("/auth/register", { method: "POST", body: { ...body, ...patch } })).status, status);
    }
    const race = await Promise.all([1, 2].map(() => request("/auth/register", { method: "POST", body: { ...body, username: "race_user", displayName: "Race" } })));
    assert.deepEqual(race.map((r) => r.status).sort(), [201, 409]);
    assert.equal(Number((await db.query("SELECT COUNT(*) AS count FROM players WHERE display_name = 'Race'")).rows[0].count), 1);
    for (const username of ["member_01", "does_not_exist"]) {
      const wrong = await request("/auth/login", { method: "POST", body: { username, password: "wrong-password" } });
      assert.equal(wrong.status, 401);
      assert.equal(wrong.body.code, "INVALID_CREDENTIALS");
    }
  });

  await t.test("web cookie auth blocks cross-origin writes and logout revokes only its session", async () => {
    assert.equal((await request("/web/me")).status, 401);
    assert.equal((await request("/auth/me", { headers: { Cookie: webCookie } })).status, 401);
    assert.equal((await request("/web/auth/logout", { method: "POST", headers: { Cookie: webCookie, Origin: "https://untrusted.example" }, body: {} })).status, 403);
    assert.equal((await request("/web/me", { headers: { Cookie: webCookie } })).status, 200);
    const out = await request("/web/auth/logout", { method: "POST", headers: { Cookie: webCookie, Origin: baseUrl }, body: {} });
    assert.equal(out.status, 200);
    assert.equal((await request("/web/me", { headers: { Cookie: webCookie } })).status, 401);
    assert.equal((await request("/player-data", { token: memberToken })).status, 200);
    const login = await request("/web/auth/login", { method: "POST", headers: { Origin: baseUrl }, body: { username: "member_01", password: memberPassword } });
    assert.equal(login.status, 200);
    webCookie = login.headers.get("set-cookie").split(";")[0];
    const secure = await request("/web/auth/login", { method: "POST", headers: { Origin: baseUrl.replace("http:", "https:"), "X-Forwarded-Proto": "https" }, body: { username: "member_01", password: memberPassword } });
    assert.match(secure.headers.get("set-cookie"), /; Secure/i);
  });

  await t.test("scores, accounts, profile and sessions survive closing and reopening database/server", async () => {
    await stop();
    await start();
    const data = (await request("/player-data", { token })).body.playerData;
    assert.equal(data.bestScore, 1000);
    assert.equal(data.coins, 220);
    assert.equal(data.level, 3);
    assert.equal((await request("/player-data", { token: guest.token })).body.playerData.bestScore, 2000);
    assert.equal((await request("/leaderboard")).body.entries[0].bestScore, 2000);
    assert.deepEqual((await request("/leaderboard")).body.entries[0].bestSkills, [{ id: "HeavyStrike", level: 2 }]);
    assert.equal((await request("/web/me", { headers: { Cookie: webCookie } })).body.playerData.bestScore, 350);
    const login = await request("/auth/login", { method: "POST", body: { username: "member_01", password: memberPassword } });
    assert.equal(login.status, 200);
    assert.equal((await request("/player-data", { token: login.body.token })).body.playerData.bestScore, 350);
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
    assert.equal((await request("/web/me", { headers: { Cookie: webCookie } })).status, 401);
  });

  await t.test("portal is served with CSP and bounded authentication requests", async () => {
    const page = await fetch(baseUrl);
    assert.equal(page.status, 200);
    assert.match(page.headers.get("content-security-policy"), /script-src 'self'/);
    assert.match(await page.text(), /TPS Project/);
    const icons = await fetch(baseUrl + "/assets/lucide.js");
    assert.equal(icons.status, 200);
    await icons.arrayBuffer();
    const limitedServer = createApp({}, { authLimit: 2 }).listen(0, "127.0.0.1");
    await new Promise((resolve) => limitedServer.once("listening", resolve));
    try {
      const url = `http://127.0.0.1:${limitedServer.address().port}/auth/login`;
      const send = () => fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" });
      assert.equal((await send()).status, 400);
      assert.equal((await send()).status, 400);
      const blocked = await send();
      assert.equal(blocked.status, 429);
      assert.equal((await blocked.json()).code, "RATE_LIMITED");
    } finally { await new Promise((resolve) => limitedServer.close(resolve)); }
  });
});
