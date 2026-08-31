const { Pool } = require("pg");
const { PlayerStore } = require("./player-store");
const { createApp } = require("./app");

async function start() {
  if (!process.env.DATABASE_URL) {
    throw new Error("Set DATABASE_URL to your PostgreSQL connection URL. See Server/README.md.");
  }
  const pool = new Pool({
    connectionString: process.env.DATABASE_URL,
    max: 5, connectionTimeoutMillis: 10000, idleTimeoutMillis: 30000, statement_timeout: 15000,
  });
  pool.on("error", (error) => console.error("Database pool error:", error.code || "connection failed"));
  const store = new PlayerStore(pool);
  try {
    await store.initialize();
  } catch (error) {
    await pool.end();
    throw error;
  }
  const server = createApp(store).listen(process.env.PORT || 3000, "0.0.0.0", () => {
    console.log("TPS server ready. Storage: PostgreSQL");
  });
  for (const signal of ["SIGTERM", "SIGINT"]) {
    process.once(signal, () => {
      server.close(() => pool.end().then(() => process.exit(0)));
      setTimeout(() => process.exit(1), 10000).unref();
    });
  }
}

start().catch((error) => {
  // Connection errors can contain credentials; do not log the full error object.
  console.error(process.env.DATABASE_URL ? `Database startup failed (${error.code || "connection error"}).` : error.message);
  process.exitCode = 1;
});
