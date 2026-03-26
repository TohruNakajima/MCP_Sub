using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automatically adds required package references (UniTask, UnityNaturalMCP) to manifest.json
/// when the Unity Editor starts. This script runs once via [InitializeOnLoadMethod] and skips
/// if the packages are already present.
/// </summary>
[InitializeOnLoad]
internal static class McpPackageInstaller
{
    private const string UniTaskPackageId = "com.cysharp.unitask";
    private const string UniTaskUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";

    private const string NmcpPackageId = "jp.notargs.unity-natural-mcp";
    private const string NmcpUrl = "https://github.com/nakagimadevelop002-glitch/UnityNaturalMCP.git?path=/UnityNaturalMCPServer";

    private const string NuGetForUnityPackageId = "com.github-glitchenzo.nugetforunity";

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

        // Add forked UnityNaturalMCP if not present
        if (!json.Contains(NmcpPackageId))
        {
            json = AddPackageToManifest(json, NmcpPackageId, NmcpUrl);
            modified = true;
            Debug.Log("[McpPackageInstaller] Added UnityNaturalMCP (fork) to manifest.json");
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
