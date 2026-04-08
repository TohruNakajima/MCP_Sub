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
    [McpServerTool, Description("Create a tree prefab with 6 age-specific child meshes from FBX model. Creates Age10_SmallTree through Age100_AncientTree children with size-sorted mesh assignment.")]
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

            // FBX内の個別メッシュをサイズ順（高さ順）に割り当て
            // mesh_bounds.jsonより: 01(3.72m) < 03(3.80m) < 05(3.90m) < 04(3.97m) < 06(4.36m) < 02(4.75m)
            string[] meshIndices = new string[] { "01", "03", "05", "04", "06", "02" };
            string[] ageNames = new string[]
            {
                "Age10_SmallTree",
                "Age25_YoungTree",
                "Age40_MediumTree",
                "Age55_MatureTree",
                "Age75_OldTree",
                "Age100_AncientTree"
            };

            for (int i = 0; i < ageNames.Length; i++)
            {
                GameObject child = GameObject.Instantiate(fbxModel);
                child.name = ageNames[i];
                child.transform.SetParent(parent.transform, false);

                // FBX内の特定メッシュのみを有効化（BC_PM_P02_japanese_cedar_XX）
                string targetMeshName = $"BC_PM_P02_japanese_cedar_{meshIndices[i]}";
                foreach (Transform meshChild in child.transform)
                {
                    meshChild.gameObject.SetActive(meshChild.name == targetMeshName);
                }

                // 最初以外は非アクティブにする
                child.SetActive(ageNames[i] == "Age10_SmallTree");
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

    [McpServerTool, Description("Save a GameObject in the active scene as a new prefab asset. The GameObject stays in the scene.")]
    public async ValueTask<string> SaveGameObjectAsPrefab(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Output prefab path (e.g., 'Assets/Prefabs/MyPrefab.prefab'). Directory is created if missing.")]
        string prefabPath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // GameObjectを検索（InstanceID or パス）
            GameObject go = null;
            if (target.StartsWith("#"))
            {
                if (int.TryParse(target.Substring(1), out int id))
                    go = EditorUtility.EntityIdToObject(id) as GameObject;
            }
            else
            {
                go = GameObject.Find(target);
            }

            if (go == null)
                return $"Error: GameObject not found: {target}";

            // ディレクトリ作成
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // プレハブ保存
            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out success);

            if (!success || prefab == null)
                return $"Error: Failed to create prefab at {prefabPath}";

            AssetDatabase.SaveAssets();
            return $"Created prefab: {prefabPath} (from '{go.name}')";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error: {e.Message}";
        }
    }
}
