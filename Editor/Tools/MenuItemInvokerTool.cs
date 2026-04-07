using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

namespace TozawaMCP.Editor.Tools
{
    /// <summary>
    /// Unity Editorのメニューバー操作を実行するMCPツール
    /// </summary>
    [McpServerToolType, Description("Unity Editor menu bar operations")]
    public class MenuItemInvokerTool
    {
        /// <summary>
        /// 指定されたメニューパスのメニューアイテムを実行する
        /// </summary>
        /// <param name="menuPath">メニューパス（例: "Window/General/Console", "File/Save", "Edit/Undo"）</param>
        /// <returns>実行結果メッセージ</returns>
        [McpServerTool, Description("Execute Unity Editor menu item by menu path")]
        public async ValueTask<string> ExecuteMenuItem(
            [Description("Menu path (e.g., 'Window/General/Console', 'File/Save', 'Edit/Undo')")]
            string menuPath)
        {
            await UniTask.SwitchToMainThread();

            if (string.IsNullOrEmpty(menuPath))
            {
                return "Error: menuPath is null or empty";
            }

            try
            {
                bool success = EditorApplication.ExecuteMenuItem(menuPath);

                if (success)
                {
                    return $"Successfully executed menu item: '{menuPath}'";
                }
                else
                {
                    return $"Failed to execute menu item: '{menuPath}' (Menu item not found or disabled)";
                }
            }
            catch (System.Exception ex)
            {
                return $"Error executing menu item '{menuPath}': {ex.Message}";
            }
        }
    }
}
