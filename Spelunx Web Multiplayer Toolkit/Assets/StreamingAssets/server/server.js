// controller-relay/server.js
// Messages use pipe-delimited strings instead of JSON.
// Format:  type|field1|field2|...
//
// HOST-bound (server → Unity):
//   room_created|CODE
//   player_queued|id|name
//   player_left_queue|id|name
//   waiting_state|count
//   game_start|id1|name1|slot1|id2|name2|slot2|...
//   player_left|id
//   slider|id|slot|value
//   text_msg|id|slot|text
//   action_btn|id|slot|state|presses
//   btn|id|slot|btn|state
//
// CLIENT-bound (server → phone):
//   joined|id|code|name|phase
//   slot_assigned|slot|name
//   game_start
//   waiting_state|count
//   slot_message|text|from
//   pong
//   error|reason
//
// CLIENT→SERVER (phone → server):
//   ping
//   slider|value
//   text_msg|text
//   action_btn|state|presses
//   btn|btn|state
//   axes|x|y
//
// HOST→SERVER (Unity → server):
//   assign_slots
//   send_to_slot|slot|text|from
//   broadcast_to_clients|payload

const express = require("express");
const { WebSocketServer } = require("ws");
const { customAlphabet }  = require("nanoid");

const nanoCode  = customAlphabet("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 4);
const nanoId    = customAlphabet("0123456789abcdef", 8);
const MAX_SLOTS = 4;
const PORT      = process.env.PORT || 3010;

const app = express();
app.use(express.static("public"));
const server = app.listen(PORT, () => console.log("Relay on http://localhost:" + PORT));
const wss    = new WebSocketServer({ server, path: "/ws" });
const rooms  = new Map();

// ── helpers ───────────────────────────────────────────────────────────────────

function send(ws, ...parts) {
  try {
    if (ws?.readyState === ws?.OPEN) ws.send(parts.join("|"));
  } catch {}
}

function parse(raw) {
  return raw.toString().split("|");
}

function toHost(code, ...parts) {
  const r = rooms.get(code);
  if (r?.host?.readyState === r?.host?.OPEN) send(r.host, ...parts);
}

function toAll(code, ...parts) {
  const r = rooms.get(code);
  if (!r) return;
  for (const seat of r.queue)      if (seat.ws) send(seat.ws, ...parts);
  for (const [, seat] of r.slots)  if (seat?.ws) send(seat.ws, ...parts);
}

function sendWaitingState(code) {
  const r = rooms.get(code);
  if (!r) return;
  const count = r.queue.filter(s => s.ws !== null).length;
  for (const seat of r.queue) if (seat.ws) send(seat.ws, "waiting_state", count);
  toHost(code, "waiting_state", count);
  if (r.hostPage?.readyState === r.hostPage?.OPEN)
    send(r.hostPage, "waiting_state", count);
}

// ── room ──────────────────────────────────────────────────────────────────────

function makeRoom(hostWs) {
  let tries = 0, code;
  do { code = nanoCode(); tries++; } while (rooms.has(code) && tries < 10);
  rooms.set(code, {
    host:      hostWs,
    hostPage:  null,
    clients:   new Map(),
    queue:     [],
    slots:     new Map([[1,null],[2,null],[3,null],[4,null]]),
    phase:     "waiting",
    createdAt: Date.now()
  });
  return code;
}

function closeRoom(code) {
  const r = rooms.get(code);
  if (!r) return;
  for (const [, cws] of r.clients) { try { cws.close(1011, "Host closed"); } catch {} }
  if (r.hostPage) { try { r.hostPage.close(1011); } catch {} }
  rooms.delete(code);
}

function doAssignAndStart(code, initiator) {
  const r = rooms.get(code);
  if (!r || r.phase === "game") return;
  const online = r.queue.filter(s => s.ws !== null);
  if (!online.length) return;

  r.phase = "game";

  // Build the flat game_start message for Unity: game_start|id1|name1|1|id2|name2|2|...
  const parts = ["game_start"];
  online.forEach((seat, i) => {
    seat.slot = i + 1;
    r.slots.set(seat.slot, seat);
    parts.push(seat.clientId, seat.name, seat.slot);
    // Tell the phone its slot, then start
    send(seat.ws, "slot_assigned", seat.slot, seat.name);
    send(seat.ws, "game_start");
  });

  toHost(code, ...parts);
  if (r.hostPage?.readyState === r.hostPage?.OPEN)
    send(r.hostPage, ...parts);

  console.log(`[${code}] Started by ${initiator}:`, online.map(s=>`P${s.slot}=${s.name}`).join(", "));
}

// ── connections ───────────────────────────────────────────────────────────────

wss.on("connection", (ws, req) => {
  const url  = new URL(req.url, `http://${req.headers.host}`);
  const role = url.searchParams.get("role");

  // ══════════════════════════════════════════════════════ UNITY HOST
  if (role === "host") {
    const code = makeRoom(ws);
    ws._roomCode = code;
    send(ws, "room_created", code);

    ws.on("message", raw => {
      const [t, ...a] = parse(raw);

      if (t === "assign_slots") { doAssignAndStart(code, "Unity"); return; }

      if (t === "send_to_slot") {
        // send_to_slot|slot|text|from
        const slot = parseInt(a[0]);
        const seat = rooms.get(code)?.slots.get(slot);
        if (seat?.ws) send(seat.ws, "slot_message", a[1], a[2]);
        return;
      }

      if (t === "broadcast_to_clients") {
        toAll(code, "host_broadcast", a.join("|"));
      }
    });

    ws.on("close", () => closeRoom(code));
    return;
  }

  // ══════════════════════════════════════════════════════ HOST WEBPAGE
  if (role === "host_page") {
    const code = (url.searchParams.get("code") || "").toUpperCase();
    const r    = rooms.get(code);
    if (!r) { send(ws, "error", "Room not found"); ws.close(1008); return; }

    r.hostPage = ws;
    send(ws, "host_page_joined", code, r.phase);
    sendWaitingState(code);

    ws.on("message", raw => {
      const [t] = parse(raw);
      if (t === "assign_slots") doAssignAndStart(code, "host_page");
    });

    ws.on("close", () => { const r = rooms.get(code); if (r?.hostPage === ws) r.hostPage = null; });
    return;
  }

  // ══════════════════════════════════════════════════════ CLIENT (phone)
  if (role === "client") {
    const code = (url.searchParams.get("code") || "").toUpperCase();
    const name = (url.searchParams.get("name") || "Player").slice(0, 16);

    const room = rooms.get(code);
    if (!room || room.host.readyState !== room.host.OPEN) {
      send(ws, "error", "Room not found"); ws.close(1008); return;
    }

    const clientId = nanoId();
    room.clients.set(clientId, ws);
    ws._roomCode = code;
    ws._clientId = clientId;
    ws._name     = name;

    if (room.phase === "waiting") {
      let seat = room.queue.find(s => s.name === name && s.ws === null);
      if (seat) {
        seat.ws = ws; seat.clientId = clientId;
        ws._queueIdx = room.queue.indexOf(seat);
      } else {
        room.queue.push({ name, clientId, ws });
        ws._queueIdx = room.queue.length - 1;
      }

      send(ws, "joined", clientId, code, name, "waiting");
      toHost(code, "player_queued", clientId, name);
      sendWaitingState(code);

    } else {
      // Mid-game reconnect
      let foundSlot = null;
      for (const [slotNum, seat] of room.slots) {
        if (seat && seat.name === name) {
          foundSlot = slotNum; seat.ws = ws; seat.clientId = clientId; break;
        }
      }
      if (foundSlot) {
        send(ws, "joined",       clientId, code, name, "game");
        send(ws, "slot_assigned", foundSlot, name);
        send(ws, "game_start");
      } else {
        send(ws, "error", "Game already started"); ws.close(1008); return;
      }
    }

    ws.on("message", raw => {
      const [t, ...a] = parse(raw);
      if (t === "ping") { send(ws, "pong"); return; }

      const r = rooms.get(code);
      if (!r || r.phase !== "game") return;

      // Find this client's assigned slot
      let slot = null;
      for (const [n, seat] of r.slots) { if (seat?.clientId === clientId) { slot = n; break; } }
      if (!slot) return;

      if (t === "slider")     { toHost(code, "slider",     clientId, slot, a[0]); return; }
      if (t === "text_msg")   { toHost(code, "text_msg",   clientId, slot, a.join("|")); return; }
      if (t === "action_btn") { toHost(code, "action_btn", clientId, slot, a[0], a[1]); return; }
      if (t === "btn")        { toHost(code, "btn",        clientId, slot, a[0], a[1]); return; }
      if (t === "axes")       { toHost(code, "axes",       clientId, slot, a[0], a[1]); return; }
    });

    ws.on("close", () => {
      const r = rooms.get(code);
      if (!r) return;
      r.clients.delete(clientId);

      if (r.phase === "waiting") {
        const seat = r.queue[ws._queueIdx];
        if (seat?.clientId === clientId) seat.ws = null;
        toHost(code, "player_left_queue", clientId, name);
        sendWaitingState(code);
      } else {
        for (const [, seat] of r.slots) { if (seat?.clientId === clientId) { seat.ws = null; break; } }
        toHost(code, "player_left", clientId);
      }
    });

    return;
  }

  send(ws, "error", "Invalid role"); ws.close(1008);
});