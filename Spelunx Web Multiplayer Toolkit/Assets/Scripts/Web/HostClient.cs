using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;
using TMPro;

[Serializable] public class MsgBase { public string t; public string code; public string id; public string name; public string btn; public int energy; public string reason; public string team; }
public class HostClient : MonoBehaviour
{
    [Header("Relay")]
    public string relayHost = "localhost";
    public int relayPort = 3010;
    public bool useSecure = false; 

    public bool isRemoted = false;

    WebSocket ws;
    public string RoomCode { get; private set; }

    public event Action<string, string> PlayerJoined; // (id, name)
    public event Action<string> PlayerLeft;          // (id)
    public readonly Dictionary<string, string> players = new(); // id -> name

    public PlayerInputRouter router;

    // Track how many of each team have joined
    private int redCount = 0;
    private int blueCount = 0;
    private int greenCount = 0;


    async void Start() 
    { 
        if(!isRemoted) relayHost = "localhost";
        await Connect(); 
    }

    async Task Connect()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string scheme = useSecure ? "wss" : "ws";
#else
        string scheme = useSecure ? "wss" : "ws";
#endif
        var url = $"{scheme}://{relayHost}:{relayPort}/ws?role=host";
        ws = new WebSocket(url);

        ws.OnOpen += () => Debug.Log("Host connected to relay.");
        ws.OnError += (e) => Debug.LogError("WS Error: " + e);
        ws.OnClose += (c) => Debug.LogWarning("WS closed: " + c);

        ws.OnMessage += (bytes) =>
        {
            var json = Encoding.UTF8.GetString(bytes);
            var msg = JsonUtility.FromJson<MsgBase>(json);

            switch (msg.t)
            {
                case "room_created":
                    RoomCode = msg.code;
                    Debug.Log("Room: " + RoomCode);
                    break;
                case "player_joined":
                    //AssignTeamIfNeeded(msg);
                    players[msg.id] = msg.name;
                    Debug.Log($"JOIN {msg.id} {msg.name} (Team={msg.team})");

                    router?.OnPlayerJoined(msg.id, msg.name, msg.team);
                    PlayerJoined?.Invoke(msg.id, msg.name);
                    break;
                case "player_left":
                    if (players.Remove(msg.id))
                    {
                        Debug.Log($"LEFT {msg.id}");
                        router?.OnPlayerLeft(msg.id);
                        PlayerLeft?.Invoke(msg.id);

                        if (msg.team == "red") redCount = Mathf.Max(0, redCount - 1);
                        else if (msg.team == "blue") blueCount = Mathf.Max(0, blueCount - 1);
                        else if (msg.team == "green") greenCount = Mathf.Max(0, greenCount - 1);
                    }
                    break;
                case "btn":
                    // msg.id (player), msg.btn, msg.state ("down"/"up")
                    HandleEnergy(msg);
                    break;
                case "error":
                    Debug.LogError("Relay error: " + msg.reason);
                    break;
            }
        };

        await ws.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    private void OnApplicationQuit()
    {
        ws?.Close();
    }

    // === TEAM ASSIGNMENT LOGIC ===
    private void AssignTeamIfNeeded(MsgBase msg)
    {
        if (string.IsNullOrEmpty(msg.team))
        {
            if (redCount <= blueCount && redCount <= greenCount)
            {
                msg.team = "red";
                redCount++;
            }
            else if (blueCount <= greenCount)
            {
                msg.team = "blue";
                blueCount++;
            }
            else
            {
                msg.team = "green";
                greenCount++;
            }
        }
    }

    // === Your game hooks ===
    void HandleEnergy(MsgBase msg)
    {
        router?.OnEnergyUpdate(msg.id, msg.btn, msg.energy);
    }


    // Optionally broadcast to all clients (e.g., "Game starting in 3...")
    public async void BroadcastToClients(object payload)
    {
        if (ws == null || ws.State != WebSocketState.Open) return;
        var obj = new { t = "broadcast_to_clients", payload };
        await ws.SendText(JsonUtility.ToJson(obj));
    }
    
}
