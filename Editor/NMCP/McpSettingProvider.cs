using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityNaturalMCP.Editor
{
    internal sealed class McpSettingProvider : SettingsProvider
    {
        private const string SettingPath = "Project/Unity Natural MCP";
        private readonly UnityEditor.Editor _settingsEditor;

        [SettingsProvider]
        public static SettingsProvider CreateSettingProvider()
        {
            return new McpSettingProvider(SettingPath, SettingsScope.Project, null);
        }

        public McpSettingProvider(string path, SettingsScope scopes, IEnumerable<string> keywords) : base(path, scopes, keywords)
        {
            var settings = MCPSetting.instance;
            settings.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable; 
            UnityEditor.Editor.CreateCachedEditor(settings, null, ref _settingsEditor);
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUI.BeginChangeCheck();
            _settingsEditor.OnInspectorGUI();
            if (GUILayout.Button("Refresh"))
            {
                McpServerRunner.RefreshMcpServer();
            }
            if (EditorGUI.EndChangeCheck())
            {
                MCPSetting.instance.Save();
            }
        }
    }
}