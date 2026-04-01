using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hierarchy操作のためのMCPツール
/// GameObject階層構造の操作、空名前GameObject削除等を提供
/// </summary>
[McpServerToolType, Description("Hierarchy operations: list GameObjects, delete unnamed objects, etc.")]
internal sealed class HierarchyTool
{
    [McpServerTool, Description("List all GameObjects in the active scene with their InstanceIDs, names, and parent information")]
    public async ValueTask<string> Hier_ListAllGameObjects()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                throw new InvalidOperationException("No active scene.");

            var rootObjects = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            sb.AppendLine($"Scene: {scene.name}");
            sb.AppendLine($"Root GameObjects: {rootObjects.Length}");
            sb.AppendLine();

            foreach (var root in rootObjects)
            {
                ListGameObjectRecursive(root, sb, 0);
            }

            return sb.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    private void ListGameObjectRecursive(GameObject go, StringBuilder sb, int depth)
    {
        string indent = new string(' ', depth * 2);
        string name = string.IsNullOrEmpty(go.name) ? "<empty>" : go.name;
        sb.AppendLine($"{indent}[{go.GetEntityId()}] {name} (Active: {go.activeSelf})");

        foreach (Transform child in go.transform)
        {
            ListGameObjectRecursive(child.gameObject, sb, depth + 1);
        }
    }

    [McpServerTool, Description("Delete all GameObjects with empty names (m_Name: empty string) in the active scene. Use with caution.")]
    public async ValueTask<string> Hier_DeleteAllUnnamedGameObjects()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                throw new InvalidOperationException("No active scene.");

            var rootObjects = scene.GetRootGameObjects();
            var unnamedObjects = rootObjects
                .SelectMany(root => GetAllChildrenRecursive(root))
                .Where(go => string.IsNullOrEmpty(go.name))
                .ToList();

            if (unnamedObjects.Count == 0)
                return "No unnamed GameObjects found.";

            // Undo登録
            Undo.SetCurrentGroupName("Delete Unnamed GameObjects");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var go in unnamedObjects)
            {
                Undo.DestroyObjectImmediate(go);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(scene.GetRootGameObjects()[0]);

            return $"Deleted {unnamedObjects.Count} unnamed GameObjects.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Delete a single GameObject by its InstanceID. Supports Undo.")]
    public async ValueTask<string> Hier_DeleteByInstanceID(
        [Description("InstanceID of the GameObject (e.g., 51188, without '#' prefix)")]
        int instanceId)
    {
        try
        {
            await UniTask.SwitchToMainThread();

#pragma warning disable CS0618
            var obj = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618

            if (obj == null)
                throw new ArgumentException($"No object found with InstanceID {instanceId}.");

            var go = obj as GameObject;
            if (go == null)
                throw new ArgumentException($"Object with InstanceID {instanceId} is not a GameObject (type: {obj.GetType().Name}).");

            string name = string.IsNullOrEmpty(go.name) ? "<empty>" : go.name;

            Undo.DestroyObjectImmediate(go);

            return $"Deleted GameObject '{name}' (InstanceID: {instanceId}).";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    private System.Collections.Generic.List<GameObject> GetAllChildrenRecursive(GameObject root)
    {
        var result = new System.Collections.Generic.List<GameObject> { root };

        foreach (Transform child in root.transform)
        {
            result.AddRange(GetAllChildrenRecursive(child.gameObject));
        }

        return result;
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

            // Find child GameObject
            GameObject child = GameObject.Find(childName);
            if (child == null)
                throw new ArgumentException($"Child GameObject not found: '{childName}'");

            // Find parent GameObject (if specified)
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(parentName))
            {
                GameObject parent = GameObject.Find(parentName);
                if (parent == null)
                    throw new ArgumentException($"Parent GameObject not found: '{parentName}'");
                parentTransform = parent.transform;
            }

            // Set parent with Undo support
            Undo.SetTransformParent(child.transform, parentTransform, "Set Parent");

            string message = parentTransform == null
                ? $"Set '{childName}' as root GameObject."
                : $"Set '{childName}' as child of '{parentName}'.";

            return message;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set the sibling index of a GameObject in the Hierarchy. Changes the order of children under the same parent.")]
    public async ValueTask<string> Hier_SetSiblingIndex(
        [Description("InstanceID of the GameObject (e.g., 51188, without '#' prefix)")]
        int instanceId,
        [Description("The new sibling index (0-based). 0 means first child.")]
        int siblingIndex)
    {
        try
        {
            await UniTask.SwitchToMainThread();

#pragma warning disable CS0618
            var obj = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618

            if (obj == null)
                throw new ArgumentException($"No object found with InstanceID {instanceId}.");

            var go = obj as GameObject;
            if (go == null)
                throw new ArgumentException($"Object with InstanceID {instanceId} is not a GameObject (type: {obj.GetType().Name}).");

            // Record Undo
            Undo.RecordObject(go.transform, "Set Sibling Index");

            // Set sibling index
            go.transform.SetSiblingIndex(siblingIndex);

            return $"Set '{go.name}' siblingIndex to {siblingIndex}.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
