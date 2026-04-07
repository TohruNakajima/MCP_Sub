using UnityEditor;

namespace UnityNaturalMCP.Editor
{
    [FilePath("ProjectSettings/UnityNaturalMCPSetting.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MCPSetting : ScriptableSingleton<MCPSetting>
    {
        public string ipAddress = "localhost";
        public int port = 56780;
        public bool showMcpServerLog = true;
        public bool enableDefaultMcpTools = true;

        public void Save()
        {
            Save(true);
        }
    }
}