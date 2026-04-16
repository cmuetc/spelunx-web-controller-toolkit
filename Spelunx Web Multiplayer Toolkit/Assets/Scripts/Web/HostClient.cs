using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;

// ─────────────────────────────────────────────────────────────────────────────
// Per-player input state
// ─────────────────────────────────────────────────────────────────────────────
public class PlayerInputState
{
    public string playerId;
    public string playerName;
    public int    slot;

    // Slot 1 — Slider
    public float sliderValue;
    public float SliderNormalized => sliderValue / 100f;
    public bool  sliderChanged;

    // Slot 2 — Messenger
    public string lastMessage;
    public bool   newMessage;
    public bool   _newMessagePending;

    // Slot 3 — Action button
    public bool actionDown;
    public bool actionPressed;
    public bool actionReleased;
    public int  totalPresses;
    public bool _prevAction;

    // Legacy d-pad
    public bool up, down, left, right, jump;
    public bool jumpPressed, jumpReleased;
    public bool _prevJump;

    public Vector2 DirectionVector =>
        new Vector2((right ? 1f : 0f) - (left ? 1f : 0f),
                    (up    ? 1f : 0f) - (down ? 1f : 0f));
}

// ─────────────────────────────────────────────────────────────────────────────
// HostClient  — messages are pipe-delimited strings, e.g. "slider|id|slot|73.5"
// ─────────────────────────────────────────────────────────────────────────────
public class HostClient : MonoBehaviour
{
    [Header("Relay")]
    public string relayHost = "localhost";
    public int    relayPort = 3010;
    public bool   useSecure = false;
    public bool   isRemoted = false;

    public string RoomCode      { get; private set; }
    public string Phase         { get; private set; } = "waiting";
    public bool   IsGameStarted => Phase == "game";
    public int    QueueCount    { get; private set; }

    public readonly Dictionary<string, PlayerInputState> Players = new();
    public readonly Dictionary<int,    PlayerInputState> Slots   = new();
    public readonly Dictionary<string, string>           players = new();

    public event Action<string, string>                   PlayerQueued;
    public event Action<string, string, int>              PlayerJoined;
    public event Action<string, int>                      PlayerLeft;
    public event Action<PlayerInputState, float>          SliderEvent;
    public event Action<PlayerInputState, string>         MessageEvent;
    public event Action<PlayerInputState, string>         ActionEvent;
    public event Action<PlayerInputState, string, string> ButtonEvent;
    public event Action                                   GameStarted;

    public PlayerInputRouter router;

    WebSocket _ws;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    async void Start()
    {
        if (!isRemoted) relayHost = "localhost";
        await Connect();
    }

    async Task Connect()
    {
        string scheme = useSecure ? "wss" : "ws";
        _ws = new WebSocket($"{scheme}://{relayHost}:{relayPort}/ws?role=host");

        _ws.OnOpen    += ()  => Debug.Log("[HostClient] Connected.");
        _ws.OnError   += e   => Debug.LogError("[HostClient] Error: " + e);
        _ws.OnClose   += c   => Debug.LogWarning("[HostClient] Closed: " + c);
        _ws.OnMessage += bytes => Handle(System.Text.Encoding.UTF8.GetString(bytes));

        await _ws.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif
        foreach (var p in Players.Values)
        {
            p.newMessage         = p._newMessagePending;
            p._newMessagePending = false;

            p.actionPressed  = p.actionDown  && !p._prevAction;
            p.actionReleased = !p.actionDown && p._prevAction;
            p.jumpPressed    = p.jump        && !p._prevJump;
            p.jumpReleased   = !p.jump       && p._prevJump;
        }
    }

    void LateUpdate()
    {
        foreach (var p in Players.Values)
        {
            p._prevAction   = p.actionDown;
            p._prevJump     = p.jump;
            p.sliderChanged = false;
        }
    }

    // ── Message handler ───────────────────────────────────────────────────────
    // All messages: "type|field1|field2|..."

    void Handle(string raw)
    {
        // Split only up to needed parts; use max 8 splits for text_msg content safety
        string[] p = raw.Split('|', 8);
        string   t = p[0];

        switch (t)
        {
            // room_created|CODE
            case "room_created":
                RoomCode = p[1];
                Debug.Log("[HostClient] Room: " + RoomCode);
                break;

            // player_queued|id|name
            case "player_queued":
            {
                string id = p[1], name = p[2];
                QueueCount++;
                Debug.Log($"[HostClient] QUEUED {name}");
                router?.OnPlayerQueued(id, name);
                PlayerQueued?.Invoke(id, name);
                break;
            }

            // player_left_queue|id|name
            case "player_left_queue":
                QueueCount = Mathf.Max(0, QueueCount - 1);
                router?.OnPlayerLeftQueue(p[1], p[2]);
                break;

            // waiting_state|count
            case "waiting_state":
                // Informational — router can override OnWaitingState if needed
                break;

            // game_start|id1|name1|slot1|id2|name2|slot2|...
            case "game_start":
            {
                Phase = "game";
                // Parse triplets starting at index 1
                for (int i = 1; i + 2 < p.Length; i += 3)
                {
                    string id   = p[i];
                    string name = p[i + 1];
                    int    slot = int.TryParse(p[i + 2], out int s) ? s : 0;
                    if (slot == 0) break;

                    var state = new PlayerInputState
                        { playerId = id, playerName = name, slot = slot };
                    Players[id] = state;
                    Slots[slot] = state;
                    players[id] = name;

                    Debug.Log($"[HostClient] ASSIGNED P{slot} = {name}");
                    router?.OnPlayerJoined(id, name, slot);
                    PlayerJoined?.Invoke(id, name, slot);
                }
                Debug.Log("[HostClient] Game started!");
                router?.OnGameStart();
                GameStarted?.Invoke();
                break;
            }

            // player_left|id
            case "player_left":
            {
                string id = p[1];
                if (Players.TryGetValue(id, out var state))
                {
                    Slots.Remove(state.slot);
                    Players.Remove(id);
                    players.Remove(id);
                    Debug.Log($"[HostClient] LEFT P{state.slot} {state.playerName}");
                    router?.OnPlayerLeft(id, state.slot);
                    PlayerLeft?.Invoke(id, state.slot);
                }
                break;
            }

            // slider|id|slot|value
            case "slider":
            {
                string id = p[1];
                float val = float.TryParse(p[3], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;
                if (!Players.TryGetValue(id, out var player)) break;
                player.sliderValue   = val;
                player.sliderChanged = true;
                router?.OnSliderInput(id, val);
                SliderEvent?.Invoke(player, val);
                break;
            }

            // text_msg|id|slot|text...
            case "text_msg":
            {
                string id   = p[1];
                string text = p.Length > 3 ? p[3] : "";
                if (!Players.TryGetValue(id, out var player)) break;
                player.lastMessage        = text;
                player._newMessagePending = true;
                Debug.Log($"[HostClient] MSG {player.playerName}: {text}");
                router?.OnTextMessage(id, text);
                MessageEvent?.Invoke(player, text);
                break;
            }

            // action_btn|id|slot|state|presses
            case "action_btn":
            {
                string id     = p[1];
                string state  = p[3];
                int presses   = int.TryParse(p[4], out int pr) ? pr : 0;
                if (!Players.TryGetValue(id, out var player)) break;
                player.actionDown   = state == "down";
                player.totalPresses = presses;
                router?.OnActionButton(id, state, presses);
                ActionEvent?.Invoke(player, state);
                break;
            }

            // btn|id|slot|btn|state
            case "btn":
            {
                string id    = p[1];
                string btn   = p[3];
                string state = p[4];
                if (!Players.TryGetValue(id, out var player)) break;
                bool held = state == "down" || state == "hold";
                switch (btn)
                {
                    case "ArrowUp":    player.up    = held; break;
                    case "ArrowDown":  player.down  = held; break;
                    case "ArrowLeft":  player.left  = held; break;
                    case "ArrowRight": player.right = held; break;
                    case "Jump":       player.jump  = held; break;
                }
                router?.OnButtonInput(id, btn, state);
                ButtonEvent?.Invoke(player, btn, state);
                break;
            }

            // error|reason
            case "error":
                Debug.LogError("[HostClient] Server error: " + (p.Length > 1 ? p[1] : ""));
                break;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public PlayerInputState GetSlot(int slot) =>
        Slots.TryGetValue(slot, out var p) ? p : null;

    async void Send(string msg)
    {
        if (_ws?.State == WebSocketState.Open) await _ws.SendText(msg);
    }

    public void AssignAndStart()      => Send("assign_slots");

    public void SendTextToDisplay(string text, string from = "Host")
        => Send($"send_to_slot|4|{text}|{from}");

    public void BroadcastToClients(string payload)
        => Send($"broadcast_to_clients|{payload}");

    void OnApplicationQuit() => _ws?.Close();
}