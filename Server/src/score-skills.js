const limits = new Map([
  ["PowerRounds", 3], ["PiercingRounds", 1], ["HeavyStrike", 3],
  ["WideSwing", 3], ["AmmoRecovery", 3], ["Toughness", 3],
  ["FirstAid", 999999], ["Vitality", 999999], ["Supply", 999999],
  ["RifleUnlock", 1], ["ShotgunUnlock", 1], ["SniperUnlock", 1],
  ["LifeSteal", 3], ["RifleUpgrade", 3], ["ShotgunUpgrade", 3], ["SniperUpgrade", 3],
]);

function parseScoreSkills(value = []) {
  if (!Array.isArray(value) || value.length > limits.size) return null;
  const seen = new Set();
  const skills = [];
  for (const item of value) {
    if (!item || typeof item !== "object" || !limits.has(item.id) || seen.has(item.id) ||
        !Number.isInteger(item.level) || item.level < 1 || item.level > limits.get(item.id)) return null;
    seen.add(item.id);
    skills.push({ id: item.id, level: item.level });
  }
  skills.sort((a, b) => a.id.localeCompare(b.id));
  return skills;
}

module.exports = { parseScoreSkills };
