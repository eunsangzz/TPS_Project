/* global lucide */
"use strict";

const element = (id) => document.getElementById(id);
const number = new Intl.NumberFormat("ko-KR");
let mode = "login";
let authBusy = false;
let sessionVersion = 0;
let refreshBusy = false;

const skillNames = {
  PowerRounds: "강화 탄환", PiercingRounds: "관통 탄환", HeavyStrike: "강타",
  WideSwing: "넓은 휘두르기", AmmoRecovery: "탄약 회수", Toughness: "강인함",
  FirstAid: "응급 처치", Vitality: "체력 보강", Supply: "보급 지원",
};

function renderSkills(container, records, collapsible = false) {
  const skills = Array.isArray(records) ? records.filter((skill) => skill && Object.hasOwn(skillNames, skill.id) && Number.isInteger(skill.level) && skill.level > 0) : [];
  container.replaceChildren();
  if (skills.length === 0) { container.textContent = "선택 기록 없음"; return; }
  let parent = container;
  if (collapsible) {
    parent = document.createElement("details");
    const summary = document.createElement("summary");
    summary.textContent = `선택 스킬 ${skills.length}종`;
    parent.append(summary);
    container.append(parent);
  }
  const list = document.createElement("ul");
  for (const skill of skills) {
    const item = document.createElement("li");
    const bonus = ["FirstAid", "Vitality", "Supply"].includes(skill.id);
    item.textContent = `${skillNames[skill.id]} ${bonus ? `${skill.level}회` : `Lv.${skill.level}`}`;
    list.append(item);
  }
  parent.append(list);
}

const errors = {
  INVALID_CREDENTIALS: "아이디 또는 비밀번호를 확인해주세요.",
  INVALID_USERNAME: "아이디는 영문·숫자·밑줄로 3~24자 입력해주세요.",
  INVALID_PASSWORD: "비밀번호는 12~128자여야 합니다.",
  INVALID_NAME: "닉네임은 특수 태그 없이 1~32자 입력해주세요.",
  USERNAME_TAKEN: "이미 사용 중인 아이디입니다.",
  RATE_LIMITED: "요청이 너무 많습니다. 잠시 후 다시 시도해주세요.",
  SESSION_EXPIRED: "로그인이 만료되었습니다. 다시 로그인해주세요.",
  INVALID_ORIGIN: "접속 주소를 확인하고 페이지를 새로고침해주세요.",
};

function message(id, text = "", error = false) {
  element(id).textContent = text;
  element(id).dataset.error = String(error);
}

async function api(path, body) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 90000);
  try {
    const response = await fetch(path, {
      method: body === undefined ? "GET" : "POST",
      credentials: "same-origin", cache: "no-store", signal: controller.signal,
      headers: { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      const error = new Error(errors[data.code] || "서버에 연결할 수 없습니다. 잠시 후 다시 시도해주세요.");
      error.status = response.status;
      throw error;
    }
    return data;
  } catch (error) {
    if (!error.status) throw new Error("서버 응답이 없습니다. 연결을 확인하고 다시 시도해주세요.");
    throw error;
  } finally { clearTimeout(timeout); }
}

function setMode(next) {
  if (authBusy) return;
  mode = next;
  const registering = mode === "register";
  for (const id of ["username-rule", "password-rule", "display-name-field", "confirm-field"]) element(id).hidden = !registering;
  for (const id of ["display-name", "confirm-password"]) {
    element(id).disabled = !registering;
    element(id).required = registering;
  }
  element("username").pattern = registering ? "[a-zA-Z0-9_]{3,24}" : ".*";
  element("password").minLength = registering ? 12 : 1;
  element("password").autocomplete = registering ? "new-password" : "current-password";
  element("password").value = "";
  element("confirm-password").value = "";
  element("confirm-password").setCustomValidity("");
  element("form-title").textContent = registering ? "새 계정 만들기" : "계정 로그인";
  element("submit-label").textContent = registering ? "회원가입" : "로그인";
  element("auth-form").setAttribute("aria-labelledby", mode + "-tab");
  for (const tab of ["login", "register"]) {
    element(tab + "-tab").setAttribute("aria-selected", String(tab === mode));
    element(tab + "-tab").tabIndex = tab === mode ? 0 : -1;
  }
  message("account-message");
}

function setAuthBusy(busy) {
  authBusy = busy;
  for (const node of document.querySelectorAll("#auth-form input, #auth-form button, .tabs button, #logout-button")) node.disabled = busy;
  if (!busy && mode === "login") {
    element("display-name").disabled = true;
    element("confirm-password").disabled = true;
  }
  element("submit-label").textContent = busy ? "처리 중..." : mode === "register" ? "회원가입" : "로그인";
}

function showSignedOut() {
  element("auth-panel").hidden = false;
  element("profile-panel").hidden = true;
  for (const id of ["display-name-value", "username-value", "best-score", "best-skills", "my-rank", "level", "xp", "coins", "weapon"]) element(id).textContent = "";
}

async function loadProfile(version = sessionVersion, quiet = false) {
  try {
    const data = await api("/web/me");
    if (version !== sessionVersion) return;
    element("auth-panel").hidden = true;
    element("profile-panel").hidden = false;
    element("display-name-value").textContent = data.user.displayName;
    element("username-value").textContent = "@" + data.user.username;
    element("best-score").textContent = number.format(data.playerData.bestScore);
    renderSkills(element("best-skills"), data.playerData.bestSkills);
    element("my-rank").textContent = data.rank ? number.format(data.rank) + "위" : "기록 없음";
    for (const key of ["level", "xp", "coins"]) element(key).textContent = number.format(data.playerData[key]);
    element("weapon").textContent = data.playerData.selectedWeapon;
    message("account-message");
  } catch (error) {
    if (version !== sessionVersion) return;
    if (error.status === 401) showSignedOut();
    if (!(quiet && error.status === 401)) message("account-message", error.message, true);
  }
}

async function loadRanking() {
  const data = await api("/leaderboard");
  if (!Array.isArray(data.entries)) throw new Error("순위 응답을 확인할 수 없습니다.");
  const fragment = document.createDocumentFragment();
  for (const entry of data.entries) {
    const row = document.createElement("tr");
    for (const value of [String(entry.rank).padStart(2, "0"), entry.displayName, number.format(entry.bestScore)]) {
      const cell = document.createElement("td");
      cell.textContent = value;
      row.append(cell);
    }
    if (entry.isGuest) {
      const badge = document.createElement("span");
      badge.className = "guest-tag";
      badge.textContent = "GUEST";
      row.children[1].append(badge);
    }
    const build = document.createElement("div");
    build.className = "record-skills";
    renderSkills(build, entry.bestSkills, true);
    row.children[1].append(build);
    fragment.append(row);
  }
  element("ranking-body").replaceChildren(fragment);
  element("empty-ranking").hidden = data.entries.length > 0;
  message("ranking-message");
  element("updated-at").textContent = new Date().toLocaleTimeString("ko-KR", { hour: "2-digit", minute: "2-digit" }) + " 갱신";
  element("connection").textContent = "서버 연결됨";
  element("connection").dataset.state = "online";
}

async function refresh() {
  if (refreshBusy) return;
  refreshBusy = true;
  element("refresh-button").disabled = true;
  message("ranking-message", "순위를 불러오는 중입니다.");
  await Promise.all([
    loadRanking().catch((error) => {
      message("ranking-message", error.message, true);
      element("connection").textContent = "서버 응답 없음";
      element("connection").dataset.state = "offline";
    }),
    loadProfile(sessionVersion, true),
  ]);
  refreshBusy = false;
  element("refresh-button").disabled = false;
}

element("auth-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  if (authBusy) return;
  if (mode === "register" && element("password").value !== element("confirm-password").value) {
    element("confirm-password").setCustomValidity("비밀번호가 일치하지 않습니다.");
    element("confirm-password").reportValidity();
    return;
  }
  const payload = { username: element("username").value.trim(), password: element("password").value };
  if (mode === "register") payload.displayName = element("display-name").value.trim();
  const version = ++sessionVersion;
  setAuthBusy(true);
  message("account-message", mode === "register" ? "계정을 만드는 중입니다." : "로그인 중입니다.");
  try {
    await api("/web/auth/" + mode, payload);
    element("password").value = "";
    element("confirm-password").value = "";
    await loadProfile(version);
  } catch (error) { message("account-message", error.message, true); }
  finally { setAuthBusy(false); }
});

element("logout-button").addEventListener("click", async () => {
  if (authBusy) return;
  ++sessionVersion;
  setAuthBusy(true);
  try {
    await api("/web/auth/logout", {});
    showSignedOut();
    setAuthBusy(false);
    setMode("login");
    message("account-message", "로그아웃되었습니다.");
  } catch (error) { message("account-message", error.message, true); }
  finally { setAuthBusy(false); }
});

for (const tab of ["login", "register"]) {
  element(tab + "-tab").addEventListener("click", () => setMode(tab));
  element(tab + "-tab").addEventListener("keydown", (event) => {
    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    setMode(event.key === "Home" ? "login" : event.key === "End" ? "register" : mode === "login" ? "register" : "login");
    element(mode + "-tab").focus();
  });
}
for (const id of ["password", "confirm-password"]) element(id).addEventListener("input", () => element("confirm-password").setCustomValidity(""));
element("toggle-password").addEventListener("click", () => {
  const shown = element("password").type === "password";
  element("password").type = shown ? "text" : "password";
  element("toggle-password").setAttribute("aria-pressed", String(shown));
  element("toggle-password").setAttribute("aria-label", shown ? "비밀번호 숨기기" : "비밀번호 표시");
  element("toggle-password").title = shown ? "비밀번호 숨기기" : "비밀번호 표시";
});
element("refresh-button").addEventListener("click", refresh);
lucide.createIcons();
refresh();
