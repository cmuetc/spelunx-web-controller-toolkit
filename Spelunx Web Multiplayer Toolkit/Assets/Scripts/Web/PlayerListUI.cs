using System.Collections.Generic;
using UnityEngine;

public class PlayerListUI : MonoBehaviour
{
    [Header("Refs")]
    public HostClient host;           // drag your HostClient here
    public Transform contentRoot;     // e.g., a Vertical Layout Group
    public GameObject rowPrefab;      // prefab with PlayerRowUI on it

    private readonly Dictionary<string, PlayerRowUI> rows = new();

    void OnEnable()
    {
        if (!host) return;
        host.PlayerJoined += OnJoined;
        host.PlayerLeft   += OnLeft;

        // Build initial list if host already has players:
        RebuildFromSnapshot();
    }

    void OnDisable()
    {
        if (!host) return;
        host.PlayerJoined -= OnJoined;
        host.PlayerLeft   -= OnLeft;
    }

    void RebuildFromSnapshot()
    {
        foreach (Transform c in contentRoot) Destroy(c.gameObject);
        rows.Clear();

        foreach (var kv in host.players)
        {
            var go = Instantiate(rowPrefab, contentRoot);
            var ui = go.GetComponent<PlayerRowUI>();
            ui.Set(kv.Value, kv.Key);
            rows[kv.Key] = ui;
        }
    }

    void OnJoined(string id, string name)
    {
        if (rows.ContainsKey(id)) return;
        var go = Instantiate(rowPrefab, contentRoot);
        var ui = go.GetComponent<PlayerRowUI>();
        ui.Set(name, id);
        rows[id] = ui;
    }

    void OnLeft(string id)
    {
        if (rows.TryGetValue(id, out var ui) && ui)
        {
            Destroy(ui.gameObject);
            rows.Remove(id);
        }
    }
}