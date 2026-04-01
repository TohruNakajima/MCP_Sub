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
/// 森林オブジェクト削除のためのMCPツール
/// </summary>
[McpServerToolType, Description("Clean up forest objects")]
internal sealed class ForestCleanupTool
{
    [McpServerTool, Description("Delete all GameObjects matching a name pattern (e.g., 'SingleTreeSimulator_' to delete SingleTreeSimulator_0001, _0002, etc.)")]
    public async ValueTask<string> Forest_DeleteByPattern(
        [Description("Name pattern to match (e.g., 'SingleTreeSimulator_')")]
        string namePattern)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            // Find all matching objects
            var matchingObjects = rootObjects
                .Where(go => go.name.StartsWith(namePattern))
                .ToList();

            if (matchingObjects.Count == 0)
                return $"No objects found matching pattern: '{namePattern}'";

            // Delete all matching objects
            Undo.SetCurrentGroupName($"Delete {matchingObjects.Count} objects");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var obj in matchingObjects)
            {
                Undo.DestroyObjectImmediate(obj);
            }

            Undo.CollapseUndoOperations(undoGroup);

            return $"Successfully deleted {matchingObjects.Count} objects matching pattern '{namePattern}'";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
