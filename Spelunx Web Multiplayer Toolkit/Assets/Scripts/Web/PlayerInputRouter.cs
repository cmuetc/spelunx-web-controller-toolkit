// PlayerInputRouter.cs
// Inherit from this and override the methods you need.
// Attach the subclass to a GameObject and assign it to HostClient.router in the Inspector.
//
// HostClient calls:
//   OnPlayerJoined(id, name, team)   — when a player connects
//   OnPlayerLeft(id)                 — when a player disconnects
//   OnButtonInput(id, btn, state)    — on every button event
//
// For most game logic you'll want to poll HostClient.Players[id] in Update()
// rather than reacting to every raw button event here.
 
using UnityEngine;
 
public class PlayerInputRouter : MonoBehaviour
{
    // Reference back to the host — set automatically if on the same GameObject,
    // or assign manually in the Inspector.
    public HostClient hostClient;
 
    protected virtual void Awake()
    {
        if (hostClient == null) hostClient = GetComponent<HostClient>();
    }
 
    // ---- Called by HostClient ----
 
    /// A new player connected. team = "red" | "blue" | "green"
    public virtual void OnPlayerJoined(string id, string name, string team)
    {
        Debug.Log($"[Router] Player joined: {name} ({team})");
    }
 
    /// A player disconnected.
    public virtual void OnPlayerLeft(string id)
    {
        Debug.Log($"[Router] Player left: {id}");
    }
 
    /// Raw button event.
    /// btn   = "ArrowUp" | "ArrowDown" | "ArrowLeft" | "ArrowRight" | "Jump"
    /// state = "down" | "hold" | "up"
    public virtual void OnButtonInput(string id, string btn, string state)
    {
        // Optional: react to individual button events.
        // For held-state polling, read hostClient.Players[id] in Update() instead.
    }
 
    // ---- Convenience: poll all players each frame ----
    protected virtual void Update()
    {
        if (hostClient == null) return;
 
        foreach (var player in hostClient.Players.Values)
        {
            // player.up / down / left / right / jump  — held booleans
            // player.jumpPressed                       — true this frame only
            // player.jumpReleased                      — true this frame only
            // player.DirectionVector                   — Vector2 (-1..1, -1..1)
            // player.team                              — "red" | "blue" | "green"
            // player.playerName                        — display name
 
            // Example: log direction while any key held
            if (player.DirectionVector.sqrMagnitude > 0)
                Debug.Log($"{player.playerName} dir={player.DirectionVector}");
 
            if (player.jumpPressed)
                Debug.Log($"{player.playerName} jumped!");
        }
    }
}