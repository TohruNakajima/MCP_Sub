using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PlayerSettings操作のためのMCPツール
/// WebGL Compression Format等のビルド設定を変更
/// </summary>
[McpServerToolType, Description("PlayerSettings operations: WebGL compression, build settings, etc.")]
internal sealed class PlayerSettingsTool
{
    [McpServerTool, Description("Get WebGL compression format. Returns: 0=Brotli, 1=Gzip, 2=Disabled")]
    public async ValueTask<string> GetWebGLCompressionFormat()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // WebGLCompressionFormat: 0=Brotli, 1=Gzip, 2=Disabled
            var format = PlayerSettings.WebGL.compressionFormat;
            return $"WebGL Compression Format: {(int)format} ({format})";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set WebGL compression format. Use: 0=Brotli, 1=Gzip, 2=Disabled")]
    public async ValueTask<string> SetWebGLCompressionFormat(
        [Description("Compression format: 0=Brotli, 1=Gzip, 2=Disabled")]
        int format)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (format < 0 || format > 2)
                throw new ArgumentException($"Invalid compression format: {format}. Must be 0 (Brotli), 1 (Gzip), or 2 (Disabled)");

            // WebGLCompressionFormat: 0=Brotli, 1=Gzip, 2=Disabled
            var compressionFormat = (WebGLCompressionFormat)format;
            PlayerSettings.WebGL.compressionFormat = compressionFormat;

            // 設定を保存
            AssetDatabase.SaveAssets();

            return $"Set WebGL Compression Format to: {format} ({compressionFormat})";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Get WebGL decompression fallback setting. Returns: true=enabled, false=disabled")]
    public async ValueTask<string> GetWebGLDecompressionFallback()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var fallback = PlayerSettings.WebGL.decompressionFallback;
            return $"WebGL Decompression Fallback: {fallback}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set WebGL decompression fallback. Use: true=enabled, false=disabled")]
    public async ValueTask<string> SetWebGLDecompressionFallback(
        [Description("Enable decompression fallback: true or false")]
        bool enabled)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            PlayerSettings.WebGL.decompressionFallback = enabled;

            // 設定を保存
            AssetDatabase.SaveAssets();

            return $"Set WebGL Decompression Fallback to: {enabled}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
