using UnityEngine;
using UnityEditor;

namespace TozawaMCP.Editor.Tools
{
    /// <summary>
    /// カメラポジション用空GameObjectを一括作成するエディタツール
    /// </summary>
    public class CreateCameraPositions
    {
        [MenuItem("Tools/Create 19 Camera Positions")]
        public static void Create19CameraPositions()
        {
            // CameraPositions親オブジェクトを探す
            GameObject parent = GameObject.Find("CameraPositions");
            if (parent == null)
            {
                Debug.LogError("CameraPositions GameObject not found in scene.");
                return;
            }

            // 既存の子を確認
            int existingCount = parent.transform.childCount;
            Debug.Log($"Existing children: {existingCount}");

            // 作成が必要な年齢リスト（既存の6個を除く13個）
            int[] agesToCreate = { 15, 20, 30, 35, 45, 50, 60, 65, 70, 80, 85, 90, 95 };

            int createdCount = 0;
            foreach (int age in agesToCreate)
            {
                string objName = $"Age{age}Position";

                // 既存チェック
                Transform existing = parent.transform.Find(objName);
                if (existing != null)
                {
                    Debug.Log($"{objName} already exists, skipping.");
                    continue;
                }

                // 新規作成
                GameObject newObj = new GameObject(objName);
                newObj.transform.SetParent(parent.transform);
                newObj.transform.localPosition = Vector3.zero;
                newObj.transform.localRotation = Quaternion.identity;
                newObj.transform.localScale = Vector3.one;

                Undo.RegisterCreatedObjectUndo(newObj, $"Create {objName}");
                createdCount++;
            }

            Debug.Log($"Created {createdCount} new camera position objects.");
            EditorUtility.SetDirty(parent);
        }
    }
}
