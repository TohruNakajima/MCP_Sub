using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

[McpServerToolType, Description("プレハブアセット本体のシリアライズフィールドを直接書き換えるツール")]
internal sealed class PrefabEditorTool
{
    [McpServerTool, Description("プレハブアセットの指定コンポーネントの ObjectReference 型プロパティに別アセット(AudioClip/Material/Texture/Mesh 等)参照を設定する。YAML を直接書き換えず PrefabUtility 経由で保存するため安全。")]
    public async ValueTask<string> Prefab_SetComponentObjectRef(
        [Description("プレハブアセットのパス (例: 'Assets/Path/To/Prefab.prefab')")]
        string prefabPath,
        [Description("コンポーネント型名 (例: 'lb_Bird', 'AudioSource'). 大文字小文字区別あり")]
        string componentType,
        [Description("シリアライズプロパティのパス (例: 'song1', 'm_audioClip')")]
        string propertyPath,
        [Description("参照するアセットのパス (例: 'Assets/Path/To/sound.wav')")]
        string valueAssetPath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(valueAssetPath);
            if (asset == null) return $"Value asset not found: {valueAssetPath}";

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null) return $"Prefab not found: {prefabPath}";

            try
            {
                // ルートの Component を優先、見つからなければ子を含め名前一致で検索
                UnityEngine.Component comp = null;
                var rootComps = contents.GetComponents<UnityEngine.Component>();
                foreach (var c in rootComps)
                {
                    if (c == null) continue;
                    var n = c.GetType().Name;
                    if (string.Equals(n, componentType, StringComparison.Ordinal))
                    {
                        comp = c;
                        break;
                    }
                }
                if (comp == null)
                {
                    var all = contents.GetComponentsInChildren<UnityEngine.Component>(true);
                    foreach (var c in all)
                    {
                        if (c == null) continue;
                        if (string.Equals(c.GetType().Name, componentType, StringComparison.Ordinal))
                        {
                            comp = c;
                            break;
                        }
                    }
                }
                if (comp == null) return $"Component '{componentType}' not found on prefab";

                var so = new SerializedObject(comp);
                var prop = so.FindProperty(propertyPath);
                if (prop == null) return $"Property '{propertyPath}' not found on '{componentType}'";
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    return $"Property '{propertyPath}' is not an ObjectReference (type={prop.propertyType})";

                prop.objectReferenceValue = asset;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return $"Set {componentType}.{propertyPath} = {valueAssetPath} on {prefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
