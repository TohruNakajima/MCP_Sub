using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automatically adds required package references (UniTask) to manifest.json
/// when the Unity Editor starts. This script runs once via [InitializeOnLoadMethod] and skips
/// if the packages are already present.
/// NMCP is now bundled directly in the TozawaMCP submodule and no longer requires a package reference.
/// </summary>
[InitializeOnLoad]
internal static class McpPackageInstaller
{
    private const string UniTaskPackageId = "com.cysharp.unitask";
    private const string UniTaskUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";

    private const string NuGetForUnityPackageId = "com.github-glitchenzo.nugetforunity";

    // 旧パッケージ参照（サブモジュール統合済みのため自動削除対象）
    private const string OldNmcpPackageId = "jp.notargs.unity-natural-mcp";

    static McpPackageInstaller()
    {
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Debug.LogError("[McpPackageInstaller] manifest.json not found: " + manifestPath);
            return;
        }

        var json = File.ReadAllText(manifestPath);
        var modified = false;

        // Check for NuGetForUnity and warn
        if (json.Contains(NuGetForUnityPackageId))
        {
            Debug.LogWarning(
                "[McpPackageInstaller] NuGetForUnity is still in manifest.json. " +
                "MCP DLLs are now managed directly by TozawaMCP-Toolkit. " +
                "Consider removing the NuGetForUnity reference to avoid conflicts.");
        }

        // Add UniTask if not present
        if (!json.Contains(UniTaskPackageId))
        {
            json = AddPackageToManifest(json, UniTaskPackageId, UniTaskUrl);
            modified = true;
            Debug.Log("[McpPackageInstaller] Added UniTask to manifest.json");
        }

        // Remove old NMCP package reference if still present (now bundled in submodule)
        if (json.Contains(OldNmcpPackageId))
        {
            var lines = json.Split('\n');
            var filtered = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                if (!line.Contains(OldNmcpPackageId))
                    filtered.AppendLine(line);
            }
            json = filtered.ToString().TrimEnd() + "\n";
            modified = true;
            Debug.Log("[McpPackageInstaller] Removed old NMCP package reference (now bundled in TozawaMCP submodule)");
        }

        if (modified)
        {
            File.WriteAllText(manifestPath, json);
            Debug.Log("[McpPackageInstaller] manifest.json updated. Unity will now resolve packages...");
            AssetDatabase.Refresh();
        }
    }

    private static string AddPackageToManifest(string json, string packageId, string url)
    {
        // Find the "dependencies" block and insert the new entry
        var dependenciesKey = "\"dependencies\"";
        var idx = json.IndexOf(dependenciesKey);
        if (idx < 0)
        {
            Debug.LogError("[McpPackageInstaller] Could not find 'dependencies' in manifest.json");
            return json;
        }

        // Find the opening brace of the dependencies object
        var braceIdx = json.IndexOf('{', idx + dependenciesKey.Length);
        if (braceIdx < 0)
        {
            Debug.LogError("[McpPackageInstaller] Malformed manifest.json");
            return json;
        }

        var entry = $"\n    \"{packageId}\": \"{url}\",";
        return json.Insert(braceIdx + 1, entry);
    }
}
