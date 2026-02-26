using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

public class NodeAutoRunner : ScriptableObject
{
    [Header("Assign your server.js here")]
    public DefaultAsset serverJs; // Drag server.js into this field in Inspector

    public bool isRemoted = false;

    private static Process nodeProcess;

    private static NodeAutoRunner _config;
    private static NodeAutoRunner Config
    {
        get
        {
            if (_config == null)
            {
                // Load or create a config asset in ProjectSettings
                var assets = AssetDatabase.FindAssets("t:NodeAutoRunner");
                if (assets.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(assets[0]);
                    _config = AssetDatabase.LoadAssetAtPath<NodeAutoRunner>(path);
                }
                else
                {
                    _config = ScriptableObject.CreateInstance<NodeAutoRunner>();
                    if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                        AssetDatabase.CreateFolder("Assets", "Editor");
                    AssetDatabase.CreateAsset(_config, "Assets/Editor/NodeAutoRunnerConfig.asset");
                    AssetDatabase.SaveAssets();
                }
            }
            return _config;
        }
    }

    [InitializeOnLoadMethod]
    private static void SetupPlayModeWatcher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) StartNodeServer();
        else if (state == PlayModeStateChange.ExitingPlayMode) StopNodeServer();
    }

    private static void StartNodeServer()
    {
        if (Config.isRemoted) return;
        if (Config.serverJs == null)
        {
            UnityEngine.Debug.LogWarning("NodeAutoRunner: No server.js assigned in NodeAutoRunnerConfig.asset");
            return;
        }

        string relativePath = AssetDatabase.GetAssetPath(Config.serverJs);
        string absolutePath = Path.GetFullPath(relativePath);

        if (!File.Exists(absolutePath))
        {
            UnityEngine.Debug.LogError($"NodeAutoRunner: server.js not found at {absolutePath}");
            return;
        }

        if (nodeProcess == null || nodeProcess.HasExited)
        {
            nodeProcess = new Process();
            nodeProcess.StartInfo.FileName = "node"; // full path if Node isn’t in PATH
            nodeProcess.StartInfo.Arguments = $"\"{absolutePath}\"";
            nodeProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(absolutePath);
            nodeProcess.StartInfo.UseShellExecute = false;
            nodeProcess.StartInfo.RedirectStandardOutput = true;
            nodeProcess.StartInfo.RedirectStandardError = true;
            nodeProcess.StartInfo.CreateNoWindow = true;

            nodeProcess.OutputDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.Log($"[Node] {e.Data}");
            };
            nodeProcess.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data)) UnityEngine.Debug.LogError($"[Node ERR] {e.Data}");
            };

            nodeProcess.Start();
            nodeProcess.BeginOutputReadLine();
            nodeProcess.BeginErrorReadLine();

            UnityEngine.Debug.Log("NodeAutoRunner: Node server started.");
        }
    }

    private static void StopNodeServer()
    {
        if (nodeProcess != null && !nodeProcess.HasExited)
        {
            try
            {
                nodeProcess.Kill();
                nodeProcess.Dispose();
                UnityEngine.Debug.Log("NodeAutoRunner: Node server stopped.");
            }
            catch { /* ignore */ }
            finally
            {
                nodeProcess = null;
            }
        }
    }
}