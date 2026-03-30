using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Visual Engine Asset Converter操作用MCPツール
/// </summary>
[McpServerToolType, Description("Convert prefabs using The Visual Engine Asset Converter")]
internal sealed class TVEAssetConverterTool
{
    [McpServerTool, Description("Convert a prefab using The Visual Engine Asset Converter with specified preset")]
    public async ValueTask<string> TVE_ConvertPrefab(
        [Description("Path to the prefab to convert (e.g., 'Assets/Prefabs/JapaneseCedar.prefab')")]
        string prefabPath,
        [Description("Preset name (e.g., 'Universal', 'SpeedTree', 'Tree Creator'). Default: 'Universal'")]
        string presetName = "Universal",
        [Description("Option name (e.g., 'Default', 'Bark', 'Leaves'). Default: 'Default'")]
        string optionName = "Default")
    {
        try
        {
            await UniTask.SwitchToMainThread();

            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException("prefabPath is required");
            }

            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabObject == null)
            {
                throw new ArgumentException($"Prefab not found at path: {prefabPath}");
            }

            // Get TVEAssetConverter window type
            Type converterType = GetTVEAssetConverterType();
            if (converterType == null)
            {
                throw new InvalidOperationException("TVEAssetConverter type not found. Is The Visual Engine installed?");
            }

            // Create or get existing window instance
            EditorWindow window = EditorWindow.GetWindow(converterType, false, "Asset Converter", false);

            if (window == null)
            {
                throw new InvalidOperationException("Failed to create Asset Converter window");
            }

            // Set selection to the prefab
            Selection.activeObject = prefabObject;

            // Trigger OnSelectionChange to update the window's prefab list
            MethodInfo onSelectionChangeMethod = converterType.GetMethod("OnSelectionChange", BindingFlags.NonPublic | BindingFlags.Instance);
            if (onSelectionChangeMethod != null)
            {
                onSelectionChangeMethod.Invoke(window, null);
            }

            // Wait a frame for the window to update
            await UniTask.DelayFrame(1);

            // Get prefabObjects field
            FieldInfo prefabObjectsField = converterType.GetField("prefabObjects", BindingFlags.NonPublic | BindingFlags.Instance);
            if (prefabObjectsField == null)
            {
                throw new InvalidOperationException("prefabObjects field not found in TVEAssetConverter");
            }

            var prefabObjects = prefabObjectsField.GetValue(window) as System.Collections.IList;
            if (prefabObjects == null || prefabObjects.Count == 0)
            {
                throw new InvalidOperationException($"No prefabs loaded. Make sure {prefabPath} is valid and supported by TVE.");
            }

            // Set preset and option indices
            SetPresetAndOption(window, converterType, presetName, optionName);

            // Set flags to avoid dialogs
            SetConversionFlags(window, converterType);

            // Execute conversion
            bool converted = ExecuteConversion(window, converterType, prefabObjects, prefabPath);

            // Refresh and close window
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            window.Close();

            if (converted)
            {
                return $"Successfully converted {prefabPath} with preset '{presetName}' and option '{optionName}'";
            }
            else
            {
                return $"Warning: Prefab at {prefabPath} may already be converted or is unsupported.";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"TVE_ConvertPrefab error: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private Type GetTVEAssetConverterType()
    {
        // Try common type names
        Type converterType = Type.GetType("TheVisualEngine.TVEAssetConverter, Assembly-CSharp-Editor");
        if (converterType != null) return converterType;

        converterType = Type.GetType("TheVisualEngine.TVEAssetConverter, TVE.Editor");
        if (converterType != null) return converterType;

        // Search in all assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            converterType = assembly.GetType("TheVisualEngine.TVEAssetConverter");
            if (converterType != null) return converterType;
        }

        return null;
    }

    private void SetPresetAndOption(EditorWindow window, Type converterType, string presetName, string optionName)
    {
        // Get PresetsEnum and OptionsEnum fields
        FieldInfo presetsEnumField = converterType.GetField("PresetsEnum", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo optionsEnumField = converterType.GetField("OptionsEnum", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo presetIndexField = converterType.GetField("presetIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo optionIndexField = converterType.GetField("optionIndex", BindingFlags.NonPublic | BindingFlags.Instance);

        if (presetsEnumField != null && presetIndexField != null)
        {
            string[] presetsEnum = presetsEnumField.GetValue(window) as string[];
            if (presetsEnum != null)
            {
                int presetIdx = Array.IndexOf(presetsEnum, presetName);
                if (presetIdx >= 0)
                {
                    presetIndexField.SetValue(window, presetIdx);
                    Debug.Log($"TVE_ConvertPrefab: Set preset to '{presetName}' (index {presetIdx})");
                }
                else
                {
                    Debug.LogWarning($"TVE_ConvertPrefab: Preset '{presetName}' not found. Available presets: {string.Join(", ", presetsEnum)}");
                }
            }
        }

        if (optionsEnumField != null && optionIndexField != null)
        {
            string[] optionsEnum = optionsEnumField.GetValue(window) as string[];
            if (optionsEnum != null)
            {
                int optionIdx = Array.IndexOf(optionsEnum, optionName);
                if (optionIdx >= 0)
                {
                    optionIndexField.SetValue(window, optionIdx);
                    Debug.Log($"TVE_ConvertPrefab: Set option to '{optionName}' (index {optionIdx})");
                }
                else
                {
                    Debug.LogWarning($"TVE_ConvertPrefab: Option '{optionName}' not found. Available options: {string.Join(", ", optionsEnum)}");
                }
            }
        }
    }

    private bool ExecuteConversion(EditorWindow window, Type converterType, System.Collections.IList prefabObjects, string prefabPath)
    {
        MethodInfo convertPrefabMethod = converterType.GetMethod("ConvertPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
        if (convertPrefabMethod == null)
        {
            throw new InvalidOperationException("ConvertPrefab method not found in TVEAssetConverter");
        }

        // Get the prefab data type
        Type prefabDataType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            prefabDataType = assembly.GetType("TheVisualEngine.TVEPrefabData");
            if (prefabDataType != null) break;
        }

        if (prefabDataType == null)
        {
            throw new InvalidOperationException("TVEPrefabData type not found");
        }

        // Get RevertPrefab method for reconverting already converted prefabs
        MethodInfo revertPrefabMethod = converterType.GetMethod("RevertPrefab", BindingFlags.NonPublic | BindingFlags.Instance);

        // Convert the first supported or converted prefab
        bool converted = false;
        foreach (var prefabData in prefabObjects)
        {
            // Check status field
            FieldInfo statusField = prefabDataType.GetField("status");
            if (statusField != null)
            {
                var status = statusField.GetValue(prefabData);
                string statusStr = status.ToString();

                // If already converted, revert first then convert
                if (statusStr == "Converted")
                {
                    if (revertPrefabMethod != null)
                    {
                        Debug.Log($"TVE_ConvertPrefab: Reverting already converted prefab {prefabPath}...");
                        revertPrefabMethod.Invoke(window, new object[] { prefabData });
                        Debug.Log($"TVE_ConvertPrefab: Now converting {prefabPath}...");
                        convertPrefabMethod.Invoke(window, new object[] { prefabData });
                        converted = true;
                        EditorUtility.ClearProgressBar();
                        break;
                    }
                }
                // TVEPrefabMode.Supported = 1
                else if (statusStr == "Supported")
                {
                    Debug.Log($"TVE_ConvertPrefab: Converting {prefabPath}...");
                    convertPrefabMethod.Invoke(window, new object[] { prefabData });
                    converted = true;
                    EditorUtility.ClearProgressBar();
                    break;
                }
            }
        }

        return converted;
    }

    private void SetConversionFlags(EditorWindow window, Type converterType)
    {
        // Set keepConvertedMaterials to false (reconvert materials)
        FieldInfo keepConvertedMaterialsField = converterType.GetField("keepConvertedMaterials", BindingFlags.NonPublic | BindingFlags.Instance);
        if (keepConvertedMaterialsField != null)
        {
            keepConvertedMaterialsField.SetValue(window, false);
            Debug.Log("TVE_ConvertPrefab: Set keepConvertedMaterials to false (will reconvert materials)");
        }

        // Set keepConvertedMaterialsSet to true (skip dialog)
        FieldInfo keepConvertedMaterialsSetField = converterType.GetField("keepConvertedMaterialsSet", BindingFlags.NonPublic | BindingFlags.Instance);
        if (keepConvertedMaterialsSetField != null)
        {
            keepConvertedMaterialsSetField.SetValue(window, true);
            Debug.Log("TVE_ConvertPrefab: Set keepConvertedMaterialsSet to true (skip dialog)");
        }

        // Set keepConvertedPrefabs to false (reconvert prefabs)
        FieldInfo keepConvertedPrefabsField = converterType.GetField("keepConvertedPrefabs", BindingFlags.NonPublic | BindingFlags.Instance);
        if (keepConvertedPrefabsField != null)
        {
            keepConvertedPrefabsField.SetValue(window, false);
            Debug.Log("TVE_ConvertPrefab: Set keepConvertedPrefabs to false (will reconvert prefabs)");
        }
    }
}
