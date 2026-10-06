using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChefShow.Editor
{
    /// <summary>Только соединение с Editor. Не создаёт и не регенерирует игровые объекты.</summary>
    [InitializeOnLoad]
    public static class UnityMcpConnection
    {
        public const string ServerUrl = "http://127.0.0.1:8766";
        private const string LocalFolder = ".local-tools/unity-mcp";
        private static bool connecting;
        private static double nextAttempt;

        static UnityMcpConnection() => EditorApplication.update += CheckConnection;

        private static void CheckConnection()
        {
            if (!File.Exists(LocalFolder + "/enabled") || connecting || EditorApplication.timeSinceStartup < nextAttempt
                || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextAttempt = EditorApplication.timeSinceStartup + 15;
            var services = FindType("MCPForUnity.Editor.Services.MCPServiceLocator");
            if (services == null) return; // Пакет ещё импортируется или удалён автором.
            var bridge = services.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (bridge == null || (bool)bridge.GetType().GetProperty("IsRunning").GetValue(bridge)) return;
            _ = ConnectAsync(bridge);
        }

        [MenuItem("Tools/Chef Show/MCP/Connect Local Server")]
        public static void Connect()
        {
            Directory.CreateDirectory(LocalFolder);
            File.WriteAllText(LocalFolder + "/enabled", "Unity MCP 10.0.0; local Editor connection\n");
            nextAttempt = 0;
            CheckConnection();
        }

        private static async Task ConnectAsync(object bridge)
        {
            connecting = true;
            try
            {
                if (!await ServerHealthy())
                {
                    string executable = Path.GetFullPath(LocalFolder + "/venv/Scripts/mcp-for-unity.exe");
                    if (!File.Exists(executable)) throw new IOException("Сначала tools/unity-mcp/install-server.ps1.");
                    var process = new ProcessStartInfo(executable,
                        "--transport http --http-url " + ServerUrl + " --project-scoped-tools") {
                        WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                        UseShellExecute = false, CreateNoWindow = true
                    };
                    process.EnvironmentVariables["UNITY_MCP_DISABLE_TELEMETRY"] = "1";
                    process.EnvironmentVariables["UNITY_MCP_LOG_DIR"] = Path.GetFullPath("TestResults/unity-mcp-logs");
                    Process.Start(process);
                    for (int i = 0; i < 30 && !await ServerHealthy(); i++) await Task.Delay(500);
                    if (!await ServerHealthy()) throw new IOException("Локальный MCP server не отвечает: " + ServerUrl);
                }
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
                EditorPrefs.SetString("MCPForUnity.HttpUrl", ServerUrl);
                EditorPrefs.SetString("MCPForUnity.UvxPath", Path.GetFullPath(LocalFolder + "/venv/Scripts/uvx.exe"));
                EditorPrefs.SetBool("MCPForUnity.TelemetryDisabled", true);
                EditorPrefs.SetBool("MCPForUnity.ProjectScopedTools.LocalHttp", true);
                var cache = FindType("MCPForUnity.Editor.Services.EditorConfigurationCache");
                var instance = cache.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                cache.GetMethod("Refresh").Invoke(instance, null);
                bool connected = await (Task<bool>)bridge.GetType().GetMethod("StartAsync").Invoke(bridge, null);
                if (!connected) throw new IOException("Bridge.StartAsync вернул false.");
                Directory.CreateDirectory("TestResults");
                var scene = SceneManager.GetActiveScene();
                File.WriteAllText("TestResults/unity-mcp-connected.txt", DateTime.UtcNow.ToString("O") + "\n" + ServerUrl
                    + "/mcp\n" + scene.path + "\nEdit Mode; scene dirty=" + scene.isDirty);
                UnityEngine.Debug.Log("Chef Show MCP: connected to " + ServerUrl + ". Рабочая сцена не изменена подключением.");
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("TestResults");
                File.WriteAllText("TestResults/unity-mcp-connection-error.txt", exception.ToString());
                UnityEngine.Debug.LogWarning("Chef Show MCP: " + exception.Message);
            }
            finally { connecting = false; }
        }

        private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        private static async Task<bool> ServerHealthy()
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) })
                using (var response = await client.GetAsync(ServerUrl + "/health"))
                    return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
