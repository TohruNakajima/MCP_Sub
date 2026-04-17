using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[McpServerToolType, Description("シーン全体の Renderer を一括最適化するツール")]
internal sealed class SceneOptimizerTool
{
    [McpServerTool, Description("アクティブシーン内の全 MeshRenderer/SkinnedMeshRenderer に対して、指定プロパティを一括設定する。対象プロパティ: lightProbeUsage (0=Off/1=BlendProbes) / reflectionProbeUsage (0=Off/1=BlendProbes) / receiveShadows (true/false) / motionVectors (0=Camera/1=Object/2=ForceNoMotion) / shadowCastingMode (0=Off/1=On/2=TwoSided/3=ShadowsOnly) / allowOcclusionWhenDynamic (true/false) / staticShadowCaster (true/false)。")]
    public async ValueTask<string> Scene_SetAllRendererProperty(
        [Description("プロパティ名")]
        string property,
        [Description("値。enum系は数値、bool は true/false")]
        string value,
        [Description("対象ルートGameObject名 (省略で全ルート対象)")]
        string rootName = null)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var scene = SceneManager.GetActiveScene();
            var roots = new List<GameObject>();
            if (string.IsNullOrEmpty(rootName))
            {
                roots.AddRange(scene.GetRootGameObjects());
            }
            else
            {
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == rootName) roots.Add(root);
                if (roots.Count == 0) return $"Root '{rootName}' not found in scene '{scene.name}'";
            }

            int count = 0;
            int skipped = 0;
            foreach (var root in roots)
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    Undo.RecordObject(r, "Scene_SetAllRendererProperty");
                    if (ApplyProperty(r, property, value))
                    {
                        EditorUtility.SetDirty(r);
                        count++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return $"Set {property}={value} on {count} Renderers (skipped {skipped}) in scene '{scene.name}'";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    private static bool ApplyProperty(Renderer r, string prop, string val)
    {
        switch (prop.ToLowerInvariant())
        {
            case "lightprobeusage":
                if (int.TryParse(val, out int lpu))
                {
                    r.lightProbeUsage = (UnityEngine.Rendering.LightProbeUsage)lpu;
                    return true;
                }
                return false;
            case "reflectionprobeusage":
                if (int.TryParse(val, out int rpu))
                {
                    r.reflectionProbeUsage = (UnityEngine.Rendering.ReflectionProbeUsage)rpu;
                    return true;
                }
                return false;
            case "receiveshadows":
                if (bool.TryParse(val, out bool rs))
                {
                    r.receiveShadows = rs;
                    return true;
                }
                return false;
            case "motionvectors":
                if (int.TryParse(val, out int mv))
                {
                    r.motionVectorGenerationMode = (MotionVectorGenerationMode)mv;
                    return true;
                }
                return false;
            case "shadowcastingmode":
                if (int.TryParse(val, out int scm))
                {
                    r.shadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode)scm;
                    return true;
                }
                return false;
            case "allowocclusionwhendynamic":
                if (bool.TryParse(val, out bool aow))
                {
                    r.allowOcclusionWhenDynamic = aow;
                    return true;
                }
                return false;
            case "staticshadowcaster":
                if (bool.TryParse(val, out bool ssc))
                {
                    r.staticShadowCaster = ssc;
                    return true;
                }
                return false;
        }
        return false;
    }
}
