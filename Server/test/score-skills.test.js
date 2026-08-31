const { test } = require("node:test");
const assert = require("node:assert/strict");
const { PGlite } = require("@electric-sql/pglite");
const { PlayerStore } = require("../src/player-store");
const { createApp } = require("../src/app");

test("score builds migrate and remain paired with their run", async (t) => {
  const db = new PGlite();
  await db.exec(`CREATE TABLE players (
    user_id TEXT PRIMARY KEY, display_name VARCHAR(32) NOT NULL, is_guest BOOLEAN NOT NULL DEFAULT FALSE,
    level INTEGER NOT NULL DEFAULT 1, xp INTEGER NOT NULL DEFAULT 0, coins INTEGER NOT NULL DEFAULT 0,
    selected_weapon VARCHAR(64) NOT NULL DEFAULT 'Rifle', best_score INTEGER NOT NULL DEFAULT 0,
    best_score_at TIMESTAMPTZ, last_login_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
  ); INSERT INTO players (user_id, display_name, best_score) VALUES ('legacy', 'Legacy', 123);`);
  const store = new PlayerStore({ query: (sql, args) => sql.includes("CREATE TABLE") ? db.exec(sql) : db.query(sql, args) });
  await store.initialize();
  await store.initialize();
  assert.equal((await store.getPlayer("legacy")).bestScore, 123);
  assert.deepEqual((await store.getPlayer("legacy")).bestSkills, []);
  const server = createApp(store).listen(0, "127.0.0.1");
  await new Promise((resolve) => server.once("listening", resolve));
  t.after(async () => { await new Promise((resolve) => server.close(resolve)); await db.close(); });
  const base = `http://127.0.0.1:${server.address().port}`;
  const session = await store.createSession({ id: "build_tester", displayName: "Build tester", isGuest: true });
  const runA = "a".repeat(32);
  const runB = "b".repeat(32);
  const runC = "c".repeat(32);
  async function save(body) {
    const response = await fetch(base + "/scores", { method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${session.token}` }, body: JSON.stringify(body) });
    return { status: response.status, body: await response.json() };
  }
  const first = [{ id: "PowerRounds", level: 1 }];
  const richer = [{ id: "HeavyStrike", level: 1 }, { id: "PowerRounds", level: 2 }];
  assert.deepEqual((await save({ score: 500, runId: runA, skills: first })).body.playerData.bestSkills, first);
  const timestamp = (await db.query("SELECT best_score_at FROM players WHERE user_id = 'build_tester'")).rows[0].best_score_at;
  assert.deepEqual((await save({ score: 500, runId: runA, skills: richer })).body.playerData.bestSkills, richer);
  assert.deepEqual((await db.query("SELECT best_score_at FROM players WHERE user_id = 'build_tester'")).rows[0].best_score_at, timestamp);
  for (const body of [
    { score: 500, runId: runA, skills: first },
    { score: 500, runId: runB, skills: [{ id: "WideSwing", level: 3 }, { id: "Toughness", level: 3 }] },
    { score: 400, runId: runA, skills: [{ id: "Toughness", level: 3 }] },
    { score: 500 },
  ]) assert.deepEqual((await save(body)).body.playerData.bestSkills, richer);

  const badSkills = [null, {}, [null], [{ id: "<script>", level: 1 }], [{ id: "PowerRounds", level: 4 }],
    [{ id: "PiercingRounds", level: 2 }], [{ id: "FirstAid", level: 1000000 }],
    [{ id: "HeavyStrike", level: "1" }], [{ id: "HeavyStrike", level: 0 }],
    [{ id: "HeavyStrike", level: 1.5 }], [first[0], first[0]], Array(10).fill(first[0])];
  for (const skills of badSkills) assert.equal((await save({ score: 1000, runId: runA, skills })).status, 400);
  for (const runId of [true, {}, "abc", "a".repeat(33)]) assert.equal((await save({ score: 1000, runId, skills: [] })).status, 400);
  assert.equal((await store.getPlayer("build_tester")).bestScore, 500);

  const contenders = [
    { score: 2000, runId: runA, skills: [{ id: "HeavyStrike", level: 2 }] },
    { score: 3000, runId: runB, skills: [{ id: "PowerRounds", level: 3 }] },
    { score: 3000, runId: runC, skills: [{ id: "WideSwing", level: 3 }] },
  ];
  await Promise.all(contenders.map(save));
  const best = await store.getPlayer("build_tester");
  assert.equal(best.bestScore, 3000);
  assert.deepEqual(best.bestSkills, contenders.find((value) => value.runId === best.bestRunId).skills);
  const entries = await (await fetch(base + "/leaderboard")).json();
  assert.deepEqual(entries.entries[0].bestSkills, best.bestSkills);
  assert.equal(entries.entries[0].bestRunId, undefined);

  const bonus = [{ id: "FirstAid", level: 2 }, { id: "Supply", level: 1 }];
  assert.deepEqual((await save({ score: 4000, runId: runA, skills: bonus })).body.playerData.bestSkills, bonus);
  const oldClient = (await save({ score: 5000 })).body.playerData;
  assert.deepEqual(oldClient.bestSkills, []);
  assert.equal(oldClient.bestRunId, null);
  const legacyUnity = await save({ score: 5100, runId: "", skills: [] });
  assert.equal(legacyUnity.status, 200);
  assert.equal(legacyUnity.body.playerData.bestRunId, null);
});
