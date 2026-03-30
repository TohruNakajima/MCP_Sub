using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// マテリアルURP変換・修復ツール。
/// Built-in/Autodesk Interactive → URP Lit変換、テクスチャ自動検出・割当、ピンクマテリアル修復。
/// </summary>
[McpServerToolType, Description("Upgrade materials to URP, auto-detect textures, repair broken materials")]
internal sealed class MaterialConverterTool
{
    // Built-inシェーダー名一覧（URP変換対象）
    private static readonly HashSet<string> BuiltInShaderNames = new HashSet<string>
    {
        "Standard", "Standard (Specular setup)",
        "Autodesk Interactive", "Autodesk Interactive Transparent",
        "Legacy Shaders/Diffuse", "Legacy Shaders/Specular",
        "Legacy Shaders/Bumped Diffuse", "Legacy Shaders/Bumped Specular",
        "Legacy Shaders/Transparent/Diffuse", "Legacy Shaders/Transparent/Specular",
        "Legacy Shaders/Self-Illumin/Diffuse",
        "Mobile/Diffuse", "Mobile/Bumped Diffuse",
        "Particles/Standard Surface", "Particles/Standard Unlit"
    };

    // テクスチャサフィックスとURPプロパティのマッピング
    private static readonly (string suffix, string urpProperty)[] TextureSuffixMap =
    {
        ("_N", "_BumpMap"),
        ("_Normal", "_BumpMap"),
        ("_AO", "_OcclusionMap"),
        ("_Occlusion", "_OcclusionMap"),
        // _R (Roughness) は直接マッピング先がないためスキップ
        ("_M", "_MetallicGlossMap"),
        ("_Metallic", "_MetallicGlossMap"),
        ("_E", "_EmissionMap"),
        ("_Emission", "_EmissionMap"),
        ("_S", "_SpecGlossMap"),
    };

    /// <summary>マテリアル名からテクスチャを自動検出して割り当て</summary>
    private static int AutoAssignTextures(Material mat, string matAssetPath)
    {
        var matDir = Path.GetDirectoryName(matAssetPath);
        var matName = Path.GetFileNameWithoutExtension(matAssetPath);
        int assigned = 0;

        // マテリアルと同階層 + 親フォルダの Textures/ を検索
        var searchFolders = new List<string> { matDir };
        var parentDir = Path.GetDirectoryName(matDir);
        if (parentDir != null)
        {
            var texturesDir = Path.Combine(parentDir, "Textures").Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(texturesDir))
                searchFolders.Add(texturesDir);
        }

        // アセットのルートから上位を探索
        var current = matDir;
        for (int i = 0; i < 3; i++)
        {
            current = Path.GetDirectoryName(current);
            if (current == null) break;
            var texDir = Path.Combine(current, "Textures").Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(texDir) && !searchFolders.Contains(texDir))
                searchFolders.Add(texDir);
        }

        var texGuids = AssetDatabase.FindAssets("t:Texture2D", searchFolders.ToArray());
        var texByName = new Dictionary<string, Texture2D>();
        foreach (var guid in texGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            texByName[name] = null; // lazy load
            texByName[name + "_key"] = null;
            // Store path for lazy loading
            texByName["_path_" + name] = null;
        }

        // ベーステクスチャ（_BaseMap）が空なら、マテリアル名と一致するテクスチャを探す
        if (mat.HasProperty("_BaseMap"))
        {
            var currentTex = mat.GetTexture("_BaseMap");
            if (currentTex == null)
            {
                // マテリアル名に完全一致するテクスチャを探す
                var baseTex = FindTextureInFolders(matName, searchFolders);
                if (baseTex != null)
                {
                    mat.SetTexture("_BaseMap", baseTex);
                    assigned++;
                }
            }
        }

        // サフィックスマッピングでテクスチャを探す
        foreach (var (suffix, urpProp) in TextureSuffixMap)
        {
            if (!mat.HasProperty(urpProp)) continue;
            var currentTex = mat.GetTexture(urpProp);
            if (currentTex != null) continue;

            var texName = matName + suffix;
            var tex = FindTextureInFolders(texName, searchFolders);
            if (tex != null)
            {
                mat.SetTexture(urpProp, tex);
                assigned++;

                // ノーマルマップのキーワード有効化
                if (urpProp == "_BumpMap")
                    mat.EnableKeyword("_NORMALMAP");
                if (urpProp == "_EmissionMap")
                    mat.EnableKeyword("_EMISSION");
                if (urpProp == "_OcclusionMap")
                    mat.EnableKeyword("_OCCLUSIONMAP");
            }
        }

        return assigned;
    }

    private static Texture2D FindTextureInFolders(string textureName, List<string> folders)
    {
        foreach (var folder in folders)
        {
            var guids = AssetDatabase.FindAssets($"{textureName} t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == textureName)
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }
        return null;
    }

    /// <summary>旧シェーダーのプロパティを保存してURP Litに復元</summary>
    private static void PreserveAndConvert(Material mat, Shader urpLit)
    {
        // 変換前にプロパティ保存
        var mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        var bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
        var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
        var metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
        var smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") :
                         mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;
        var emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
        var emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
        var occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
        var metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;

        // シェーダー変更
        mat.shader = urpLit;

        // URP Litプロパティに復元
        if (mainTex != null && mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", mainTex);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (bumpMap != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", bumpMap);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);
        if (metallicMap != null && mat.HasProperty("_MetallicGlossMap"))
            mat.SetTexture("_MetallicGlossMap", metallicMap);
        if (occlusionMap != null && mat.HasProperty("_OcclusionMap"))
        {
            mat.SetTexture("_OcclusionMap", occlusionMap);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }
        if (emissionMap != null && mat.HasProperty("_EmissionMap"))
        {
            mat.SetTexture("_EmissionMap", emissionMap);
            mat.EnableKeyword("_EMISSION");
        }
        if (emissionColor != Color.black && mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emissionColor);
            mat.EnableKeyword("_EMISSION");
        }
    }

    [McpServerTool, Description("Upgrade all materials in a folder to URP Lit. Converts Built-in/Autodesk Interactive shaders, preserves textures, and auto-detects missing textures by naming convention. Also repairs already-converted materials with missing textures.")]
    public async ValueTask<string> Material_UpgradeToURP(
        [Description("Folder path (e.g. 'Assets/MyAssets'). Searches recursively.")]
        string folderPath,
        [Description("If true, only report without changing. Default: false.")]
        bool dryRun = false)
    {
        await UniTask.SwitchToMainThread();

        if (!AssetDatabase.IsValidFolder(folderPath))
            return $"Invalid folder path: {folderPath}";

        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            return "URP Lit shader not found. Is URP installed?";

        var guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        if (guids.Length == 0)
            return $"No materials found in {folderPath}";

        int converted = 0, repaired = 0, skipped = 0;
        var sb = new System.Text.StringBuilder();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            var shaderName = mat.shader != null ? mat.shader.name : "(null)";
            var matName = Path.GetFileNameWithoutExtension(path);
            bool isBroken = mat.shader == null || shaderName == "Hidden/InternalErrorShader";
            bool isBuiltIn = BuiltInShaderNames.Contains(shaderName);
            bool isAlreadyURP = shaderName.StartsWith("Universal Render Pipeline/");

            if (isAlreadyURP)
            {
                // 既にURPだがテクスチャが欠けている場合は修復
                bool hasBaseMap = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null;
                if (!hasBaseMap)
                {
                    if (dryRun)
                    {
                        sb.AppendLine($"  [DRY-REPAIR] {matName}: auto-assign textures");
                        repaired++;
                    }
                    else
                    {
                        int texAssigned = AutoAssignTextures(mat, path);
                        if (texAssigned > 0)
                        {
                            EditorUtility.SetDirty(mat);
                            sb.AppendLine($"  [REPAIR] {matName}: {texAssigned} textures auto-assigned");
                            repaired++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }
                else
                {
                    skipped++;
                }
                continue;
            }

            if (!isBroken && !isBuiltIn)
            {
                skipped++;
                continue;
            }

            if (dryRun)
            {
                sb.AppendLine($"  [DRY] {matName}: {shaderName} -> URP/Lit");
                converted++;
                continue;
            }

            // シェーダー変換（テクスチャ保持）
            PreserveAndConvert(mat, urpLit);

            // テクスチャが空なら自動検出
            int autoAssigned = AutoAssignTextures(mat, path);

            EditorUtility.SetDirty(mat);
            var texInfo = autoAssigned > 0 ? $" (+{autoAssigned} textures)" : "";
            sb.AppendLine($"  [OK] {matName}: {shaderName} -> URP/Lit{texInfo}");
            converted++;
        }

        if (!dryRun && (converted > 0 || repaired > 0))
            AssetDatabase.SaveAssets();

        var header = dryRun ? "[DRY RUN] " : "";
        return $"{header}Converted: {converted}, Repaired: {repaired}, Skipped: {skipped}, Total: {guids.Length}\n{sb}";
    }

    [McpServerTool, Description("Scan for broken (pink) or texture-missing materials in a folder.")]
    public async ValueTask<string> Material_FindProblems(
        [Description("Folder path (e.g. 'Assets'). Searches recursively.")]
        string folderPath = "Assets")
    {
        await UniTask.SwitchToMainThread();

        if (!AssetDatabase.IsValidFolder(folderPath))
            return $"Invalid folder path: {folderPath}";

        var guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        var broken = new List<string>();
        var noTexture = new List<string>();
        var nonURP = new List<string>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            var shaderName = mat.shader != null ? mat.shader.name : "(null)";

            if (mat.shader == null || shaderName == "Hidden/InternalErrorShader")
            {
                broken.Add($"  [BROKEN] {path}");
            }
            else if (BuiltInShaderNames.Contains(shaderName))
            {
                nonURP.Add($"  [NON-URP] {path} ({shaderName})");
            }
            else if (shaderName.StartsWith("Universal Render Pipeline/"))
            {
                bool hasBaseMap = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null;
                if (!hasBaseMap)
                    noTexture.Add($"  [NO-TEX] {path}");
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Scanned {guids.Length} materials in {folderPath}:");
        if (broken.Count > 0) { sb.AppendLine($"\nBroken shaders ({broken.Count}):"); broken.ForEach(b => sb.AppendLine(b)); }
        if (nonURP.Count > 0) { sb.AppendLine($"\nNon-URP shaders ({nonURP.Count}):"); nonURP.ForEach(n => sb.AppendLine(n)); }
        if (noTexture.Count > 0) { sb.AppendLine($"\nMissing base texture ({noTexture.Count}):"); noTexture.ForEach(n => sb.AppendLine(n)); }
        if (broken.Count == 0 && nonURP.Count == 0 && noTexture.Count == 0)
            sb.AppendLine("No problems found.");

        return sb.ToString();
    }

    [McpServerTool, Description("List all unique shader names used by materials in a folder.")]
    public async ValueTask<string> Material_ListShaders(
        [Description("Folder path (e.g. 'Assets/MyAssets').")]
        string folderPath = "Assets")
    {
        await UniTask.SwitchToMainThread();

        if (!AssetDatabase.IsValidFolder(folderPath))
            return $"Invalid folder path: {folderPath}";

        var guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        var shaderCounts = new Dictionary<string, int>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            var name = mat.shader != null ? mat.shader.name : "(null)";
            if (!shaderCounts.ContainsKey(name)) shaderCounts[name] = 0;
            shaderCounts[name]++;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Shaders in {folderPath} ({guids.Length} materials):");
        foreach (var kv in shaderCounts.OrderByDescending(x => x.Value))
            sb.AppendLine($"  [{kv.Value}] {kv.Key}");

        return sb.ToString();
    }
}
