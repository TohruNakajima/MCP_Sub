using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hierarchyウィンドウ操作用MCPツール（GameObject作成、親子関係設定等）。
/// </summary>
[McpServerToolType, Description("Create and manage GameObjects in the Unity Hierarchy")]
internal sealed class CreateEmptyGameObjectTool
{
    [McpServerTool, Description("Create an empty GameObject in the active scene or prefab stage. Equivalent to 'GameObject > Create Empty' in the Unity Editor.")]
    public async ValueTask<string> CreateEmptyGameObject(
        [Description("The name of the new GameObject. Defaults to 'GameObject' if not specified.")]
        string name = "GameObject")
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var gameObject = new GameObject(name);

            // プレハブステージ内で作業中の場合、プレハブステージのルートを親に設定
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                gameObject.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(gameObject, $"Create Empty GameObject '{name}'");
            return $"Created empty GameObject: '{gameObject.name}' (InstanceID: {gameObject.GetInstanceID()})";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set parent-child relationship between GameObjects in Hierarchy. Equivalent to drag-and-drop in Hierarchy window.")]
    public async ValueTask<string> Hier_SetParent(
        [Description("The name of the child GameObject.")]
        string childName,
        [Description("The name of the parent GameObject. Use empty string or null to unparent (set to root).")]
        string parentName = null)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // 子GameObjectを検索（プレハブステージ対応）
            var child = InspectorTool.GameObjectResolver.Resolve(childName);

            // 親が指定されている場合は検索（プレハブステージ対応）
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(parentName))
            {
                var parent = InspectorTool.GameObjectResolver.Resolve(parentName);
                parentTransform = parent.transform;
            }

            // 親子関係を設定
            Undo.SetTransformParent(child.transform, parentTransform, $"Set Parent: {childName} -> {parentName ?? "root"}");

            string result = parentTransform != null
                ? $"Set '{childName}' as child of '{parentName}'."
                : $"Unparented '{childName}' (moved to root).";

            return result;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
