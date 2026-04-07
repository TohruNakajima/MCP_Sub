using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityNaturalMCP.Editor.McpTools;

namespace UnityNaturalMCP.Editor
{
    internal static class McpServerRunner
    {
        private static CancellationTokenSource _cancellationTokenSource;
        private static McpServerApplication _mcpServerApplication;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            Cleanup();

            // ポート解放を待つため遅延起動
            EditorApplication.delayCall += StartServer;

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void StartServer()
        {
            // 既に起動済み or リロード中なら何もしない
            if (_mcpServerApplication != null) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource.AddTo(Application.exitCancellationToken);
            _mcpServerApplication = new McpServerApplication();
            _mcpServerApplication.Run(_cancellationTokenSource.Token).Forget();
        }

        private static void OnBeforeAssemblyReload()
        {
            Cleanup();
        }

        private static void Cleanup()
        {
            var cts = _cancellationTokenSource;
            _cancellationTokenSource = null;
            if (cts != null)
            {
                try { cts.Cancel(); } catch (ObjectDisposedException) { }
                try { cts.Dispose(); } catch (ObjectDisposedException) { }
            }

            var app = _mcpServerApplication;
            _mcpServerApplication = null;
            if (app != null)
            {
                try { app.Dispose(); } catch (Exception) { }
            }
        }

        public static void RefreshMcpServer()
        {
            Cleanup();

            _cancellationTokenSource = new CancellationTokenSource();
            _mcpServerApplication = new McpServerApplication();
            _mcpServerApplication.Run(_cancellationTokenSource.Token).Forget();
        }
    }
}
