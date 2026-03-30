using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Projectウィンドウ操作のためのMCPツール
/// アセット選択、シーンロード等のProject Window操作を提供
/// </summary>
[McpServerToolType, Description("Project Window operations: asset selection, scene loading, etc.")]
internal sealed class ProjectTool
{
    /// <summary>Editor起動時のメインスレッドID（SwitchToMainThread デッドロック回避用）</summary>
    private static int s_mainThreadId;

    [UnityEditor.InitializeOnLoadMethod]
    private static void CaptureMainThreadId()
    {
        s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    private static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == s_mainThreadId;

    // ========== MCP Methods (Facade) ==========

    [McpServerTool, Description("Load a scene in the Unity Editor. Equivalent to double-clicking a scene file in the Project window.")]
    public async ValueTask<string> Proj_LoadScene(
        [Description("Path to the scene file (e.g. 'Assets/Scenes/MyScene.unity').")]
        string scenePath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // シーンファイルの存在確認
            if (!System.IO.File.Exists(scenePath))
                throw new ArgumentException($"Scene file not found: '{scenePath}'");

            // .unityファイルか確認
            if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Not a scene file: '{scenePath}' (must end with .unity)");

            // シーンロード
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            return $"Loaded scene: '{scene.name}' from '{scenePath}'";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Select an asset in the Project window. Equivalent to clicking an asset in the Project window.")]
    public async ValueTask<string> Proj_SelectAsset(
        [Description("Path to the asset (e.g. 'Assets/Prefabs/MyPrefab.prefab').")]
        string assetPath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // アセット読み込み
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                throw new ArgumentException($"Asset not found: '{assetPath}'");

            // Project Windowで選択
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            return $"Selected asset: '{asset.name}' at '{assetPath}'";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Save the active scene. Equivalent to 'File > Save' (Ctrl+S) in the Unity Editor.")]
    public async ValueTask<string> Proj_SaveScene()
    {
        try
        {
            if (!IsMainThread)
                await UniTask.SwitchToMainThread();

            // アクティブシーン取得
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            if (string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Active scene has no path. Please save the scene with a path first.");

            string sceneName = scene.name;
            string scenePath = scene.path;

            // シーン保存（同期処理）
            bool saved = EditorSceneManager.SaveScene(scene);

            if (!saved)
                throw new InvalidOperationException($"Failed to save scene: '{sceneName}'");

            // 保存完了を確実に待つ
            await UniTask.Yield();

            return $"Saved scene: '{sceneName}' at '{scenePath}'";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Save the active scene with a new path. Equivalent to 'File > Save As...' in the Unity Editor.")]
    public async ValueTask<string> Proj_SaveSceneAs(
        [Description("Path where to save the scene (e.g. 'Assets/Scenes/MyNewScene.unity').")]
        string scenePath)
    {
        try
        {
            if (!IsMainThread)
                await UniTask.SwitchToMainThread();

            // .unityファイルか確認
            if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid scene path: '{scenePath}' (must end with .unity)");

            // アクティブシーン取得
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // シーン保存
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);

            if (!saved)
                throw new InvalidOperationException($"Failed to save scene to: '{scenePath}'");

            return $"Saved scene as: '{scenePath}'";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Create a Prefab Variant from a GameObject in the scene. The GameObject must be a prefab instance.")]
    public async ValueTask<string> Proj_CreatePrefabVariant(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string gameObjectPath,
        [Description("Path where to save the prefab variant (e.g. 'Assets/Prefabs/MyVariant.prefab').")]
        string variantPrefabPath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // .prefabファイルか確認
            if (!variantPrefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid prefab path: '{variantPrefabPath}' (must end with .prefab)");

            // GameObjectを取得
            GameObject go = FindGameObject(gameObjectPath);
            if (go == null)
                throw new ArgumentException($"GameObject not found: '{gameObjectPath}'");

            // Prefabインスタンスかどうかで処理分岐
            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go);
            string originalPrefabPath = isPrefabInstance
                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)
                : null;

            // Prefabとして保存（インスタンスの場合はVariant、通常のGameObjectの場合は新規Prefab）
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, variantPrefabPath, InteractionMode.AutomatedAction);

            if (prefab == null)
                throw new InvalidOperationException($"Failed to create prefab at: '{variantPrefabPath}'");

            string message = isPrefabInstance
                ? $"Created prefab variant at '{variantPrefabPath}' (based on '{originalPrefabPath}')"
                : $"Created prefab at '{variantPrefabPath}'";

            return message;        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Rename an asset in the Project window. Equivalent to right-click > Rename in the Project window.")]
    public async ValueTask<string> Proj_RenameAsset(
        [Description("Path to the asset (e.g. 'Assets/Prefabs/OldName.prefab').")]
        string assetPath,
        [Description("New name for the asset (without path, e.g. 'NewName.prefab').")]
        string newName)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // アセット存在確認
            if (!System.IO.File.Exists(assetPath) && !System.IO.Directory.Exists(assetPath))
                throw new ArgumentException($"Asset not found: '{assetPath}'");

            // リネーム実行
            string errorMessage = AssetDatabase.RenameAsset(assetPath, newName);

            if (!string.IsNullOrEmpty(errorMessage))
                throw new InvalidOperationException($"Failed to rename asset: {errorMessage}");

            // 新しいパスを取得（拡張子を保持）
            string directory = System.IO.Path.GetDirectoryName(assetPath);
            string newPath = System.IO.Path.Combine(directory, newName);

            return $"Renamed asset from '{assetPath}' to '{newPath}'";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== Helper Methods ==========

    /// <summary>
    /// GameObjectをパスまたはInstanceIDで検索
    /// </summary>
    private GameObject FindGameObject(string path)
    {
        // InstanceID形式（#12345）
        if (path.StartsWith("#"))
        {
            if (int.TryParse(path.Substring(1), out int instanceId))
            {
#if UNITY_6000_0_OR_NEWER
#pragma warning disable CS0618
                var obj = EditorUtility.EntityIdToObject(instanceId);
#pragma warning restore CS0618
#else
                var obj = EditorUtility.InstanceIDToObject(instanceId);
#endif
                return obj as GameObject;
            }
            return null;
        }

        // パス形式（Canvas/Button）
        return GameObject.Find(path);
    }
}
