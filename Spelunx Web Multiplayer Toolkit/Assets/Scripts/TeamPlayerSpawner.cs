// TeamPlayerSpawner.cs
// Attach to any persistent GameObject alongside (or referencing) HostClient.
// Spawns one TeamPlayer per team when the first player on that team joins.
// Despawns the team player when the last player on that team leaves.

using System.Collections.Generic;
using UnityEngine;

public class TeamPlayerSpawner : MonoBehaviour
{
    [Header("References")]
    public HostClient hostClient;

    [Header("Prefab")]
    [Tooltip("Assign your player prefab here — must have TeamPlayer + Rigidbody")]
    public GameObject playerPrefab;

    [Header("Spawn Points")]
    [Tooltip("Assign 3 transforms: index 0 = red, 1 = blue, 2 = green")]
    public Transform[] spawnPoints = new Transform[3];

    // ---- Internal ----
    // One active TeamPlayer per team
    private readonly Dictionary<string, TeamPlayer> _teamPlayers = new();

    static readonly string[] Teams = { "red", "blue", "green" };

    // ============================================================
    void Awake()
    {
        if (hostClient == null) hostClient = GetComponent<HostClient>();
    }

    void OnEnable()
    {
        hostClient.PlayerJoined += OnPlayerJoined;
        hostClient.PlayerLeft   += OnPlayerLeft;
    }

    void OnDisable()
    {
        hostClient.PlayerJoined -= OnPlayerJoined;
        hostClient.PlayerLeft   -= OnPlayerLeft;
    }

    // ============================================================
    void OnPlayerJoined(string id, string name)
    {
        // Look up team from HostClient.Players
        if (!hostClient.Players.TryGetValue(id, out var state)) return;
        string team = state.team;

        // Already have a player for this team — nothing to do
        if (_teamPlayers.ContainsKey(team)) return;

        SpawnTeamPlayer(team);
    }

    void OnPlayerLeft(string id)
    {
        // Find which team this player was on by scanning Players
        // (they're already removed from the dict, so check all teams)
        foreach (string team in Teams)
        {
            if (_teamPlayers.ContainsKey(team) && TeamIsEmpty(team))
            {
                DespawnTeamPlayer(team);
            }
        }
    }

    // ============================================================
    void SpawnTeamPlayer(string team)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[Spawner] playerPrefab is not assigned!");
            return;
        }

        int index = TeamIndex(team);
        Transform spawnPoint = (spawnPoints != null && index < spawnPoints.Length && spawnPoints[index] != null)
            ? spawnPoints[index]
            : transform;   // fallback: spawn at spawner position

        GameObject go = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        TeamPlayer tp = go.GetComponent<TeamPlayer>();

        if (tp == null)
        {
            Debug.LogError("[Spawner] playerPrefab is missing a TeamPlayer component!");
            Destroy(go);
            return;
        }

        tp.Init(team, hostClient);
        _teamPlayers[team] = tp;

        Debug.Log($"[Spawner] Spawned player for team {team}");
    }

    void DespawnTeamPlayer(string team)
    {
        if (_teamPlayers.TryGetValue(team, out TeamPlayer tp))
        {
            Debug.Log($"[Spawner] Despawning player for team {team}");
            if (tp != null) Destroy(tp.gameObject);
            _teamPlayers.Remove(team);
        }
    }

    // ============================================================
    // True if no clients remain on this team
    bool TeamIsEmpty(string team)
    {
        foreach (var p in hostClient.Players.Values)
            if (p.team == team) return false;
        return true;
    }

    static int TeamIndex(string team) => team switch
    {
        "red"   => 0,
        "blue"  => 1,
        "green" => 2,
        _       => 0
    };

    // ---- Optional: public access ----
    public TeamPlayer GetTeamPlayer(string team) =>
        _teamPlayers.TryGetValue(team, out var tp) ? tp : null;
}
