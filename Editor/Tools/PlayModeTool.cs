using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

[McpServerToolType, Description("Unity Editor の Play Mode 制御")]
internal sealed class PlayModeTool
{
    [McpServerTool, Description("Play Mode を開始する（既にPlay中なら何もしない）")]
    public async ValueTask<string> PlayMode_Start()
    {
        try
        {
            await UniTask.SwitchToMainThread();
            if (EditorApplication.isPlaying) return "Already in Play mode";
            EditorApplication.isPlaying = true;
            return "Play mode start requested";
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }

    [McpServerTool, Description("Play Mode を停止する（Edit Mode に戻る）")]
    public async ValueTask<string> PlayMode_Stop()
    {
        try
        {
            await UniTask.SwitchToMainThread();
            if (!EditorApplication.isPlaying) return "Not in Play mode";
            EditorApplication.isPlaying = false;
            return "Play mode stop requested";
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }

    [McpServerTool, Description("現在 Play Mode かどうかを返す ('Playing' or 'Edit')")]
    public async ValueTask<string> PlayMode_GetState()
    {
        try
        {
            await UniTask.SwitchToMainThread();
            return EditorApplication.isPlaying ? "Playing" : "Edit";
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }
}
