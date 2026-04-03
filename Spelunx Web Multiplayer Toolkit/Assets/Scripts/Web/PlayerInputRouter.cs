// PlayerInputRouter.cs  (updated for slot-based controllers)
// Inherit from this and override the methods you need.
// Attach the subclass to a GameObject and assign it to HostClient.router.
//
// Slot assignments (determined by join order):
//   Slot 1 — Slider controller
//   Slot 2 — Messenger (text input → sends to server + Player 4)
//   Slot 3 — Single action button
//   Slot 4 — Display (receives text from Player 2, read-only)

using UnityEngine;

public class PlayerInputRouter : MonoBehaviour
{
    public HostClient hostClient;

    protected virtual void Awake()
    {
        if (hostClient == null) hostClient = GetComponent<HostClient>();
    }

    // ---- Called by HostClient ----

    /// A new player connected. slot = 1–4
    public virtual void OnPlayerJoined(string id, string name, int slot)
    {
        Debug.Log($"[Router] Player joined: {name} → slot {slot}");
    }

    /// A player disconnected.
    public virtual void OnPlayerLeft(string id, int slot)
    {
        Debug.Log($"[Router] Player left: slot {slot} / id={id}");
    }

    // ---- Slot 1: Slider ----
    /// value: 0–100 (use SliderNormalized for 0–1)
    public virtual void OnSliderInput(string id, float value)
    {
        // e.g. control a volume, speed, power level
    }

    // ---- Slot 2: Messenger ----
    public virtual void OnTextMessage(string id, string text)
    {
        // text is automatically forwarded to Player 4's display by the server
        Debug.Log($"[Router] Message from {id}: {text}");
    }

    // ---- Slot 3: Action button ----
    /// state = "down" | "up", presses = cumulative count
    public virtual void OnActionButton(string id, string state, int presses)
    {
        Debug.Log($"[Router] Action {state} (total: {presses})");
    }

    // ---- Legacy d-pad (original controller) ----
    public virtual void OnButtonInput(string id, string btn, string state) { }

    // ---- Polling loop ----
    protected virtual void Update()
    {
        if (hostClient == null) return;

        // ---- Slot 1: Slider ----
        var p1 = hostClient.GetSlot(1);
        if (p1 != null && p1.sliderChanged)
        {
            // p1.sliderValue      → 0–100
            // p1.SliderNormalized → 0–1
        }

        // ---- Slot 2: Messenger ----
        var p2 = hostClient.GetSlot(2);
        if (p2 != null && p2.newMessage)
        {
            // p2.lastMessage → the text that was just sent
            // (also auto-forwarded to Player 4 by the server)
        }

        // ---- Slot 3: Action button ----
        var p3 = hostClient.GetSlot(3);
        if (p3 != null)
        {
            if (p3.actionPressed)
                Debug.Log($"{p3.playerName} fired! (press #{p3.totalPresses})");
            // p3.actionDown → held state
        }

        // ---- Slot 4: Display ----
        // Player 4 is receive-only; they show text sent by Player 2.
        // If you want to push text TO Player 4 from the host:
        //   hostClient.SendTextToDisplay("Hello!", "Host");

        // ---- All players (for any remaining polling needs) ----
        foreach (var player in hostClient.Players.Values)
        {
            // player.slot          → 1-4
            // player.DirectionVector → legacy d-pad Vector2
            // player.jumpPressed   → legacy jump
        }
    }
}