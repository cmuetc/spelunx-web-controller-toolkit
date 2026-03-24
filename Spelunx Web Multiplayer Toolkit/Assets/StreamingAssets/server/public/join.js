let code = null;
let name = "Player";

const st     = document.getElementById("status");
const joinEl = document.getElementById("join");
const teamEl = document.getElementById("teamSelect");

function qs(k){ return new URLSearchParams(location.search).get(k); }

/* -------- JOIN BUTTON -------- */
document.getElementById("joinBtn").onclick = () => {
  code = document.getElementById("code").value.trim().toUpperCase();
  name = document.getElementById("name").value.trim();

  if (!name) { st.textContent = "Please enter your name."; return; }
  if (!code || code.length < 4) { st.textContent = "Enter 4-letter room code."; return; }

  joinEl.classList.add("hidden");
  teamEl.classList.remove("hidden");
};

/* -------- TEAM SELECT -------- */
document.querySelectorAll(".team-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    const team = btn.dataset.team;
    document.getElementById("teamStatus").textContent = "Loading controller...";

    const params = new URLSearchParams({ code, name, team });
    window.location.href = "controller.html?" + params.toString();
  });
});

/* -------- AUTO-FILL from URL (e.g. QR code link) -------- */
const cq = qs("code"), nq = qs("name");
if (cq) {
  document.getElementById("code").value = cq.toUpperCase();
  if (nq) document.getElementById("name").value = nq;
  document.getElementById("joinBtn").click();
}