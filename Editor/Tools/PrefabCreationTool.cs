using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prefab作成専用MCPツール
/// </summary>
[McpServerToolType, Description("Create prefabs from models")]
internal sealed class PrefabCreationTool
{
    [McpServerTool, Description("Create a tree prefab with 6 age-specific child meshes from FBX model. Creates Age10_SmallTree through Age100_AncientTree children.")]
    public async ValueTask<string> CreateTreePrefabFromFBX(
        [Description("Path to the FBX model asset (e.g., 'Assets/Model/BC_PM_P02_japanese_cedar.fbx')")]
        string fbxPath,
        [Description("Output prefab path (e.g., 'Assets/Prefabs/JapaneseCedar.prefab')")]
        string prefabPath,
        [Description("Parent GameObject name (e.g., 'JapaneseCedar')")]
        string parentName)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // FBXモデルをロード
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxModel == null)
            {
                return $"Error: FBX model not found at path: {fbxPath}";
            }

            // 親GameObjectを作成
            GameObject parent = new GameObject(parentName);

            // FBXから6つのインスタンスを作成し、それぞれ異なる名前をつけて子として追加
            string[] ageNames = new string[]
            {
                "Age10_SmallTree",
                "Age25_YoungTree",
                "Age40_MediumTree",
                "Age55_MatureTree",
                "Age75_OldTree",
                "Age100_AncientTree"
            };

            foreach (string ageName in ageNames)
            {
                GameObject child = GameObject.Instantiate(fbxModel);
                child.name = ageName;
                child.transform.SetParent(parent.transform, false);

                // 最初以外は非アクティブにする
                child.SetActive(ageName == "Age10_SmallTree");
            }

            // Prefabとして保存
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            PrefabUtility.SaveAsPrefabAsset(parent, prefabPath);

            // シーンから削除
            GameObject.DestroyImmediate(parent);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Successfully created tree prefab at: {prefabPath} with 6 age-specific children: {string.Join(", ", ageNames)}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error creating tree prefab: {e.Message}";
        }
    }
}
