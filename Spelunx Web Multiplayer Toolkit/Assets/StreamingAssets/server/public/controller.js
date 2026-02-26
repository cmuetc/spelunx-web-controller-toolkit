let ws, clientId = null;
let code = null;
let name = "Player";
let selectedTeam = null;

/* -------- ENERGY -------- */
let energy = 1.0;
const maxEnergy = 1.0;
const drainPerTap = 0.1;
const regenRate = 0.15;
const minEnergyToCheer = 0.10;

const energyFill = document.getElementById("energyFill");
const energyValue = document.getElementById("energyValue");
const st = document.getElementById("status");
const cheerBtn = document.getElementById("cheerBtn");
const cheerImg = document.getElementById("cheerImg");

const sfxEmpty = document.getElementById("sfxEmpty");

function qs(k){ return new URLSearchParams(location.search).get(k); }
function send(o){ if(ws && ws.readyState===WebSocket.OPEN) ws.send(JSON.stringify(o)); }

/* ENERGY LOOP */
let lastTime = performance.now();
function loop(){
  const now = performance.now();
  const dt = (now - lastTime) / 1000;
  lastTime = now;

  updateEnergy(dt);
  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

function updateEnergy(dt){

  energy += regenRate * dt;
  energy = Math.min(maxEnergy, energy);

  if (energy <= minEnergyToCheer) {
      sfxEmpty.currentTime = 0;
      sfxEmpty.play();
  }

  energyFill.style.width = (energy * 100) + "%";
  energyValue.textContent = Math.round(energy * 100) + "%";
  cheerBtn.disabled = (energy <= minEnergyToCheer);
  cheerBtn.style.opacity = cheerBtn.disabled ? 0.5 : 1.0;
}

/* -------- BUTTON LOGIC -------- */
let buttonsBound = false;
function bindButtons(){
  if (buttonsBound) return;
  buttonsBound = true;

  cheerBtn.addEventListener("pointerdown", e=>{
    e.preventDefault();
    if (energy > minEnergyToCheer){
      energy = Math.max(0, energy - drainPerTap);
      send({ t:"btn", btn:"Accel", energy:Math.round(drainPerTap*100) });


      cheerBtn.classList.add("animate");
      setTimeout(()=>cheerBtn.classList.remove("animate"), 250);

      if (navigator.vibrate) navigator.vibrate(50);
    }
  });
}

/* -------- WEBSOCKET -------- */
function connectAndJoin(c,n){
  const proto = location.protocol==="https:" ? "wss" : "ws";
  const url = `${proto}://${location.host}/ws?role=client&code=${encodeURIComponent(c)}&name=${encodeURIComponent(n)}&team=${selectedTeam}`;
  
  ws = new WebSocket(url);

  ws.onopen = ()=> st.textContent="Connecting...";

  ws.onmessage = e=>{
    const msg = JSON.parse(e.data);

    if(msg.t==="joined"){
      clientId = msg.id;
      const team = msg.team || "Unknown";
      st.textContent = `Joined as ${team} Team`;

      const pad = document.getElementById("pad");

      /* TEAM COLORS */
      if(team === "red"){
        cheerImg.src = "RedButton.png";
        energyFill.style.background = "linear-gradient(90deg,#ff0000,#ff8800)";
        setBackground(pad, "RedBackground.jpg", "#ff7a00");
      }
      else if(team === "blue"){
        cheerImg.src = "BlueButton.png";
        energyFill.style.background = "linear-gradient(90deg,#007bff,#00bfff)";
        setBackground(pad, "BlueBackground.jpg", "#007bff");
      }
      else if(team === "green"){
        cheerImg.src = "GreenButton.png";
        energyFill.style.background = "linear-gradient(90deg,#BDFFFF,#44FFFF)";
        setBackground(pad, "GreenBackground.png", "#44FFFF");
      }

      document.getElementById("teamSelect").classList.add("hidden");
      pad.classList.add("active");

      bindButtons();
      heartbeat();
    }
  };

  ws.onclose = ()=>{
    st.textContent="Disconnected.";
    buttonsBound = false;
    document.getElementById("pad").classList.remove("active");
    document.getElementById("join").classList.remove("hidden");
  };
}

function setBackground(pad, img, color){
  pad.style.backgroundImage = `url("${img}")`;
  pad.style.backgroundSize = "cover";
  pad.style.backgroundPosition = "center";
  pad.style.backgroundRepeat = "no-repeat";
  pad.style.backgroundColor = color;
}

function heartbeat(){
  if(ws?.readyState===WebSocket.OPEN){
    send({t:"ping"});
    setTimeout(heartbeat,10000);
  }
}

/* -------- JOIN ACTION -------- */
document.getElementById("joinBtn").onclick = ()=>{
  code = document.getElementById("code").value.trim().toUpperCase();
  name = document.getElementById("name").value.trim();

  if (!name) {
    st.textContent = "Please enter your name.";
    return;
  }
  if (!code || code.length < 4) {
    st.textContent = "Enter 4-letter room code.";
    return;
  }

  // Move to team selection screen
  document.getElementById("join").classList.add("hidden");
  document.getElementById("teamSelect").classList.remove("hidden");
};

document.querySelectorAll(".team-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    selectedTeam = btn.dataset.team;  // red, blue, or green

    document.getElementById("teamStatus").textContent =
      "Connecting to room...";

    // Now actually connect to WS
    connectAndJoin(code, name);
  });
});

/* AUTO JOIN SUPPORT */
const cq = qs("code"), nq = qs("name");
if(cq){
  document.getElementById("code").value = cq.toUpperCase();
  if(nq) document.getElementById("name").value = nq;
  document.getElementById("joinBtn").click();
}