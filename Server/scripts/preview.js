const { PGlite } = require("@electric-sql/pglite");
const path = require("node:path");
const { PlayerStore } = require("../src/player-store");
const { createApp } = require("../src/app");

async function start() {
  if (process.env.RENDER || process.env.NODE_ENV === "production") throw new Error("Local preview only. Use npm start for production.");
  const db = new PGlite(path.join(__dirname, "../data/web-preview"));
  const store = new PlayerStore({ query: (sql, args) => sql.includes("CREATE TABLE") ? db.exec(sql) : db.query(sql, args) });
  await store.initialize();
  const server = createApp(store, { authLimit: 200 }).listen(process.env.PREVIEW_PORT || 3100, "127.0.0.1", () => {
    console.log(`LOCAL TEST DATABASE ONLY: http://127.0.0.1:${server.address().port}`);
  });
  for (const signal of ["SIGINT", "SIGTERM"]) process.once(signal, () => server.close(() => db.close().then(() => process.exit(0))));
}
start().catch((error) => { console.error(error.message); process.exitCode = 1; });
