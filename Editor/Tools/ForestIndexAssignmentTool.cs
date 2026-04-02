using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 森林内の木にtreeIndexを自動割り当てるMCPツール
/// </summary>
[McpServerToolType, Description("Assign tree indices to forest trees")]
internal sealed class ForestIndexAssignmentTool
{
    [McpServerTool, Description("Assign treeIndex to all SingleTreeGrowth components in the scene. Center tree gets 0, others get 1-299 based on name order.")]
    public async ValueTask<string> Forest_AssignTreeIndices()
    {
        try
        {
            await UniTask.SwitchToMainThread();

            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            // Find all SingleTreeGrowth components using reflection
            System.Type singleTreeGrowthType = System.Type.GetType("WoodSimulator.SingleTreeGrowth, Assembly-CSharp");
            if (singleTreeGrowthType == null)
            {
                return "SingleTreeGrowth type not found. Make sure the scripts are compiled.";
            }

            var allTrees = rootObjects
                .SelectMany(go => go.GetComponentsInChildren(singleTreeGrowthType, true))
                .Cast<UnityEngine.Component>()
                .ToList();

            if (allTrees.Count == 0)
                return "No SingleTreeGrowth components found in the scene.";

            // Sort by name (SingleTreeSimulator should be first, then SingleTreeSimulator_0001, _0002, etc.)
            var sortedTrees = allTrees
                .OrderBy(tree => tree.gameObject.name)
                .ToList();

            // Assign indices
            string undoName = "Assign Tree Indices (" + sortedTrees.Count.ToString() + " trees)";
            Undo.SetCurrentGroupName(undoName);
            int undoGroup = Undo.GetCurrentGroup();

            var treeIndexField = singleTreeGrowthType.GetField("treeIndex");
            if (treeIndexField == null)
            {
                return "treeIndex field not found in SingleTreeGrowth.";
            }

            for (int i = 0; i < sortedTrees.Count; i++)
            {
                Undo.RecordObject(sortedTrees[i], "Set treeIndex");
                treeIndexField.SetValue(sortedTrees[i], i);
                EditorUtility.SetDirty(sortedTrees[i]);
            }

            Undo.CollapseUndoOperations(undoGroup);

            int maxIndex = sortedTrees.Count - 1;
            return "Successfully assigned treeIndex to " + sortedTrees.Count.ToString() + " trees (0-" + maxIndex.ToString() + ")";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
