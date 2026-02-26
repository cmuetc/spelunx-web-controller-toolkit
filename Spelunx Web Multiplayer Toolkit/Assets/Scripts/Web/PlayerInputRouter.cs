using UnityEngine;
using System.Collections.Generic;
using static UnityEngine.EventSystems.EventTrigger;

public class PlayerInputRouter : MonoBehaviour
{
    private readonly Dictionary<string, PlayerState> _players = new();

    public class PlayerState
    {
        public string team;
        public string name;
        public int totalEnergy;   // total energy accumulated for that player
    }

    [Header("Energy Settings")]
    public float decayRatePerSecond = 3f; // energy lost per second
    public int minEnergy = 0;


    public event System.Action<string, string, int> OnPlayerTap;


    public void OnPlayerJoined(string playerId, string name, string team)
    {
        if (!_players.TryGetValue(playerId, out var ps))
            _players[playerId] = ps = new PlayerState();

        ps.team = team;
        ps.name = name;
        ps.totalEnergy = 0;
        Debug.Log($"[JOIN] {playerId} {name} assigned to team {team}");
    }

    public void OnPlayerLeft(string playerId)
    {
        if (_players.Remove(playerId))
            Debug.Log($"[LEAVE] {playerId} removed from router");
    }

    // === ENERGY UPDATE FROM CONTROLLER ===
    public void OnEnergyUpdate(string playerId, string btn, int energyPulse)
    {
        if (!_players.TryGetValue(playerId, out var ps))
            _players[playerId] = ps = new PlayerState();

        // Each tap gives "energyPulse" amount (not absolute)
        ps.totalEnergy += energyPulse;

        Debug.Log($"[ENERGY] {playerId} +{energyPulse} (Total={ps.totalEnergy}) (Team={ps.team})");

        //Trigger the energy ball
        OnPlayerTap?.Invoke(playerId, ps.team, energyPulse);
    }

    // === GETTERS ===

    public List<string> getPlayerNames(string team)
    {
        List<string> names = new List<string>();
        foreach (var kv in _players)
        {
            if (kv.Value.team == team)
            {
                names.Add(kv.Value.name);
            }
        }

        return names;
    }

    public List<string> GetAllPlayerIds()
    {
        return new List<string>(_players.Keys);
    }

}
