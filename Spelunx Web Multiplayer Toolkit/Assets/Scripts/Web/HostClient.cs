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
    public string t;       // "room_created" | "player_joined" | "player_left" | "btn" | "error"
    public string code;    // room code
    public string id;      // player id
    public string name;    // player name
    public string team;    // "red" | "blue" | "green"
    public string btn;     // "ArrowUp" | "ArrowDown" | "ArrowLeft" | "ArrowRight" | "Jump"
    public string state;   // "down" | "hold" | "up"
    public string reason;  // error reason
}
 
// -------------------------------------------------------
// Per-player input state — read this from PlayerInputRouter
// -------------------------------------------------------
[Serializable]
public class PlayerInputState
{
    public string playerId;
    public string playerName;
    public string team;         // "red" | "blue" | "green"
 
    // Held state — true while button is down or holding
    public bool up;
    public bool down;
    public bool left;
    public bool right;
    public bool jump;
 
    // Edge detection — true for exactly one frame
    public bool jumpPressed;    // first frame of jump press
    public bool jumpReleased;   // first frame after jump release
 
    // Internal: previous frame jump state
    [NonSerialized] public bool _prevJump;
 
    /// Normalized direction vector: x = left(-1)/right(+1), y = down(-1)/up(+1)
    public Vector2 DirectionVector =>
        new Vector2((right ? 1f : 0f) - (left ? 1f : 0f),
                    (up    ? 1f : 0f) - (down ? 1f : 0f));
 
    public override string ToString() =>
        $"[{playerName}/{team}] U:{up} D:{down} L:{left} R:{right} J:{jump}";
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
 
    /// Legacy name lookup — kept for backward compatibility
    public readonly Dictionary<string, string> players = new(); // id -> name
 
    // ---- Events ----
    public event Action<string, string>         PlayerJoined;   // (id, name)
    public event Action<string>                 PlayerLeft;     // (id)
    public event Action<PlayerInputState, string, string> ButtonEvent; // (player, btn, state)
 
    // ---- Inspector hooks ----
    public PlayerInputRouter router;
 
    // ---- Internal ----
    WebSocket _ws;
    private int redCount, blueCount, greenCount;
 
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
 
        // Update per-frame edge detection for every player
        foreach (var p in Players.Values)
        {
            p.jumpPressed  = p.jump && !p._prevJump;
            p.jumpReleased = !p.jump && p._prevJump;
            p._prevJump    = p.jump;
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
                    team       = msg.team
                };
 
                Players[msg.id] = state;
                players[msg.id] = msg.name;   // legacy dict
 
                if      (msg.team == "red")   redCount++;
                else if (msg.team == "blue")  blueCount++;
                else if (msg.team == "green") greenCount++;
 
                Debug.Log($"[HostClient] JOIN {msg.id} {msg.name} (team={msg.team})");
 
                router?.OnPlayerJoined(msg.id, msg.name, msg.team);
                PlayerJoined?.Invoke(msg.id, msg.name);
                break;
            }
 
            // ---- Player left ----
            case "player_left":
            {
                if (Players.TryGetValue(msg.id, out var state))
                {
                    // decrement team counter using stored team (server may omit it on leave)
                    string t = state.team;
                    if      (t == "red")   redCount   = Mathf.Max(0, redCount   - 1);
                    else if (t == "blue")  blueCount  = Mathf.Max(0, blueCount  - 1);
                    else if (t == "green") greenCount = Mathf.Max(0, greenCount - 1);
 
                    Players.Remove(msg.id);
                    players.Remove(msg.id);
 
                    Debug.Log($"[HostClient] LEFT {msg.id} ({t})");
 
                    router?.OnPlayerLeft(msg.id);
                    PlayerLeft?.Invoke(msg.id);
                }
                break;
            }
 
            // ---- Button input ----
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
 
            // ---- Error ----
            case "error":
                Debug.LogError("[HostClient] Relay error: " + msg.reason);
                break;
        }
    }
 
    // ============================================================
    // Broadcast a message to all connected clients
    public async void BroadcastToClients(object payload)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        var wrapper = $"{{\"t\":\"broadcast_to_clients\",\"payload\":{JsonUtility.ToJson(payload)}}}";
        await _ws.SendText(wrapper);
    }
 
    // Team helpers
    public List<PlayerInputState> GetTeam(string team)
    {
        var list = new List<PlayerInputState>();
        foreach (var p in Players.Values)
            if (p.team == team) list.Add(p);
        return list;
    }
 
    private void OnApplicationQuit() => _ws?.Close();
}