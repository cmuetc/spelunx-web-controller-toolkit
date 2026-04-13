// controller-relay/server.js
const express = require("express");
const { WebSocketServer } = require("ws");
const { customAlphabet } = require("nanoid");

const nanoCode   = customAlphabet("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 4);
const nanoId     = customAlphabet("0123456789abcdef", 8);
const MAX_SLOTS  = 4;
const PORT       = process.env.PORT || 3010;

const app = express();
app.use(express.static("public"));
const server = app.listen(PORT, () => console.log("Relay on http://localhost:" + PORT));
const wss    = new WebSocketServer({ server, path: "/ws" });
const rooms  = new Map();

// ── helpers ──────────────────────────────────────────────────────────────────

function safeSend(ws, obj) {
  try { ws.readyState === ws.OPEN && ws.send(JSON.stringify(obj)); } catch {}
}

function broadcastToHost(code, msg) {
  const r = rooms.get(code);
  if (r?.host?.readyState === r?.host?.OPEN) safeSend(r.host, msg);
}

// ── room ─────────────────────────────────────────────────────────────────────

function makeRoom(hostWs) {
  let tries = 0, code;
  do { code = nanoCode(); tries++; } while (rooms.has(code) && tries < 10);

  // slots[1..4]:  null = empty, or { name, clientId, ws }
  // ws = null means reserved-but-disconnected (player can reclaim by same name)
  const slots = new Map([[1,null],[2,null],[3,null],[4,null]]);

  rooms.set(code, { host: hostWs, clients: new Map(), slots, createdAt: Date.now() });
  return code;
}

function closeRoom(code) {
  const r = rooms.get(code);
  if (!r) return;
  for (const [, cws] of r.clients) { try { cws.close(1011, "Host closed"); } catch {} }
  rooms.delete(code);
}

// ── connection ────────────────────────────────────────────────────────────────

wss.on("connection", (ws, req) => {
  const url  = new URL(req.url, `http://${req.headers.host}`);
  const role = url.searchParams.get("role");

  // ═══════════════════════════════════════ HOST
  if (role === "host") {
    const code = makeRoom(ws);
    ws._roomCode = code;
    safeSend(ws, { t: "room_created", code });

    ws.on("message", raw => {
      let msg; try { msg = JSON.parse(raw.toString()); } catch { return; }

      if (msg.t === "broadcast_to_clients") {
        const r = rooms.get(code);
        if (r) for (const [, cws] of r.clients)
          safeSend(cws, { t: "host_broadcast", payload: msg.payload });
      }

      if (msg.t === "send_to_slot") {
        const seat = rooms.get(code)?.slots.get(msg.slot);
        if (seat?.ws) safeSend(seat.ws, { t: "slot_message", payload: msg.payload });
      }
    });

    ws.on("close", () => closeRoom(code));
    return;
  }

  // ═══════════════════════════════════════ CLIENT
  if (role === "client") {
    const code = (url.searchParams.get("code") || "").toUpperCase();
    const name = (url.searchParams.get("name") || "Player").slice(0, 16);

    const room = rooms.get(code);
    if (!room || room.host.readyState !== room.host.OPEN) {
      safeSend(ws, { t: "error", reason: "Room not found" });
      ws.close(1008, "Room not found");
      return;
    }

    // ── Slot assignment ──────────────────────────────────────────────────────
    // Priority 1: reclaim own disconnected seat (matched by name)
    // Priority 2: lowest free seat (null)
    // If all 4 seats occupied by different live/reserved names → reject
    let slot = null;

    for (const [n, seat] of room.slots) {
      if (seat && seat.name === name && seat.ws === null) { slot = n; break; }
    }
    if (slot === null) {
      for (const [n, seat] of room.slots) {
        if (seat === null) { slot = n; break; }
      }
    }
    if (slot === null) {
      safeSend(ws, { t: "error", reason: "Room is full (4/4 seats taken)" });
      ws.close(1008, "Room full");
      return;
    }

    const clientId = nanoId();
    room.slots.set(slot, { name, clientId, ws });
    room.clients.set(clientId, ws);

    ws._roomCode = code;
    ws._clientId = clientId;
    ws._slot     = slot;
    ws._name     = name;

    safeSend(ws, { t: "joined", id: clientId, code, name, slot });
    broadcastToHost(code, { t: "player_joined", id: clientId, name, slot });

    // ── Messages ─────────────────────────────────────────────────────────────
    ws.on("message", raw => {
      let msg; try { msg = JSON.parse(raw.toString()); } catch { return; }

      if (msg.t === "ping") { safeSend(ws, { t: "pong" }); return; }

      if (msg.t === "slider") {
        broadcastToHost(code, { t: "slider", id: clientId, slot, value: msg.value });
        return;
      }

      if (msg.t === "text_msg") {
        // Forward ONLY to host (Unity). Unity calls SendTextToDisplay() which
        // comes back as send_to_slot → slot 4. Full P2→server→Unity→server→P4 round-trip.
        broadcastToHost(code, { t: "text_msg", id: clientId, slot, text: msg.text });
        return;
      }

      if (msg.t === "action_btn") {
        broadcastToHost(code, { t: "action_btn", id: clientId, slot, state: msg.state, presses: msg.presses });
        return;
      }

      // legacy d-pad / axes
      if (msg.t === "btn" || msg.t === "axes") {
        broadcastToHost(code, { ...msg, id: clientId, slot });
      }
    });

    // ── Disconnect: keep seat reserved by name; just null out the ws ─────────
    ws.on("close", () => {
      const r = rooms.get(code);
      if (!r) return;
      r.clients.delete(clientId);
      const seat = r.slots.get(slot);
      if (seat && seat.clientId === clientId) {
        seat.ws = null; // seat stays reserved for this name
      }
      broadcastToHost(code, { t: "player_left", id: clientId, slot });
    });

    return;
  }

  safeSend(ws, { t: "error", reason: "Invalid role" });
  ws.close(1008, "Invalid role");
});
