using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 単一メッシュPrefab作成ツール（1本の杉成長シミュレーション用）
/// </summary>
[McpServerToolType, Description("Create single-mesh tree prefabs for growth simulation")]
internal sealed class SingleTreePrefabTool
{
    [McpServerTool, Description("Create a single-mesh tree prefab from FBX model. Extracts one specific mesh from the FBX.")]
    public async ValueTask<string> CreateSingleTreePrefab(
        [Description("Path to the FBX model asset (e.g., 'Assets/Model/BC_PM_P02_japanese_cedar.fbx')")]
        string fbxPath,
        [Description("Mesh index to extract (01-06, e.g., '01' for BC_PM_P02_japanese_cedar_01)")]
        string meshIndex,
        [Description("Output prefab path (e.g., 'Assets/Prefabs/SingleTree/Age10_Tree.prefab')")]
        string prefabPath,
        [Description("Prefab name (e.g., 'Age10_Tree')")]
        string prefabName)
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

            // FBXをインスタンス化
            GameObject instance = GameObject.Instantiate(fbxModel);
            instance.name = prefabName;

            // 指定メッシュ以外を削除
            // FBXインスタンス化後のメッシュ名は"BC_PM_P02_japanese_cedar_01"形式（アンダースコア付き）
            string targetMeshName = $"BC_PM_P02_japanese_cedar_{meshIndex}";
            Transform targetMesh = null;

            // デバッグ: 子オブジェクト名をすべて列挙
            System.Text.StringBuilder childNames = new System.Text.StringBuilder();
            foreach (Transform child in instance.transform)
            {
                childNames.Append(child.name).Append(", ");
                if (child.name.Contains(meshIndex))
                {
                    targetMesh = child;
                    targetMeshName = child.name; // 実際の名前を記録
                }
            }

            if (targetMesh == null)
            {
                GameObject.DestroyImmediate(instance);
                return $"Error: Mesh containing '{meshIndex}' not found in FBX. Children: {childNames}";
            }

            // targetMesh以外を削除
            Transform[] allChildren = instance.transform.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child != instance.transform && child != targetMesh && !targetMesh.IsChildOf(child))
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
            }

            // ディレクトリ作成
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Prefabとして保存
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            // シーンから削除
            GameObject.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Successfully created single-mesh tree prefab: {prefabPath} (mesh: {targetMeshName})";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return $"Error creating single tree prefab: {e.Message}";
        }
    }
}
