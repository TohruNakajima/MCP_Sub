using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GameObject削除専用MCPツール
/// </summary>
[McpServerToolType, Description("Delete GameObjects")]
internal sealed class GameObjectDeleteTool
{
    [McpServerTool, Description("Delete a GameObject by InstanceID. Use with # prefix (e.g., '#-471090').")]
    public async ValueTask<string> DeleteGameObjectByID(
        [Description("GameObject InstanceID with # prefix (e.g., '#-471090')")]
        string instanceID)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            GameObject obj = InspectorTool.GameObjectResolver.Resolve(instanceID);
            string objName = obj.name;

            Undo.DestroyObjectImmediate(obj);

            return $"Successfully deleted GameObject: '{objName}' (ID: {instanceID})";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error: {e.Message}";
        }
    }
}
