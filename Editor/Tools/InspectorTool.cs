using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[McpServerToolType, Description("Inspect and manipulate GameObjects, Components, and Properties in the Unity scene.")]
internal sealed class InspectorTool
{
    // ========== MCP Methods (Facade) ==========

    [McpServerTool, Description("Get the hierarchy tree of the active scene. Returns root GameObjects and their children up to the specified depth.")]
    public async ValueTask<string> Ins_GetHierarchyInfo(
        [Description("Maximum depth to traverse. 0 = root only, 1 = root + direct children, etc. Defaults to 1.")]
        int maxDepth = 1)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            sb.AppendLine($"Scene: {scene.name} ({roots.Length} root objects)");
            sb.AppendLine("---");

            foreach (var root in roots)
            {
                HierarchyNodeInfo.AppendTo(sb, root, 0, maxDepth);
            }

            return sb.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Get detailed information about a specific GameObject including name, active state, tag, layer, components, and children.")]
    public async ValueTask<string> Ins_GetGameObjectInfo(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345'). Path is relative to scene root.")]
        string target)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var info = GameObjectInfo.From(go);
            return info.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Get mesh bounds information (size) from a GameObject with MeshFilter.")]
    public async ValueTask<string> Ins_GetMeshBounds(
        [Description("Path to the GameObject in hierarchy (e.g. 'Model/Mesh01') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null)
                throw new ArgumentException($"GameObject '{go.name}' does not have a MeshFilter component.");

            if (meshFilter.sharedMesh == null)
                throw new ArgumentException($"GameObject '{go.name}' MeshFilter has no mesh assigned.");

            var bounds = meshFilter.sharedMesh.bounds;
            return $"Mesh: {meshFilter.sharedMesh.name}\nBounds Center: {bounds.center}\nBounds Size: {bounds.size}\nBounds Extents: {bounds.extents}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Get all serialized properties of a specific component on a GameObject. Shows property names, types, and current values.")]
    public async ValueTask<string> Ins_GetComponentProperties(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Transform', 'BoxCollider') or index in brackets (e.g. '[0]', '[1]'). Type name matching is case-insensitive.")]
        string component)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var info = ComponentInfo.From(comp);
            return info.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set a GameObject's active state (SetActive). Supports Undo.")]
    public async ValueTask<string> Ins_SetActive(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("True to activate, false to deactivate.")]
        bool active)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            Undo.RecordObject(go, $"Set Active '{go.name}' to {active}");
            go.SetActive(active);
            EditorUtility.SetDirty(go);
            return $"'{go.name}' activeSelf set to {active}.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Rename a GameObject. Supports Undo.")]
    public async ValueTask<string> Ins_RenameGameObject(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("The new name for the GameObject.")]
        string newName)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name must not be empty.");

            var go = GameObjectResolver.Resolve(target);
            var oldName = go.name;
            Undo.RecordObject(go, $"Rename '{oldName}' to '{newName}'");
            go.name = newName;
            EditorUtility.SetDirty(go);
            return $"Renamed '{oldName}' to '{newName}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set a serialized property value on a component. Use GetComponentProperties first to see available property paths and types. Supports Undo.")]
    public async ValueTask<string> Ins_SetPropertyValue(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Transform', 'BoxCollider') or index in brackets (e.g. '[0]', '[1]').")]
        string component,
        [Description("The serialized property path (e.g. 'm_LocalPosition.x', 'm_IsActive'). Use GetComponentProperties to find valid paths.")]
        string propertyPath,
        [Description("The value to set as a string. Examples: '42', '3.14', 'true', 'Hello', '(1.0, 2.0, 3.0)' for Vector3.")]
        string value)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var result = PropertyAccessor.WriteProperty(comp, propertyPath, value);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Add a component to a GameObject by type name. Supports Undo.")]
    public async ValueTask<string> Ins_AddComponent(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Full or short type name of the component to add (e.g. 'BoxCollider', 'Rigidbody', 'UnityEngine.UI.Image').")]
        string componentType)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var type = ComponentResolver.ResolveType(componentType);
            var comp = Undo.AddComponent(go, type);
            return $"Added {comp.GetType().Name} to '{go.name}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Remove a component from a GameObject. Cannot remove Transform. Supports Undo.")]
    public async ValueTask<string> Ins_RemoveComponent(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'BoxCollider') or index in brackets (e.g. '[1]'). Cannot remove Transform ([0]).")]
        string component)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);

            if (comp is Transform)
                throw new InvalidOperationException("Cannot remove Transform component.");

            Undo.DestroyObjectImmediate(comp);
            return $"Removed {comp.GetType().Name} from '{go.name}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Enable or disable a component (only works on components that have an 'enabled' property, e.g. Renderer, Collider, MonoBehaviour). Supports Undo.")]
    public async ValueTask<string> Ins_SetComponentEnabled(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'MeshRenderer') or index in brackets (e.g. '[1]').")]
        string component,
        [Description("True to enable, false to disable.")]
        bool enabled)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);

            if (comp is Behaviour behaviour)
            {
                Undo.RecordObject(behaviour, $"Set {comp.GetType().Name} enabled={enabled}");
                behaviour.enabled = enabled;
                EditorUtility.SetDirty(behaviour);
                return $"{comp.GetType().Name} on '{go.name}' enabled set to {enabled}.";
            }

            if (comp is Renderer renderer)
            {
                Undo.RecordObject(renderer, $"Set {comp.GetType().Name} enabled={enabled}");
                renderer.enabled = enabled;
                EditorUtility.SetDirty(renderer);
                return $"{comp.GetType().Name} on '{go.name}' enabled set to {enabled}.";
            }

            if (comp is Collider collider)
            {
                Undo.RecordObject(collider, $"Set {comp.GetType().Name} enabled={enabled}");
                collider.enabled = enabled;
                EditorUtility.SetDirty(collider);
                return $"{comp.GetType().Name} on '{go.name}' enabled set to {enabled}.";
            }

            throw new InvalidOperationException(
                $"{comp.GetType().Name} does not have an 'enabled' property.");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set the size of an array property. Supports Undo.")]
    public async ValueTask<string> Ins_SetArraySize(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Transform', 'BoxCollider') or index in brackets (e.g. '[0]', '[1]').")]
        string component,
        [Description("The serialized property path to the array (e.g. 'myArray'). Use GetComponentProperties to find valid paths.")]
        string propertyPath,
        [Description("The new size of the array.")]
        int size)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var result = PropertyAccessor.SetArraySize(comp, propertyPath, size);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Insert a new element into an array property at the specified index. Supports Undo.")]
    public async ValueTask<string> Ins_InsertArrayElement(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Transform', 'BoxCollider') or index in brackets (e.g. '[0]', '[1]').")]
        string component,
        [Description("The serialized property path to the array (e.g. 'myArray'). Use GetComponentProperties to find valid paths.")]
        string propertyPath,
        [Description("The index at which to insert the new element. Use -1 to append at the end.")]
        int index = -1)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var result = PropertyAccessor.InsertArrayElement(comp, propertyPath, index);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Delete an element from an array property at the specified index. Supports Undo.")]
    public async ValueTask<string> Ins_DeleteArrayElement(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Transform', 'BoxCollider') or index in brackets (e.g. '[0]', '[1]').")]
        string component,
        [Description("The serialized property path to the array (e.g. 'myArray'). Use GetComponentProperties to find valid paths.")]
        string propertyPath,
        [Description("The index of the element to delete.")]
        int index)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var result = PropertyAccessor.DeleteArrayElement(comp, propertyPath, index);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set the tag of a GameObject. Supports Undo.")]
    public async ValueTask<string> Ins_SetTag(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("The tag name (e.g. 'Player', 'Enemy', 'Untagged').")]
        string tagName)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            Undo.RecordObject(go, $"Set tag '{tagName}' on '{go.name}'");
            go.tag = tagName;
            EditorUtility.SetDirty(go);
            return $"Set tag of '{go.name}' to '{tagName}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set the layer of a GameObject. Supports Undo.")]
    public async ValueTask<string> Ins_SetLayer(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("The layer name (e.g. 'Default', 'UI', 'Water') or layer number (0-31).")]
        string layer)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            int layerIndex;

            // Try parse as number first
            if (int.TryParse(layer, out layerIndex))
            {
                if (layerIndex < 0 || layerIndex > 31)
                    throw new ArgumentException($"Layer number must be between 0 and 31. Got: {layerIndex}");
            }
            else
            {
                // Parse as layer name
                layerIndex = LayerMask.NameToLayer(layer);
                if (layerIndex == -1)
                    throw new ArgumentException($"Layer '{layer}' does not exist.");
            }

            Undo.RecordObject(go, $"Set layer {layerIndex} on '{go.name}'");
            go.layer = layerIndex;
            EditorUtility.SetDirty(go);
            return $"Set layer of '{go.name}' to {layerIndex} ({LayerMask.LayerToName(layerIndex)}).";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set the static flags of a GameObject. Supports Undo.")]
    public async ValueTask<string> Ins_SetStatic(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("True to set all static flags, false to clear all static flags.")]
        bool isStatic)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            Undo.RecordObject(go, $"Set static={isStatic} on '{go.name}'");

            if (isStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)(-1)); // All flags
            }
            else
            {
                GameObjectUtility.SetStaticEditorFlags(go, 0); // No flags
            }

            EditorUtility.SetDirty(go);
            return $"Set static flags of '{go.name}' to {(isStatic ? "all enabled" : "all disabled")}.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Delete a GameObject from the scene. Supports Undo.")]
    public async ValueTask<string> Ins_DeleteGameObject(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var name = go.name;
            Undo.DestroyObjectImmediate(go);
            return $"Deleted GameObject '{name}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Instantiate a Prefab into the scene hierarchy. Supports Undo.")]
    public async ValueTask<string> Ins_InstantiatePrefab(
        [Description("Path to the Prefab asset (e.g. 'Assets/Prefabs/MyPrefab.prefab').")]
        string prefabPath,
        [Description("Optional name for the instantiated GameObject. If not specified, uses the Prefab's name.")]
        string name = null,
        [Description("Optional parent GameObject path (e.g. 'Canvas/Panel'). If not specified, instantiates at scene root.")]
        string parentPath = null)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // Load Prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new ArgumentException($"Prefab not found at path: '{prefabPath}'");

            // Instantiate Prefab
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Failed to instantiate Prefab: '{prefabPath}'");

            // Set name
            if (!string.IsNullOrEmpty(name))
            {
                instance.name = name;
            }

            // Set parent
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = GameObjectResolver.Resolve(parentPath);
                instance.transform.SetParent(parent.transform, false);
            }

            // Register Undo
            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate Prefab '{prefabPath}'");

            return $"Instantiated Prefab '{prefabPath}' as '{instance.name}' (ID: {instance.GetEntityId()}).";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Add a method listener to a UnityEvent (e.g. Button.onClick). Supports Undo.")]
    public async ValueTask<string> Ins_AddUnityEventListener(
        [Description("Path to the GameObject in hierarchy (e.g. 'Canvas/Button') or InstanceID prefixed with '#' (e.g. '#12345').")]
        string target,
        [Description("Component type name (e.g. 'Button') or index in brackets (e.g. '[1]').")]
        string component,
        [Description("The serialized property path to the UnityEvent (e.g. 'm_OnClick'). Use GetComponentProperties to find valid paths.")]
        string eventPropertyPath,
        [Description("Path to the GameObject or Component that has the method (e.g. 'GameStarter' or 'GameStarter/GameStarter').")]
        string listenerTarget,
        [Description("The method name to call (e.g. 'OnStartButtonClick').")]
        string methodName)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var go = GameObjectResolver.Resolve(target);
            var comp = ComponentResolver.Resolve(go, component);
            var result = PropertyAccessor.AddUnityEventListener(comp, eventPropertyPath, listenerTarget, methodName);
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== Inner Classes ==========

    /// <summary>パスまたはInstanceIDからGameObjectを解決する</summary>
    internal static class GameObjectResolver
    {
        public static GameObject Resolve(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Target must not be empty.");

            // InstanceID形式: "#12345"
            if (target.StartsWith("#") && int.TryParse(target.Substring(1), out var instanceId))
            {
#if UNITY_6000_0_OR_NEWER
#pragma warning disable CS0618
                var obj = EditorUtility.EntityIdToObject(instanceId) as GameObject;
#pragma warning restore CS0618
#else
                var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#endif
                if (obj == null)
                    throw new ArgumentException($"No GameObject found with InstanceID {instanceId}.");
                return obj;
            }

            // パス形式: "Canvas/Button" or "/Canvas/Button"
            var path = target.TrimStart('/');
            var go = FindByPath(path);
            if (go == null)
                throw new ArgumentException($"No GameObject found at path '{target}'.");
            return go;
        }

        private static GameObject FindByPath(string path)
        {
            var parts = path.Split('/');
            if (parts.Length == 0) return null;

            // プレハブステージ内で作業中の場合、プレハブステージから検索
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                var prefabRoot = prefabStage.prefabContentsRoot;
                if (prefabRoot.name == parts[0])
                {
                    if (parts.Length == 1) return prefabRoot;

                    var current = prefabRoot.transform;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var child = current.Find(parts[i]);
                        if (child == null) return null;
                        current = child;
                    }
                    return current.gameObject;
                }
            }

            // プレハブステージ外の場合、通常のシーンから検索
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var root = roots.FirstOrDefault(r => r.name == parts[0]);
            if (root == null) return null;
            if (parts.Length == 1) return root;

            var current2 = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                var child = current2.Find(parts[i]);
                if (child == null) return null;
                current2 = child;
            }

            return current2.gameObject;
        }
    }

    /// <summary>型名またはインデックスからComponentを解決する</summary>
    private static class ComponentResolver
    {
        public static UnityEngine.Component Resolve(GameObject go, string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Component identifier must not be empty.");

            // インデックス形式: "[0]", "[1]"
            if (identifier.StartsWith("[") && identifier.EndsWith("]"))
            {
                var indexStr = identifier.Substring(1, identifier.Length - 2);
                if (!int.TryParse(indexStr, out var index))
                    throw new ArgumentException($"Invalid component index: '{identifier}'.");

                var all = go.GetComponents<UnityEngine.Component>();
                if (index < 0 || index >= all.Length)
                    throw new ArgumentException($"Component index {index} out of range. GameObject '{go.name}' has {all.Length} components.");
                return all[index];
            }

            // 型名形式: "Transform", "BoxCollider" (case-insensitive)
            var components = go.GetComponents<UnityEngine.Component>();
            var match = components.FirstOrDefault(c =>
                c != null && c.GetType().Name.Equals(identifier, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                var available = string.Join(", ", components
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name));
                throw new ArgumentException(
                    $"No component '{identifier}' found on '{go.name}'. Available: {available}");
            }

            return match;
        }

        public static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("Component type name must not be empty.");

            // フルネームで検索 (e.g. "UnityEngine.UI.Image")
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .FirstOrDefault(t =>
                    typeof(UnityEngine.Component).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    (t.FullName != null && t.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                     t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)));

            if (type == null)
                throw new ArgumentException($"Component type '{typeName}' not found.");

            return type;
        }
    }

    /// <summary>SerializedPropertyの読み書き</summary>
    private static class PropertyAccessor
    {
        public static List<PropertySummary> ReadAll(UnityEngine.Component component)
        {
            var result = new List<PropertySummary>();
            var so = new SerializedObject(component);
            var iterator = so.GetIterator();

            if (!iterator.NextVisible(true)) return result;

            do
            {
                result.Add(PropertySummary.From(iterator));
            }
            while (iterator.NextVisible(false));

            return result;
        }

        public static string WriteProperty(UnityEngine.Component component, string propertyPath, string value)
        {
            var so = new SerializedObject(component);
            SerializedProperty prop;

            // Check if this is an array size change pattern: "arrayName.Array.size"
            var sizePattern = System.Text.RegularExpressions.Regex.Match(
                propertyPath, @"^(.+)\.Array\.size$");

            if (sizePattern.Success)
            {
                // Extract array property name
                var arrayPropertyName = sizePattern.Groups[1].Value;

                // Get the array property
                var arrayProp = so.FindProperty(arrayPropertyName);
                if (arrayProp == null || !arrayProp.isArray)
                    throw new ArgumentException(
                        $"Array property '{arrayPropertyName}' not found on {component.GetType().Name}.");

                // Parse new size
                if (!int.TryParse(value, out var newSize))
                    throw new ArgumentException($"Cannot parse '{value}' as int for array size.");

                if (newSize < 0)
                    throw new ArgumentException($"Array size cannot be negative: {newSize}");

                // Record undo and set array size
                Undo.RecordObject(component, $"Set {propertyPath} on {component.GetType().Name}");
                arrayProp.arraySize = newSize;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
                return $"Set {component.GetType().Name}.{propertyPath} = {newSize}";
            }

            // Check if this is an array element access pattern with optional nested property:
            // "arrayName.Array.data[index]" or "arrayName.Array.data[index].nestedProperty"
            var arrayPattern = System.Text.RegularExpressions.Regex.Match(
                propertyPath, @"^(.+)\.Array\.data\[(\d+)\](.*)$");

            if (arrayPattern.Success)
            {
                // Extract array property name, index, and optional nested property path
                var arrayPropertyName = arrayPattern.Groups[1].Value;
                var arrayIndex = int.Parse(arrayPattern.Groups[2].Value);
                var nestedPath = arrayPattern.Groups[3].Value.TrimStart('.');

                // Get the array property
                var arrayProp = so.FindProperty(arrayPropertyName);
                if (arrayProp == null || !arrayProp.isArray)
                    throw new ArgumentException(
                        $"Array property '{arrayPropertyName}' not found on {component.GetType().Name}.");

                // Auto-expand array size if necessary
                if (arrayIndex >= arrayProp.arraySize)
                {
                    Debug.Log($"[InspectorTool] Auto-expanding array '{arrayPropertyName}' from size {arrayProp.arraySize} to {arrayIndex + 1}");
                    arrayProp.arraySize = arrayIndex + 1;
                }

                // Get the array element (after potential expansion)
                if (arrayIndex < 0)
                    throw new ArgumentException($"Array index {arrayIndex} cannot be negative.");

                prop = arrayProp.GetArrayElementAtIndex(arrayIndex);

                // If there's a nested property path, navigate to it
                if (!string.IsNullOrEmpty(nestedPath))
                {
                    prop = prop.FindPropertyRelative(nestedPath);
                    if (prop == null)
                        throw new ArgumentException(
                            $"Nested property '{nestedPath}' not found in array element {arrayIndex} of '{arrayPropertyName}'.");
                }
            }
            else
            {
                // Normal property path
                prop = so.FindProperty(propertyPath);
                if (prop == null)
                    throw new ArgumentException(
                        $"Property '{propertyPath}' not found on {component.GetType().Name}.");
            }

            Undo.RecordObject(component, $"Set {propertyPath} on {component.GetType().Name}");

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!int.TryParse(value, out var intVal))
                        throw new ArgumentException($"Cannot parse '{value}' as int.");
                    prop.intValue = intVal;
                    break;

                case SerializedPropertyType.Boolean:
                    if (!bool.TryParse(value, out var boolVal))
                        throw new ArgumentException($"Cannot parse '{value}' as bool.");
                    prop.boolValue = boolVal;
                    break;

                case SerializedPropertyType.Float:
                    if (!float.TryParse(value, out var floatVal))
                        throw new ArgumentException($"Cannot parse '{value}' as float.");
                    prop.floatValue = floatVal;
                    break;

                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    break;

                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out var enumIndex))
                    {
                        prop.enumValueIndex = enumIndex;
                    }
                    else
                    {
                        var names = prop.enumDisplayNames;
                        var idx = Array.IndexOf(names, value);
                        if (idx < 0)
                            throw new ArgumentException(
                                $"Invalid enum value '{value}'. Valid: {string.Join(", ", names)}");
                        prop.enumValueIndex = idx;
                    }
                    break;

                case SerializedPropertyType.Color:
                    prop.colorValue = ParseColor(value);
                    break;

                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ParseVector2(value);
                    break;

                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ParseVector3(value);
                    break;

                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ParseVector4(value);
                    break;

                case SerializedPropertyType.Vector2Int:
                    var v2 = ParseVector2(value);
                    prop.vector2IntValue = new Vector2Int((int)v2.x, (int)v2.y);
                    break;

                case SerializedPropertyType.Vector3Int:
                    var v3 = ParseVector3(value);
                    prop.vector3IntValue = new Vector3Int((int)v3.x, (int)v3.y, (int)v3.z);
                    break;

                case SerializedPropertyType.LayerMask:
                    if (!int.TryParse(value, out var maskVal))
                        throw new ArgumentException($"Cannot parse '{value}' as LayerMask int.");
                    prop.intValue = maskVal;
                    break;

                case SerializedPropertyType.ObjectReference:
                    SetObjectReference(prop, value);
                    break;

                default:
                    throw new ArgumentException(
                        $"Setting property type '{prop.propertyType}' is not supported.");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            return $"Set {component.GetType().Name}.{propertyPath} = {value}";
        }

        private static Vector2 ParseVector2(string value)
        {
            var nums = ParseFloatTuple(value, 2);
            return new Vector2(nums[0], nums[1]);
        }

        private static Vector3 ParseVector3(string value)
        {
            var nums = ParseFloatTuple(value, 3);
            return new Vector3(nums[0], nums[1], nums[2]);
        }

        private static Vector4 ParseVector4(string value)
        {
            var nums = ParseFloatTuple(value, 4);
            return new Vector4(nums[0], nums[1], nums[2], nums[3]);
        }

        private static Color ParseColor(string value)
        {
            var nums = ParseFloatTuple(value, 4);
            return new Color(nums[0], nums[1], nums[2], nums[3]);
        }

        private static float[] ParseFloatTuple(string value, int expected)
        {
            var cleaned = value.Trim().TrimStart('(').TrimEnd(')');
            var parts = cleaned.Split(',');
            if (parts.Length != expected)
                throw new ArgumentException(
                    $"Expected {expected} comma-separated values, got {parts.Length} in '{value}'.");

            var result = new float[expected];
            for (int i = 0; i < expected; i++)
            {
                if (!float.TryParse(parts[i].Trim(), out result[i]))
                    throw new ArgumentException($"Cannot parse '{parts[i].Trim()}' as float.");
            }
            return result;
        }

        private static void SetObjectReference(SerializedProperty prop, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "None" || value == "null")
            {
                prop.objectReferenceValue = null;
                return;
            }

            // Determine if this is an asset path or a scene GameObject reference
            if (value.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                // Asset reference - check if this is a sub-asset reference (e.g., "path/to/asset.mixer:BGM")
                if (value.Contains(":"))
                {
                    // Sub-asset reference
                    var parts = value.Split(':');
                    if (parts.Length != 2)
                        throw new ArgumentException($"Invalid sub-asset path format: '{value}'. Expected 'Assets/path/to/asset.ext:SubAssetName'.");

                    var mainAssetPath = parts[0];
                    var subAssetName = parts[1];

                    // Load all assets at the path
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(mainAssetPath);
                    if (allAssets == null || allAssets.Length == 0)
                        throw new ArgumentException($"No assets found at path: '{mainAssetPath}'.");

                    // Find the sub-asset by name
                    var subAsset = System.Array.Find(allAssets, a => a.name == subAssetName);
                    if (subAsset == null)
                    {
                        var availableNames = string.Join(", ", System.Array.ConvertAll(allAssets, a => $"'{a.name}'"));
                        throw new ArgumentException($"Sub-asset '{subAssetName}' not found in '{mainAssetPath}'. Available sub-assets: {availableNames}");
                    }

                    prop.objectReferenceValue = subAsset;
                }
                else
                {
                    // Main asset reference (existing logic)
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
                    if (asset == null)
                        throw new ArgumentException($"Asset not found at path: '{value}'.");

                    prop.objectReferenceValue = asset;
                }
            }
            else
            {
                // Check if this is an InstanceID reference (works for any UnityEngine.Object)
                if (value.StartsWith("#") && int.TryParse(value.Substring(1), out var instanceId))
                {
#if UNITY_6000_0_OR_NEWER
#pragma warning disable CS0618
                    var obj = EditorUtility.EntityIdToObject(instanceId);
#pragma warning restore CS0618
#else
                    var obj = EditorUtility.InstanceIDToObject(instanceId);
#endif
                    if (obj == null)
                        throw new ArgumentException($"No object found with InstanceID {instanceId}.");
                    prop.objectReferenceValue = obj;
                    return;
                }

                // Check if this specifies a Component type explicitly: "GameObjectPath::ComponentType"
                string componentTypeName = null;
                string gameObjectPath = value;

                if (value.Contains("::"))
                {
                    var parts = value.Split(new[] { "::" }, System.StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        gameObjectPath = parts[0];
                        componentTypeName = parts[1];
                    }
                }

                // Scene GameObject reference
                var go = GameObjectResolver.Resolve(gameObjectPath);

                // Determine expected type
                System.Type expectedType;
                if (!string.IsNullOrEmpty(componentTypeName))
                {
                    // Type explicitly specified
                    expectedType = ComponentResolver.ResolveType(componentTypeName);
                }
                else
                {
                    // Try to infer from existing value
                    expectedType = prop.objectReferenceValue != null
                        ? prop.objectReferenceValue.GetType()
                        : typeof(UnityEngine.Object);
                }

                // If property expects a Component type, try to get that component
                if (typeof(UnityEngine.Component).IsAssignableFrom(expectedType) && expectedType != typeof(Transform))
                {
                    var comp = go.GetComponent(expectedType);
                    if (comp == null)
                        throw new ArgumentException($"GameObject '{go.name}' does not have component of type '{expectedType.Name}'.");
                    prop.objectReferenceValue = comp;
                }
                else
                {
                    // Otherwise, assign the GameObject itself
                    prop.objectReferenceValue = go;
                }
            }
        }

        public static string SetArraySize(UnityEngine.Component component, string propertyPath, int size)
        {
            if (size < 0)
                throw new ArgumentException("Array size must be non-negative.");

            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyPath}' not found on {component.GetType().Name}.");

            if (!prop.isArray || prop.propertyType == SerializedPropertyType.String)
                throw new ArgumentException($"Property '{propertyPath}' is not an array.");

            Undo.RecordObject(component, $"Set array size {propertyPath} to {size}");
            prop.arraySize = size;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            return $"Set {component.GetType().Name}.{propertyPath} array size to {size}";
        }

        public static string InsertArrayElement(UnityEngine.Component component, string propertyPath, int index)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyPath}' not found on {component.GetType().Name}.");

            if (!prop.isArray || prop.propertyType == SerializedPropertyType.String)
                throw new ArgumentException($"Property '{propertyPath}' is not an array.");

            Undo.RecordObject(component, $"Insert array element at {propertyPath}[{index}]");

            if (index < 0 || index >= prop.arraySize)
            {
                // Append at the end
                prop.InsertArrayElementAtIndex(prop.arraySize);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
                return $"Appended element to {component.GetType().Name}.{propertyPath} (new size: {prop.arraySize})";
            }
            else
            {
                prop.InsertArrayElementAtIndex(index);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);
                return $"Inserted element at {component.GetType().Name}.{propertyPath}[{index}] (new size: {prop.arraySize})";
            }
        }

        public static string DeleteArrayElement(UnityEngine.Component component, string propertyPath, int index)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyPath);
            if (prop == null)
                throw new ArgumentException($"Property '{propertyPath}' not found on {component.GetType().Name}.");

            if (!prop.isArray || prop.propertyType == SerializedPropertyType.String)
                throw new ArgumentException($"Property '{propertyPath}' is not an array.");

            if (index < 0 || index >= prop.arraySize)
                throw new ArgumentException($"Index {index} out of range. Array size is {prop.arraySize}.");

            Undo.RecordObject(component, $"Delete array element at {propertyPath}[{index}]");
            prop.DeleteArrayElementAtIndex(index);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            return $"Deleted element at {component.GetType().Name}.{propertyPath}[{index}] (new size: {prop.arraySize})";
        }

        public static string AddUnityEventListener(UnityEngine.Component component, string eventPropertyPath, string listenerTarget, string methodName)
        {
            var so = new SerializedObject(component);
            var eventProp = so.FindProperty(eventPropertyPath);
            if (eventProp == null)
                throw new ArgumentException($"UnityEvent property '{eventPropertyPath}' not found on {component.GetType().Name}.");

            // Get m_PersistentCalls.m_Calls array
            var callsProp = eventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (callsProp == null || !callsProp.isArray)
                throw new ArgumentException($"Property '{eventPropertyPath}' is not a valid UnityEvent (m_PersistentCalls.m_Calls not found).");

            // Resolve listener target (GameObject or Component)
            UnityEngine.Object listenerObject = null;
            try
            {
                // Try to resolve as GameObject first
                var listenerGo = GameObjectResolver.Resolve(listenerTarget);

                // Check if target specifies a component (path contains '/')
                if (listenerTarget.Contains("/"))
                {
                    // Last part is component name
                    var parts = listenerTarget.Split('/');
                    var componentName = parts[parts.Length - 1];
                    listenerObject = ComponentResolver.Resolve(listenerGo, componentName);
                }
                else
                {
                    // Try to find a component with the same name as GameObject
                    var components = listenerGo.GetComponents<UnityEngine.Component>();
                    var matchingComp = Array.Find(components, c => c.GetType().Name == listenerGo.name);
                    listenerObject = matchingComp ?? (UnityEngine.Object)listenerGo;
                }
            }
            catch
            {
                throw new ArgumentException($"Listener target '{listenerTarget}' not found.");
            }

            // Add new persistent call
            Undo.RecordObject(component, $"Add UnityEvent listener {methodName} to {eventPropertyPath}");
            int newIndex = callsProp.arraySize;
            callsProp.InsertArrayElementAtIndex(newIndex);
            var newCall = callsProp.GetArrayElementAtIndex(newIndex);

            // Set target
            var targetProp = newCall.FindPropertyRelative("m_Target");
            if (targetProp != null)
                targetProp.objectReferenceValue = listenerObject;

            // Set method name
            var methodNameProp = newCall.FindPropertyRelative("m_MethodName");
            if (methodNameProp != null)
                methodNameProp.stringValue = methodName;

            // Set mode (0 = EventDefined, 1 = Void, etc.)
            var modeProp = newCall.FindPropertyRelative("m_Mode");
            if (modeProp != null)
                modeProp.enumValueIndex = 1; // 1 = Void (no parameters)

            // Set call state (2 = RuntimeOnly)
            var callStateProp = newCall.FindPropertyRelative("m_CallState");
            if (callStateProp != null)
                callStateProp.enumValueIndex = 2; // 2 = RuntimeOnly

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            return $"Added UnityEvent listener: {listenerObject.GetType().Name}.{methodName} to {component.GetType().Name}.{eventPropertyPath}";
        }
    }

    // ========== Data Types ==========

    /// <summary>ヒエラルキーノードの表示情報</summary>
    private static class HierarchyNodeInfo
    {
        public static void AppendTo(StringBuilder sb, GameObject go, int currentDepth, int maxDepth)
        {
            var indent = new string(' ', currentDepth * 2);
            var activeMarker = go.activeSelf ? "" : " [Inactive]";
            var childCount = go.transform.childCount;
            var childInfo = childCount > 0 ? $" ({childCount} children)" : "";

            sb.AppendLine($"{indent}- {go.name} [ID:{go.GetEntityId()}]{activeMarker}{childInfo}");

            if (currentDepth < maxDepth)
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    AppendTo(sb, go.transform.GetChild(i).gameObject, currentDepth + 1, maxDepth);
                }
            }
        }
    }

    /// <summary>GameObjectの詳細情報</summary>
    private sealed class GameObjectInfo
    {
        public string Name;
        public EntityId InstanceID;
        public bool ActiveSelf;
        public bool ActiveInHierarchy;
        public string Tag;
        public string Layer;
        public string[] Components;
        public string[] Children;

        public static GameObjectInfo From(GameObject go)
        {
            var components = go.GetComponents<UnityEngine.Component>();
            var children = new string[go.transform.childCount];
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                children[i] = $"{child.name} [ID:{child.gameObject.GetEntityId()}]";
            }

            return new GameObjectInfo
            {
                Name = go.name,
                InstanceID = go.GetEntityId(),
                ActiveSelf = go.activeSelf,
                ActiveInHierarchy = go.activeInHierarchy,
                Tag = go.tag,
                Layer = LayerMask.LayerToName(go.layer),
                Components = components
                    .Select(c => c != null ? c.GetType().Name : "(missing script)")
                    .ToArray(),
                Children = children
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"GameObject: {Name}");
            sb.AppendLine($"  InstanceID: {InstanceID}");
            sb.AppendLine($"  ActiveSelf: {ActiveSelf}");
            sb.AppendLine($"  ActiveInHierarchy: {ActiveInHierarchy}");
            sb.AppendLine($"  Tag: {Tag}");
            sb.AppendLine($"  Layer: {Layer}");
            sb.AppendLine($"  Components ({Components.Length}):");
            for (int i = 0; i < Components.Length; i++)
                sb.AppendLine($"    [{i}] {Components[i]}");
            if (Children.Length > 0)
            {
                sb.AppendLine($"  Children ({Children.Length}):");
                foreach (var child in Children)
                    sb.AppendLine($"    - {child}");
            }

            return sb.ToString();
        }
    }

    /// <summary>コンポーネントの詳細情報（プロパティ一覧付き）</summary>
    private sealed class ComponentInfo
    {
        public string GameObjectName;
        public string ComponentType;
        public List<PropertySummary> Properties;

        public static ComponentInfo From(UnityEngine.Component component)
        {
            return new ComponentInfo
            {
                GameObjectName = component.gameObject.name,
                ComponentType = component.GetType().Name,
                Properties = PropertyAccessor.ReadAll(component)
            };
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Component: {ComponentType} (on '{GameObjectName}')");
            sb.AppendLine($"  Properties ({Properties.Count}):");
            foreach (var prop in Properties)
            {
                sb.AppendLine($"    {prop.Path} ({prop.Type}) = {prop.Value}");
            }

            return sb.ToString();
        }
    }

    /// <summary>SerializedPropertyの要約情報</summary>
    private sealed class PropertySummary
    {
        public string Path;
        public string Type;
        public string Value;

        public static PropertySummary From(SerializedProperty property)
        {
            return new PropertySummary
            {
                Path = property.propertyPath,
                Type = property.propertyType.ToString(),
                Value = ReadValue(property)
            };
        }

        private static string ReadValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("G");
                case SerializedPropertyType.String:
                    return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 &&
                           prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})"
                        : "None";
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.Vector2Int:
                    return prop.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return prop.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return prop.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return prop.boundsIntValue.ToString();
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.AnimationCurve:
                    return $"AnimationCurve ({prop.animationCurveValue?.length ?? 0} keys)";
                default:
                    return $"({prop.propertyType})";
            }
        }
    }

    [McpServerTool, Description("Invoke a method on a ScriptableObject asset (e.g., ImportAll on AdvScenarioDataProject). Supports Undo.")]
    public async ValueTask<string> Ins_InvokeAssetMethod(
        [Description("Path to the asset (e.g. 'Assets/MCPTest/MCPTest.project.asset').")]
        string assetPath,
        [Description("Method name to invoke (e.g. 'ImportAll').")]
        string methodName)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                return $"Error: Asset not found at path: {assetPath}";
            }

            var method = asset.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (method == null)
            {
                return $"Error: Method '{methodName}' not found on type {asset.GetType().Name}";
            }

            Undo.RecordObject(asset, $"Invoke {methodName}");
            method.Invoke(asset, null);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return $"Successfully invoked {methodName}() on {assetPath}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Invoke a method with a single string argument on a ScriptableObject asset (e.g., ImportFromJSON on GrowthDatabase). Supports Undo.")]
    public async ValueTask<string> Ins_InvokeAssetMethodWithStringArg(
        [Description("Path to the asset (e.g. 'Assets/Model/GrowthDatabase.asset').")]
        string assetPath,
        [Description("Method name to invoke (e.g. 'ImportFromJSON').")]
        string methodName,
        [Description("String argument to pass to the method (e.g., JSON content or file path).")]
        string argument)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                return $"Error: Asset not found at path: {assetPath}";
            }

            var method = asset.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);

            if (method == null)
            {
                return $"Error: Method '{methodName}(string)' not found on type {asset.GetType().Name}";
            }

            Undo.RecordObject(asset, $"Invoke {methodName}");
            method.Invoke(asset, new object[] { argument });
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return $"Successfully invoked {methodName}(string) on {assetPath}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Invoke ImportFromJSON on a GrowthDatabase by reading a JSON file. Supports Undo.")]
    public async ValueTask<string> Ins_ImportJSONFromFile(
        [Description("Path to the GrowthDatabase asset (e.g. 'Assets/Model/GrowthDatabase.asset').")]
        string assetPath,
        [Description("Path to the JSON file to import (e.g. 'D:/Tozawa_Unity/WoodSimulator/forest_growth_data.json').")]
        string jsonFilePath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (!System.IO.File.Exists(jsonFilePath))
            {
                return $"Error: JSON file not found at path: {jsonFilePath}";
            }

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
            {
                return $"Error: Asset not found at path: {assetPath}";
            }

            var method = asset.GetType().GetMethod("ImportFromJSON",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);

            if (method == null)
            {
                return $"Error: Method 'ImportFromJSON(string)' not found on type {asset.GetType().Name}";
            }

            string jsonText = System.IO.File.ReadAllText(jsonFilePath);
            Undo.RecordObject(asset, "Import JSON");
            method.Invoke(asset, new object[] { jsonText });
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return $"Successfully imported JSON from {jsonFilePath} to {assetPath}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
