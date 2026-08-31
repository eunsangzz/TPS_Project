const { randomBytes, scrypt, timingSafeEqual } = require("node:crypto");
const { promisify } = require("node:util");

const derive = promisify(scrypt);
const options = { N: 32768, r: 8, p: 3, maxmem: 64 * 1024 * 1024 };

async function hashPassword(password) {
  const salt = randomBytes(16).toString("hex");
  const key = await derive(password, salt, 64, options);
  return `scrypt-v1$${salt}$${key.toString("hex")}`;
}

async function verifyPassword(password, hash) {
  const [version, salt, encoded] = (hash || "").split("$");
  if (version !== "scrypt-v1" || !/^[a-f0-9]{32}$/.test(salt) || !/^[a-f0-9]{128}$/.test(encoded)) return false;
  const key = await derive(password, salt, 64, options);
  return timingSafeEqual(key, Buffer.from(encoded, "hex"));
}

module.exports = { hashPassword, verifyPassword };
