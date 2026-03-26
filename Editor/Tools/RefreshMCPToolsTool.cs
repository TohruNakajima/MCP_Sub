using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEngine;
using UnityEditor;

[McpServerToolType, Description("Automate MCP tools registration refresh with server restart")]
internal sealed class RefreshMCPToolsTool
{
    [McpServerTool, Description("Refresh assets, wait for compilation, and restart MCP server to reload tools - complete automation")]
    public async ValueTask<string> RefreshMCPTools()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            Debug.Log("[RefreshMCPTools] Step 1/3: Refreshing assets...");
            AssetDatabase.Refresh();

            Debug.Log("[RefreshMCPTools] Step 2/3: Waiting for compilation (2 seconds)...");
            await UniTask.Delay(2000);

            Debug.Log("[RefreshMCPTools] Step 3/3: Restarting MCP server...");
            RestartMcpServer();

            Debug.Log("[RefreshMCPTools] ✓ Complete - MCP tools refreshed and server restarted");
            return "SUCCESS: MCP tools refreshed and server restarted successfully";
        }
        catch (Exception e)
        {
            Debug.LogError($"[RefreshMCPTools] Failed: {e.Message}");
            Debug.LogError(e);
            throw;
        }
    }

    private void RestartMcpServer()
    {
        try
        {
            // Find McpServerRunner type via reflection
            var runnerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "McpServerRunner" && t.Namespace == "UnityNaturalMCP.Editor");

            if (runnerType == null)
            {
                throw new Exception("McpServerRunner type not found. Ensure UnityNaturalMCP is installed.");
            }

            // Call the public static RefreshMcpServer method directly
            var refreshMethod = runnerType.GetMethod("RefreshMcpServer", BindingFlags.Public | BindingFlags.Static);
            if (refreshMethod == null)
            {
                throw new Exception("McpServerRunner.RefreshMcpServer method not found.");
            }

            refreshMethod.Invoke(null, null);
            Debug.Log("[RefreshMCPTools] Server restarted via RefreshMcpServer() method");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RefreshMCPTools] Server restart failed: {e.Message}");
            Debug.LogWarning("[RefreshMCPTools] Please manually restart via: Edit > Project Settings > UnityNaturalMCP");
            throw;
        }
    }
}
