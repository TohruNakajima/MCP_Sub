using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

namespace TozawaMCP.Editor.Tools
{
    /// <summary>
    /// 宴（Utage）の新規プロジェクト作成MCPツール
    /// </summary>
    [McpServerToolType, Description("Utage new project creation")]
    public class UtageNewProjectTool
    {
        /// <summary>
        /// 宴の新規プロジェクトを作成する
        /// </summary>
        /// <param name="projectName">プロジェクト名（半角英数推奨）</param>
        /// <param name="createType">作成タイプ（CreateNewAdvScene, AddToCurrentScene, CreateScenarioAssetOnly）デフォルト: CreateNewAdvScene</param>
        /// <returns>実行結果メッセージ</returns>
        [McpServerTool, Description("Create new Utage project with specified name and type")]
        public async ValueTask<string> CreateUtageProject(
            [Description("Project name (e.g., 'MyAdventure', 'ShrineADV')")]
            string projectName,
            [Description("Create type: 'CreateNewAdvScene' (default), 'AddToCurrentScene', or 'CreateScenarioAssetOnly'")]
            string createType = "CreateNewAdvScene")
        {
            await UniTask.SwitchToMainThread();

            if (string.IsNullOrEmpty(projectName))
            {
                return "Error: projectName is null or empty";
            }

            try
            {
                // UtageEditorアセンブリを取得
                var utageAssembly = Assembly.Load("UtageEditor");
                if (utageAssembly == null)
                {
                    return "Error: UtageEditor assembly not found. Make sure Utage is installed.";
                }

                // AdvNewProjectWindow型を取得
                var windowType = utageAssembly.GetType("Utage.AdvNewProjectWindow");
                if (windowType == null)
                {
                    return "Error: AdvNewProjectWindow type not found in Utage assembly";
                }

                // ProjectType enumを取得
                var projectTypeEnumType = windowType.GetNestedType("ProjectType");
                if (projectTypeEnumType == null)
                {
                    return "Error: ProjectType enum not found in AdvNewProjectWindow";
                }

                // createTypeの値を検証・パース
                if (!Enum.IsDefined(projectTypeEnumType, createType))
                {
                    return $"Error: Invalid createType '{createType}'. Valid values: CreateNewAdvScene, AddToCurrentScene, CreateScenarioAssetOnly";
                }
                var projectTypeValue = Enum.Parse(projectTypeEnumType, createType);

                // EditorWindowのGetWindowメソッドを使ってウィンドウインスタンスを取得
                var getWindowMethod = typeof(EditorWindow).GetMethod("GetWindow",
                    new Type[] { typeof(Type), typeof(bool), typeof(string) });
                if (getWindowMethod == null)
                {
                    return "Error: EditorWindow.GetWindow method not found";
                }

                var window = getWindowMethod.Invoke(null, new object[] { windowType, false, "New Project" });
                if (window == null)
                {
                    return "Error: Failed to get AdvNewProjectWindow instance";
                }

                // NewProjectNameプロパティを設定
                var newProjectNameProperty = windowType.GetProperty("NewProjectName",
                    BindingFlags.Public | BindingFlags.Instance);
                if (newProjectNameProperty == null)
                {
                    return "Error: NewProjectName property not found";
                }

                // CreateTypeプロパティを設定
                var createTypeProperty = windowType.GetProperty("CreateType",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (createTypeProperty == null)
                {
                    return "Error: CreateType property not found";
                }

                // プロパティに値を設定
                newProjectNameProperty.SetValue(window, projectName);
                createTypeProperty.SetValue(window, projectTypeValue);

                // OnChangeSelectedメソッドを呼び出して内部状態を更新
                var onChangeSelectedMethod = windowType.GetMethod("OnChangeSelected",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (onChangeSelectedMethod != null)
                {
                    onChangeSelectedMethod.Invoke(window, null);
                }

                // Createメソッドを呼び出し
                var createMethod = windowType.GetMethod("Create",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (createMethod == null)
                {
                    return "Error: Create method not found";
                }

                createMethod.Invoke(window, null);

                // ウィンドウを閉じる
                var closeMethod = typeof(EditorWindow).GetMethod("Close");
                closeMethod?.Invoke(window, null);

                return $"Successfully created Utage project: '{projectName}' (Type: {createType})";
            }
            catch (Exception ex)
            {
                return $"Error creating Utage project '{projectName}': {ex.Message}\n{ex.StackTrace}";
            }
        }
    }
}
