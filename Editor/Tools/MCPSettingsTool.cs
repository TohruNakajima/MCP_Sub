using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityNaturalMCP.Editor;

/// <summary>
/// Tool to get and set Unity Natural MCP settings.
/// </summary>
[McpServerToolType, Description("Get and set Unity Natural MCP settings")]
internal sealed class MCPSettingsTool
{
    [McpServerTool, Description("Get current MCP settings (port, ipAddress, etc.)")]
    public async ValueTask<string> GetMCPSettings()
    {
        await UniTask.SwitchToMainThread();
        var settings = MCPSetting.instance;
        return $"IP Address: {settings.ipAddress}\nPort: {settings.port}\nShow MCP Server Log: {settings.showMcpServerLog}\nEnable Default MCP Tools: {settings.enableDefaultMcpTools}";
    }

    [McpServerTool, Description("Set MCP port number")]
    public async ValueTask<string> SetMCPPort(int port)
    {
        await UniTask.SwitchToMainThread();

        if (port < 1 || port > 65535)
            return $"Invalid port number: {port}. Must be between 1 and 65535.";

        var settings = MCPSetting.instance;
        settings.port = port;
        settings.Save();

        return $"MCP port changed to {port}. Please restart Unity Editor for changes to take effect.";
    }

    [McpServerTool, Description("Set MCP IP address")]
    public async ValueTask<string> SetMCPIPAddress(string ipAddress)
    {
        await UniTask.SwitchToMainThread();

        if (string.IsNullOrWhiteSpace(ipAddress))
            return "IP Address cannot be empty.";

        var settings = MCPSetting.instance;
        settings.ipAddress = ipAddress;
        settings.Save();

        return $"MCP IP address changed to {ipAddress}. Please restart Unity Editor for changes to take effect.";
    }
}
