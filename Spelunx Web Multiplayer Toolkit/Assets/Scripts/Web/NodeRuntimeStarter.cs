using UnityEngine;
using System.Diagnostics;
using System.IO;

public class NodeRuntimeStarter : MonoBehaviour
{
    [Header("Path inside StreamingAssets to your server.js")]
    public string serverJsPath = "server/server.js";

    private Process nodeProcess;

    private HostClient hostClient;

    void Awake()
    {
        hostClient = FindObjectOfType<HostClient>();
#if !UNITY_EDITOR
        if(hostClient != null && !hostClient.isRemoted) StartNodeServer();
#endif
    }

    void OnApplicationQuit()
    {
        StopNodeServer();
    }

    private void StartNodeServer()
    {
        string absolutePath = Path.Combine(Application.streamingAssetsPath, serverJsPath);

        if (!File.Exists(absolutePath))
        {
            UnityEngine.Debug.LogError($"[NodeRuntimeStarter] server.js not found at {absolutePath}");
            return;
        }

        try
        {
            nodeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = $"\"{absolutePath}\"",
                    WorkingDirectory = Path.GetDirectoryName(absolutePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            nodeProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.Log($"[Node] {e.Data}");
            };
            nodeProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    UnityEngine.Debug.LogError($"[Node ERR] {e.Data}");
            };

            nodeProcess.Start();
            nodeProcess.BeginOutputReadLine();
            nodeProcess.BeginErrorReadLine();

            UnityEngine.Debug.Log("[NodeRuntimeStarter] Node server started in build.");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[NodeRuntimeStarter] Failed to start Node: {ex.Message}");
        }
    }

    private void StopNodeServer()
    {
        if (nodeProcess != null && !nodeProcess.HasExited)
        {
            try
            {
                nodeProcess.Kill();
                nodeProcess.Dispose();
                UnityEngine.Debug.Log("[NodeRuntimeStarter] Node server stopped.");
            }
            catch { }
            finally
            {
                nodeProcess = null;
            }
        }
    }
}
