// controller-relay/server.js
const express = require("express");
const { WebSocketServer } = require("ws");
const { customAlphabet } = require("nanoid");

// 4-char code, no ambiguous letters
const nanoCode = customAlphabet("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 4);

const PORT = process.env.PORT || 3010;

const app = express();
app.use(express.static("public")); // serves controller.html at /controller.html

const server = app.listen(PORT, () => {
  console.log("Relay listening on http://localhost:" + PORT);
});

const wss = new WebSocketServer({ server, path: "/ws" });
const rooms = new Map();

function safeSend(ws, obj) {
  try { ws.readyState === ws.OPEN && ws.send(JSON.stringify(obj)); } catch {}
}
function broadcastToHost(code, msg) {
  const r = rooms.get(code);
  if (r && r.host && r.host.readyState === r.host.OPEN) safeSend(r.host, msg);
}

function makeRoom(ws) {
  let tries = 0, code;
  do { code = nanoCode(); tries++; } while (rooms.has(code) && tries < 10);
  rooms.set(code, { host: ws, clients: new Map(), createdAt: Date.now() });
  return code;
}

function closeRoom(code) {
  const r = rooms.get(code);
  if (!r) return;
  for (const [, cws] of r.clients) { try { cws.close(1011, "Host closed"); } catch {} }
  rooms.delete(code);
}

function findClient(ws) {
  for (const [code, room] of rooms) {
    for (const [cid, cws] of room.clients) {
      if (cws === ws) return { code, cid };
    }
  }
  return null;
}

wss.on("connection", (ws, req) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const role = url.searchParams.get("role"); // "host" or "client"

  if (role === "host") {
    // Create a room for this host
    const code = makeRoom(ws);
    ws._roomCode = code;
    safeSend(ws, { t: "room_created", code });

    ws.on("message", (data) => {
      let msg;
      try { msg = JSON.parse(data.toString()); } catch { return; }
      // (Optional) Handle host->server messages here if needed
      // e.g., start game, broadcast state to clients, etc.
      if (msg.t === "broadcast_to_clients") {
        const r = rooms.get(code);
        if (r) {
          for (const [, cws] of r.clients) safeSend(cws, { t: "host_broadcast", payload: msg.payload });
        }
      }
    });

    ws.on("close", () => closeRoom(code));
    return;
  }

  if (role === "client") {
    const code = (url.searchParams.get("code") || "").toUpperCase();
    const name = (url.searchParams.get("name") || "Player").slice(0, 16);

    const room = rooms.get(code);
    if (!room || room.host.readyState !== room.host.OPEN) {
      safeSend(ws, { t: "error", reason: "Room not found" });
      ws.close(1008, "Room not found");
      return;
    }

    const clientId = customAlphabet("0123456789abcdef", 8)();
    const requestedTeam = url.searchParams.get("team");
    const validTeams = ["red", "blue", "green"];
    const team = validTeams.includes(requestedTeam) ? requestedTeam : "red";
    room.clients.set(clientId, ws);
    ws._roomCode = code;
    ws._clientId = clientId;
    ws._team = team;


    // Acknowledge to client & notify host
    safeSend(ws, { t: "joined", id: clientId, code, name, team });
    broadcastToHost(code, { t: "player_joined", id: clientId, name, team });

    ws.on("message", (data) => {
      let msg;
      try { msg = JSON.parse(data.toString()); } catch { return; }

      // Forward inputs to host
      // Expected messages from client:
      // { t:"btn", id, btn:"A"/"B"/"Up"/"Down"/"Left"/"Right", state:"down"|"up" }
      // { t:"axes", id, x:float, y:float }
      // { t:"ping" }
      if (msg.t === "ping") { safeSend(ws, { t: "pong" }); return; }

      if (msg.t === "btn" || msg.t === "axes") {
        msg.id = ws._clientId; // enforce correct id
        broadcastToHost(code, msg);
      }
    });

    ws.on("close", () => {
      const r = rooms.get(code);
      if (r) {
        r.clients.delete(clientId);
        broadcastToHost(code, { t: "player_left", id: clientId });
      }
    });

    return;
  }

  // Unknown role
  safeSend(ws, { t: "error", reason: "Invalid role" });
  ws.close(1008, "Invalid role");
});
