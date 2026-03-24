/* -------- URL PARAMS -------- */
function qs(k){ return new URLSearchParams(location.search).get(k); }

const code = (qs("code") || "").toUpperCase();
const name = qs("name") || "Player";
const team = qs("team") || "red";

if (!code) window.location.href = "join.html";

/* -------- STATE -------- */
let ws       = null;
let clientId = null;

const padStatus = document.getElementById("pad-status");

function send(o){ if(ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(o)); }

/* -------- DPAD -------- */
let activeDir    = null;
let holdInterval = null;
const HOLD_REPEAT_MS = 120;

const dirMap = {
  up:    "ArrowUp",
  down:  "ArrowDown",
  left:  "ArrowLeft",
  right: "ArrowRight"
};

function pressDir(dir) {
  if (activeDir === dir) return;
  releaseDir();
  activeDir = dir;
  document.getElementById("dpad-" + dir)?.classList.add("active");
  sendDir(dir, "down");
  holdInterval = setInterval(() => sendDir(dir, "hold"), HOLD_REPEAT_MS);
  if (navigator.vibrate) navigator.vibrate(30);
}

function releaseDir() {
  if (!activeDir) return;
  document.getElementById("dpad-" + activeDir)?.classList.remove("active");
  sendDir(activeDir, "up");
  activeDir = null;
  clearInterval(holdInterval);
  holdInterval = null;
}

function sendDir(dir, state) {
  send({ t: "btn", btn: dirMap[dir], state });
}

/* -------- BIND CONTROLS -------- */
function bindButtons() {
  document.querySelectorAll(".dpad-zone").forEach(zone => {
    const dir = zone.dataset.dir;
    zone.addEventListener("pointerdown", e => {
      e.preventDefault();
      zone.setPointerCapture(e.pointerId);
      pressDir(dir);
    });
    zone.addEventListener("pointerup",     e => { e.preventDefault(); releaseDir(); });
    zone.addEventListener("pointercancel", e => { e.preventDefault(); releaseDir(); });
    zone.addEventListener("pointermove",   e => { e.preventDefault(); });
  });

  document.getElementById("dpad")
    .addEventListener("pointerleave", () => releaseDir());

  const jumpBtn = document.getElementById("jumpBtn");
  jumpBtn.addEventListener("pointerdown", e => {
    e.preventDefault();
    jumpBtn.setPointerCapture(e.pointerId);
    jumpBtn.classList.add("active");
    send({ t: "btn", btn: "Jump", state: "down" });
    if (navigator.vibrate) navigator.vibrate(40);
  });
  jumpBtn.addEventListener("pointerup", e => {
    e.preventDefault();
    jumpBtn.classList.remove("active");
    send({ t: "btn", btn: "Jump", state: "up" });
  });
  jumpBtn.addEventListener("pointercancel", e => {
    e.preventDefault();
    jumpBtn.classList.remove("active");
  });
}

/* -------- TEAM COLORS -------- */
const teamConfig = {
  red:   { base: "#cc2200", active: "#ff4422", arrow: "#ffffff" },
  blue:  { base: "#0044cc", active: "#2266ff", arrow: "#ffffff" },
  green: { base: "#00bbaa", active: "#44ffee", arrow: "#003333" }
};

function applyTeam(t) {
  const pad = document.getElementById("pad");
  const c   = teamConfig[t] || teamConfig.red;

  /* Remove any previous team class, add new one */
  pad.classList.remove("team-red", "team-blue", "team-green");
  pad.classList.add("team-" + t);

  document.documentElement.style.setProperty("--dpad-base",   c.base);
  document.documentElement.style.setProperty("--dpad-active", c.active);
  document.documentElement.style.setProperty("--dpad-arrow",  c.arrow);
  document.getElementById("jumpBtn").style.color = c.arrow;
}

/* -------- WEBSOCKET -------- */
function connect() {
  const proto = location.protocol === "https:" ? "wss" : "ws";
  const url   = `${proto}://${location.host}/ws?role=client&code=${encodeURIComponent(code)}&name=${encodeURIComponent(name)}&team=${encodeURIComponent(team)}`;

  ws = new WebSocket(url);
  padStatus.textContent = "Connecting…";

  ws.onopen = () => padStatus.textContent = "Connected";

  ws.onmessage = e => {
    const msg = JSON.parse(e.data);
    if (msg.t === "joined") {
      clientId = msg.id;
      const confirmedTeam = msg.team || team;
      padStatus.textContent = confirmedTeam.charAt(0).toUpperCase() + confirmedTeam.slice(1) + " Team";
      applyTeam(confirmedTeam);
      document.getElementById("pad").classList.add("active");
      bindButtons();
      heartbeat();
    }
  };

  ws.onclose = () => {
    padStatus.textContent = "Disconnected";
    releaseDir();
    document.getElementById("pad").classList.remove("active");
    document.getElementById("disconnected").classList.remove("hidden");
  };
}

function heartbeat() {
  if (ws?.readyState === WebSocket.OPEN) {
    send({ t: "ping" });
    setTimeout(heartbeat, 10000);
  }
}

/* -------- DISCONNECTED OVERLAY BUTTONS -------- */
document.getElementById("reconnectBtn").addEventListener("click", () => {
  document.getElementById("disconnected").classList.add("hidden");
  connect();
});

document.getElementById("backBtn").addEventListener("click", () => {
  window.location.href = "join.html";
});

/* -------- INIT -------- */
applyTeam(team);
connect();