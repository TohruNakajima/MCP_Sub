using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prefabインスタンス化のためのMCPツール
/// </summary>
[McpServerToolType, Description("Instantiate prefabs in the scene")]
internal sealed class PrefabInstantiationTool
{
    [McpServerTool, Description("Instantiate a prefab in the scene at specified position with optional parent")]
    public async ValueTask<string> Prefab_Instantiate(
        [Description("Path to the prefab asset (e.g., 'Assets/Prefabs/MyPrefab.prefab')")]
        string prefabPath,
        [Description("Position in world space (format: 'x,y,z' e.g., '0,0,0')")]
        string position = "0,0,0",
        [Description("Parent GameObject path (optional, e.g., 'ParentObject')")]
        string parentPath = null)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // Load prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new ArgumentException($"Prefab not found at path: {prefabPath}");

            // Parse position
            string[] posParts = position.Split(',');
            if (posParts.Length != 3)
                throw new ArgumentException($"Invalid position format: '{position}'. Use 'x,y,z' format.");

            Vector3 pos = new Vector3(
                float.Parse(posParts[0].Trim()),
                float.Parse(posParts[1].Trim()),
                float.Parse(posParts[2].Trim())
            );

            // Find parent if specified
            Transform parent = null;
            if (!string.IsNullOrEmpty(parentPath))
            {
                GameObject parentGo = GameObject.Find(parentPath);
                if (parentGo == null)
                    throw new ArgumentException($"Parent GameObject not found: '{parentPath}'");
                parent = parentGo.transform;
            }

            // Instantiate prefab
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = pos;

            // Register Undo
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

            string parentInfo = parent != null ? $" (parent: {parentPath})" : "";
            return $"Instantiated '{instance.name}' at {pos}{parentInfo}. InstanceID: {instance.GetInstanceID()}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
