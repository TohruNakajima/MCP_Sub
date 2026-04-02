using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 森林配置のためのMCPツール（フィボナッチスパイラル + Perlinノイズ）
/// </summary>
[McpServerToolType, Description("Plant trees in forest pattern")]
internal sealed class ForestPlantingTool
{
    [McpServerTool, Description("Plant trees using Fibonacci spiral with Perlin noise. Instantiates prefabs one by one carefully.")]
    public async ValueTask<string> Forest_PlantTrees(
        [Description("Path to the tree prefab (e.g., 'Assets/Prefabs/SingleTreeSimulator.prefab')")]
        string prefabPath,
        [Description("Center position (format: 'x,y,z' e.g., '0,0,0')")]
        string centerPosition = "0,0,0",
        [Description("Number of trees to plant (excluding the center tree)")]
        int count = 2999,
        [Description("Spacing between trees (base radius multiplier)")]
        float spacing = 2.0f,
        [Description("Jitter amount for Perlin noise randomness")]
        float jitter = 1.5f,
        [Description("Parent GameObject path (optional)")]
        string parentPath = null)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // Load prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new ArgumentException($"Prefab not found at path: {prefabPath}");

            // Parse center position
            string[] posParts = centerPosition.Split(',');
            if (posParts.Length != 3)
                throw new ArgumentException($"Invalid position format: '{centerPosition}'. Use 'x,y,z' format.");

            Vector3 center = new Vector3(
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

            // Golden angle constant
            const float goldenAngle = 137.5f;

            // Plant trees one by one
            int planted = 0;
            for (int i = 0; i < count; i++)
            {
                // Fibonacci spiral calculation
                float angle = i * goldenAngle;
                float radius = Mathf.Sqrt(i + 1) * spacing;

                // Add Perlin noise for randomness
                float noiseX = (Mathf.PerlinNoise(i * 0.1f, 0) - 0.5f) * jitter;
                float noiseZ = (Mathf.PerlinNoise(0, i * 0.1f) - 0.5f) * jitter;

                // Calculate final position
                float x = radius * Mathf.Cos(angle * Mathf.Deg2Rad) + noiseX;
                float z = radius * Mathf.Sin(angle * Mathf.Deg2Rad) + noiseZ;
                Vector3 position = center + new Vector3(x, 0, z);

                // Instantiate prefab
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.position = position;
                instance.name = $"SingleTreeSimulator_{i + 1:D4}";

                // Set treeIndex (starting from 1, center tree is 0) using reflection
                System.Type singleTreeGrowthType = System.Type.GetType("WoodSimulator.SingleTreeGrowth, Assembly-CSharp");
                if (singleTreeGrowthType != null)
                {
                    var singleTreeGrowth = instance.GetComponent(singleTreeGrowthType);
                    if (singleTreeGrowth != null)
                    {
                        var treeIndexField = singleTreeGrowthType.GetField("treeIndex");
                        if (treeIndexField != null)
                        {
                            treeIndexField.SetValue(singleTreeGrowth, i + 1);
                        }
                    }
                }

                // Register Undo
                Undo.RegisterCreatedObjectUndo(instance, "Plant Tree");

                planted++;

                // Progress report every 100 trees
                if ((i + 1) % 100 == 0)
                {
                    Debug.Log($"[ForestPlanting] Progress: {i + 1}/{count} trees planted");
                    await UniTask.Yield(); // Allow Unity to process
                }
            }

            string parentInfo = parent != null ? $" under '{parentPath}'" : "";
            return $"Successfully planted {planted} trees{parentInfo}. Algorithm: Fibonacci spiral (golden angle 137.5°) + Perlin noise (jitter {jitter})";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
