using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;

// -------------------------------------------------------
// Message envelope — matches every JSON shape the server sends
// -------------------------------------------------------
[Serializable]
public class MsgBase
{
    public string t;        // message type
    public string code;     // room code
    public string id;       // player id
    public string name;     // player name
    public string team;     // "red" | "blue" | "green" (legacy)
    public int    slot;     // 1 | 2 | 3 | 4  — join order
    public string btn;      // "ArrowUp" | "ArrowDown" | "ArrowLeft" | "ArrowRight" | "Jump"
    public string state;    // "down" | "hold" | "up"
    public string reason;   // error reason
    public float  value;    // slider value (0–100)
    public string text;     // text message (slot 2)
    public int    presses;  // cumulative press count (slot 3)
}

// -------------------------------------------------------
// Per-player input state — read this from PlayerInputRouter
// -------------------------------------------------------
[Serializable]
public class PlayerInputState
{
    public string playerId;
    public string playerName;
    public int    slot;         // 1 = slider, 2 = messenger, 3 = action btn, 4 = display

    // ---- Slot 1: Slider ----
    public float sliderValue;           // 0.0 – 100.0, normalised to 0–1 via SliderNormalized
    public float SliderNormalized => sliderValue / 100f;
    public bool  sliderChanged;         // true for one frame after slider moves

    // ---- Slot 2: Messenger ----
    public string lastMessage;          // most recent text sent
    public bool   newMessage;           // true for one frame after a message arrives
    [NonSerialized] public bool _newMessagePending;
    [NonSerialized] public string _prevMessage;

    // ---- Slot 3: Action button ----
    public bool   actionDown;           // held state
    public bool   actionPressed;        // true for one frame (leading edge)
    public bool   actionReleased;       // true for one frame (trailing edge)
    public int    totalPresses;         // cumulative count
    [NonSerialized] public bool _prevAction;

    // ---- Legacy d-pad (slot 1 fallback / original controllers) ----
    public bool up, down, left, right, jump;
    public bool jumpPressed, jumpReleased;
    [NonSerialized] public bool _prevJump;

    public Vector2 DirectionVector =>
        new Vector2((right ? 1f : 0f) - (left ? 1f : 0f),
                    (up    ? 1f : 0f) - (down ? 1f : 0f));

    public override string ToString() =>
        $"[P{slot}/{playerName}] slider={sliderValue:F0} msg={lastMessage} action={actionDown}";
}

// -------------------------------------------------------
// HostClient
// -------------------------------------------------------
public class HostClient : MonoBehaviour
{
    [Header("Relay")]
    public string relayHost = "localhost";
    public int    relayPort = 3010;
    public bool   useSecure = false;
    public bool   isRemoted = false;

    // ---- Public state ----
    public string RoomCode { get; private set; }

    /// All connected players, keyed by server-assigned id
    public readonly Dictionary<string, PlayerInputState> Players = new();

    /// Quick slot lookup  —  slot 1-4 → PlayerInputState (null if not connected)
    public readonly Dictionary<int, PlayerInputState> Slots = new();

    /// Legacy name lookup
    public readonly Dictionary<string, string> players = new();

    // ---- Events ----
    public event Action<string, string, int>          PlayerJoined;   // (id, name, slot)
    public event Action<string, int>                  PlayerLeft;     // (id, slot)
    public event Action<PlayerInputState, string, string> ButtonEvent; // (player, btn, state)
    public event Action<PlayerInputState, float>      SliderEvent;    // (player, value)
    public event Action<PlayerInputState, string>     MessageEvent;   // (player, text)
    public event Action<PlayerInputState, string>     ActionEvent;    // (player, state "down"|"up")

    // ---- Inspector hooks ----
    public PlayerInputRouter router;

    // ---- Internal ----
    WebSocket _ws;

    // ============================================================
    async void Start()
    {
        if (!isRemoted) relayHost = "localhost";
        await Connect();
    }

    async Task Connect()
    {
        string scheme = useSecure ? "wss" : "ws";
        string url    = $"{scheme}://{relayHost}:{relayPort}/ws?role=host";

        _ws = new WebSocket(url);

        _ws.OnOpen  += ()  => Debug.Log("[HostClient] Connected to relay.");
        _ws.OnError += (e) => Debug.LogError("[HostClient] WS Error: " + e);
        _ws.OnClose += (c) => Debug.LogWarning("[HostClient] WS closed: " + c);

        _ws.OnMessage += (bytes) =>
        {
            string json = Encoding.UTF8.GetString(bytes);
            var msg = JsonUtility.FromJson<MsgBase>(json);
            HandleMessage(msg);
        };

        await _ws.Connect();
    }

    // ============================================================
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _ws?.DispatchMessageQueue();
#endif

        foreach (var p in Players.Values)
        {
            // p2 message
            p.newMessage         = p._newMessagePending;
            p._newMessagePending = false;
            // Jump / legacy
            p.jumpPressed  = p.jump && !p._prevJump;
            p.jumpReleased = !p.jump && p._prevJump;
            p._prevJump    = p.jump;

            // Action button edge detection
            p.actionPressed  = p.actionDown && !p._prevAction;
            p.actionReleased = !p.actionDown && p._prevAction;
            p._prevAction    = p.actionDown;

            // Slider changed flag — cleared each frame after being set by HandleMessage
            // (already set in HandleMessage; we clear it here after one frame)
            // newMessage similarly
        }
    }

    void LateUpdate()
    {
        // Clear single-frame flags after they've been readable for one full Update()
        foreach (var p in Players.Values)
        {
            p.sliderChanged = false;
        }
    }

    // ============================================================
    void HandleMessage(MsgBase msg)
    {
        switch (msg.t)
        {
            // ---- Room ready ----
            case "room_created":
                RoomCode = msg.code;
                Debug.Log("[HostClient] Room: " + RoomCode);
                break;

            // ---- Player joined ----
            case "player_joined":
            {
                var state = new PlayerInputState
                {
                    playerId   = msg.id,
                    playerName = msg.name,
                    slot       = msg.slot
                };

                Players[msg.id] = state;
                Slots[msg.slot] = state;
                players[msg.id] = msg.name;

                Debug.Log($"[HostClient] JOIN id={msg.id} name={msg.name} slot={msg.slot}");

                router?.OnPlayerJoined(msg.id, msg.name, msg.slot);
                PlayerJoined?.Invoke(msg.id, msg.name, msg.slot);
                break;
            }

            // ---- Player left ----
            case "player_left":
            {
                if (Players.TryGetValue(msg.id, out var state))
                {
                    Slots.Remove(state.slot);
                    Players.Remove(msg.id);
                    players.Remove(msg.id);

                    Debug.Log($"[HostClient] LEFT id={msg.id} slot={state.slot}");

                    router?.OnPlayerLeft(msg.id, state.slot);
                    PlayerLeft?.Invoke(msg.id, state.slot);
                }
                break;
            }

            // ---- Slot 1: Slider ----
            case "slider":
            {
                if (!Players.TryGetValue(msg.id, out var player)) break;
                player.sliderValue  = msg.value;
                player.sliderChanged = true;
                router?.OnSliderInput(msg.id, msg.value);
                SliderEvent?.Invoke(player, msg.value);
                break;
            }

            // ---- Slot 2: Text message ----
            case "text_msg":
            {
                if (!Players.TryGetValue(msg.id, out var player)) break;
                player.lastMessage = msg.text;
                player._newMessagePending = true;
                Debug.Log($"[HostClient] MSG from {player.playerName}: {msg.text}");
                router?.OnTextMessage(msg.id, msg.text);
                MessageEvent?.Invoke(player, msg.text);

                // Example: forward this text to slot 4's display 
                SendTextToDisplay(msg.text, player.playerName);
                break;
            }

            // ---- Slot 3: Action button ----
            case "action_btn":
            {
                if (!Players.TryGetValue(msg.id, out var player)) break;
                player.actionDown   = msg.state == "down";
                player.totalPresses = msg.presses;
                router?.OnActionButton(msg.id, msg.state, msg.presses);
                ActionEvent?.Invoke(player, msg.state);
                break;
            }

            // ---- Legacy btn input ----
            case "btn":
            {
                if (!Players.TryGetValue(msg.id, out var player)) break;
                bool held = msg.state == "down" || msg.state == "hold";
                switch (msg.btn)
                {
                    case "ArrowUp":    player.up    = held; break;
                    case "ArrowDown":  player.down  = held; break;
                    case "ArrowLeft":  player.left  = held; break;
                    case "ArrowRight": player.right = held; break;
                    case "Jump":       player.jump  = held; break;
                }
                router?.OnButtonInput(msg.id, msg.btn, msg.state);
                ButtonEvent?.Invoke(player, msg.btn, msg.state);
                break;
            }

            case "error":
                Debug.LogError("[HostClient] Relay error: " + msg.reason);
                break;
        }
    }

    // ============================================================
    // Convenience: get player by slot
    public PlayerInputState GetSlot(int slot) =>
        Slots.TryGetValue(slot, out var p) ? p : null;

    // Send text to slot 4's display
    public async void SendTextToDisplay(string text, string from = "Host")
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        var payload = $"{{\"text\":\"{EscapeJson(text)}\",\"from\":\"{EscapeJson(from)}\"}}";
        var msg     = $"{{\"t\":\"send_to_slot\",\"slot\":4,\"payload\":{payload}}}";
        await _ws.SendText(msg);
    }

    // Broadcast a raw payload to all clients
    public async void BroadcastToClients(object payload)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        var wrapper = $"{{\"t\":\"broadcast_to_clients\",\"payload\":{JsonUtility.ToJson(payload)}}}";
        await _ws.SendText(wrapper);
    }

    static string EscapeJson(string s) =>
        s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    private void OnApplicationQuit() => _ws?.Close();
}