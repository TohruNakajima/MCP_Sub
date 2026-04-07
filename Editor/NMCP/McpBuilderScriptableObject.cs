using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace UnityNaturalMCP.Editor
{
    public abstract class McpBuilderScriptableObject : ScriptableObject
    {
        public abstract void Build(IMcpServerBuilder builder);
    }
}