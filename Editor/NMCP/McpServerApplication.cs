using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

namespace UnityNaturalMCP.Editor
{
    internal sealed class McpServerApplication : IDisposable
    {
        private readonly HttpListener _httpListener = new();

        public void Dispose()
        {
            _httpListener.Close();
        }

        public async UniTask Run(CancellationToken token)
        {
            var setting = MCPSetting.instance;
            var mcpEntPoint = $"http://{setting.ipAddress}:{setting.port}/mcp/";
            _httpListener.Prefixes.Add(mcpEntPoint);

            // ポート解放待ちリトライ（リコンパイル時の競合回避）
            for (int retry = 0; retry < 5; retry++)
            {
                try
                {
                    _httpListener.Start();
                    break;
                }
                catch (System.Net.Sockets.SocketException) when (retry < 4)
                {
                    await UniTask.WaitForEndOfFrame();
                }
            }
            if (setting.showMcpServerLog)
            {
                Debug.Log($"Started MCP server at {mcpEntPoint}");
            }

            Pipe clientToServerPipe = new();
            Pipe serverToClientPipe = new();

            var builder = new ServiceCollection()
                .AddMcpServer()
                .WithStreamServerTransport(
                    clientToServerPipe.Reader.AsStream(),
                    serverToClientPipe.Writer.AsStream());

            if (setting.enableDefaultMcpTools)
            {
                builder.WithToolsFromAssembly();

                if (setting.showMcpServerLog)
                {
                    Debug.Log($"Loaded default MCP tools");
                }
            }

            var mcpBuilderScriptableObjects = AssetDatabase.FindAssets("t:McpBuilderScriptableObject");
            foreach (var guid in mcpBuilderScriptableObjects)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var scriptableObject = AssetDatabase.LoadAssetAtPath<McpBuilderScriptableObject>(path);
                if (scriptableObject != null)
                {
                    scriptableObject.Build(builder);
                    if (setting.showMcpServerLog)
                    {
                        Debug.Log($"Loaded MCP tool builder: {scriptableObject.name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to load MCP tool builder at path: {path}");
                }
            }

            await using var services = builder.Services.BuildServiceProvider();

            HandleHttpRequestAsync(_httpListener, clientToServerPipe, serverToClientPipe, token).Forget();

            var mcpServer = services.GetRequiredService<IMcpServer>();
            await mcpServer.RunAsync(token);
        }

        private static async UniTask HandleHttpRequestAsync(HttpListener listener, Pipe clientToServerPipe,
            Pipe serverToClientPipe, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    // リクエスト取得
                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    switch (request.HttpMethod)
                    {
                        case "POST":
                        {
                            using var inputReader = new StreamReader(request.InputStream, Encoding.UTF8);
                            var inputBody = await inputReader.ReadLineAsync();
                            var inputBodyJson = JsonNode.Parse(inputBody);
                            if (inputBodyJson?["method"]?.ToString() != "notifications/initialized")
                            {
                                await clientToServerPipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(inputBody + "\n"),
                                    token);

                                var result = await serverToClientPipe.Reader.ReadAsync(token);
                                var buffer = result.Buffer;

                                var resultBody = Encoding.UTF8.GetString(buffer.ToArray());
                                serverToClientPipe.Reader.AdvanceTo(buffer.End);

                                response.ContentType = "application/json";
                                await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(resultBody + "\n"),
                                    token);
                            }
                            response.Close();

                            break;
                        }
                        case "GET":
                        {
                            var text = Encoding.UTF8.GetBytes(@"{
    ""jsonrpc"": ""2.0"",
    ""error"": {
        ""code"": -32000,
        ""message"": ""Method not allowed.""
    },
    ""id"": null
}");
                            await response.OutputStream.WriteAsync(text, 0, text.Length, token);
                            response.Close();
                            break;
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Ignore for cancellation
            }
        }
    }
}