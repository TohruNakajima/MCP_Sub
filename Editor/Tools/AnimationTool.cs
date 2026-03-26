using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Animation関連の全操作を提供するMCPツール
/// AnimatorController、AnimationClip、State、Transition、Curve、Eventの作成・編集を完全自動化
/// </summary>
[McpServerToolType, Description("Create and edit Animator Controllers, Animation Clips, States, Transitions, Curves, and Events.")]
internal sealed class AnimationTool
{
    // ========== 1. AnimatorController作成 ==========

    [McpServerTool, Description("Create a new AnimatorController asset at the specified path. Supports Undo.")]
    public async ValueTask<string> Anim_CreateAnimatorController(
        [Description("Path to save the AnimatorController (e.g. 'Assets/Animations/MyController.controller'). Must start with 'Assets/' and end with '.controller'.")]
        string assetPath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // パス検証
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new ArgumentException("assetPath cannot be null or empty.");
            }

            if (!assetPath.StartsWith("Assets/"))
            {
                throw new ArgumentException("assetPath must start with 'Assets/'.");
            }

            if (!assetPath.EndsWith(".controller"))
            {
                throw new ArgumentException("assetPath must end with '.controller'.");
            }

            // 既存アセット確認
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath) != null)
            {
                throw new InvalidOperationException($"AnimatorController already exists at '{assetPath}'.");
            }

            // ディレクトリ確認・作成
            string directory = System.IO.Path.GetDirectoryName(assetPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                CreateDirectoryRecursive(directory);
            }

            // AnimatorController作成
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(assetPath);

            if (controller == null)
            {
                throw new InvalidOperationException($"Failed to create AnimatorController at '{assetPath}'.");
            }

            // Undo登録
            Undo.RegisterCreatedObjectUndo(controller, "Create AnimatorController");

            // 保存
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Created AnimatorController at '{assetPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== 2. AnimationClip作成 ==========

    [McpServerTool, Description("Create a new AnimationClip asset at the specified path. Supports Undo.")]
    public async ValueTask<string> Anim_CreateAnimationClip(
        [Description("Path to save the AnimationClip (e.g. 'Assets/Animations/MyClip.anim'). Must start with 'Assets/' and end with '.anim'.")]
        string assetPath)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // パス検証
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new ArgumentException("assetPath cannot be null or empty.");
            }

            if (!assetPath.StartsWith("Assets/"))
            {
                throw new ArgumentException("assetPath must start with 'Assets/'.");
            }

            if (!assetPath.EndsWith(".anim"))
            {
                throw new ArgumentException("assetPath must end with '.anim'.");
            }

            // 既存アセット確認
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) != null)
            {
                throw new InvalidOperationException($"AnimationClip already exists at '{assetPath}'.");
            }

            // ディレクトリ確認・作成
            string directory = System.IO.Path.GetDirectoryName(assetPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                CreateDirectoryRecursive(directory);
            }

            // AnimationClip作成
            AnimationClip clip = new AnimationClip();

            // Undo登録
            Undo.RegisterCreatedObjectUndo(clip, "Create AnimationClip");

            // アセット保存
            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Created AnimationClip at '{assetPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== 3. Parameter追加 ==========

    [McpServerTool, Description("Add a parameter to an AnimatorController. Supports Int, Float, Bool, and Trigger types. Supports Undo.")]
    public async ValueTask<string> Anim_AddParameter(
        [Description("Path to the AnimatorController asset (e.g. 'Assets/Animations/MyController.controller').")]
        string controllerPath,
        [Description("Parameter name (e.g. 'Speed', 'IsJumping').")]
        string parameterName,
        [Description("Parameter type: 'Int', 'Float', 'Bool', or 'Trigger'.")]
        string parameterType)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // パス検証
            if (string.IsNullOrEmpty(controllerPath))
            {
                throw new ArgumentException("controllerPath cannot be null or empty.");
            }

            // AnimatorController読み込み
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException($"AnimatorController not found at '{controllerPath}'.");
            }

            // パラメータ名検証
            if (string.IsNullOrEmpty(parameterName))
            {
                throw new ArgumentException("parameterName cannot be null or empty.");
            }

            // 既存パラメータ確認
            if (controller.parameters.Any(p => p.name == parameterName))
            {
                throw new InvalidOperationException($"Parameter '{parameterName}' already exists in '{controllerPath}'.");
            }

            // パラメータタイプ解析
            AnimatorControllerParameterType paramType;
            switch (parameterType.ToLower())
            {
                case "int":
                    paramType = AnimatorControllerParameterType.Int;
                    break;
                case "float":
                    paramType = AnimatorControllerParameterType.Float;
                    break;
                case "bool":
                    paramType = AnimatorControllerParameterType.Bool;
                    break;
                case "trigger":
                    paramType = AnimatorControllerParameterType.Trigger;
                    break;
                default:
                    throw new ArgumentException($"Invalid parameterType '{parameterType}'. Valid types: Int, Float, Bool, Trigger.");
            }

            // Undo登録
            Undo.RecordObject(controller, "Add AnimatorController Parameter");

            // パラメータ追加
            controller.AddParameter(parameterName, paramType);

            // 保存
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return $"Added parameter '{parameterName}' (type: {parameterType}) to '{controllerPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== 4. State追加 ==========

    [McpServerTool, Description("Add a state with motion (AnimationClip) to an AnimatorController layer. Supports Undo.")]
    public async ValueTask<string> Anim_AddState(
        [Description("Path to the AnimatorController asset (e.g. 'Assets/Animations/MyController.controller').")]
        string controllerPath,
        [Description("State name (e.g. 'Idle', 'Walk', 'Jump').")]
        string stateName,
        [Description("Path to the AnimationClip to use as motion (optional, can be empty).")]
        string motionPath = "",
        [Description("Layer index (default: 0 for Base Layer).")]
        int layerIndex = 0)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // パス検証
            if (string.IsNullOrEmpty(controllerPath))
            {
                throw new ArgumentException("controllerPath cannot be null or empty.");
            }

            // AnimatorController読み込み
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException($"AnimatorController not found at '{controllerPath}'.");
            }

            // State名検証
            if (string.IsNullOrEmpty(stateName))
            {
                throw new ArgumentException("stateName cannot be null or empty.");
            }

            // Layer取得
            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
            {
                throw new ArgumentException($"Invalid layerIndex {layerIndex}. AnimatorController has {controller.layers.Length} layer(s).");
            }

            AnimatorControllerLayer layer = controller.layers[layerIndex];
            AnimatorStateMachine stateMachine = layer.stateMachine;

            // 既存State確認
            if (stateMachine.states.Any(s => s.state.name == stateName))
            {
                throw new InvalidOperationException($"State '{stateName}' already exists in layer {layerIndex} of '{controllerPath}'.");
            }

            // AnimationClip読み込み（オプション）
            AnimationClip motion = null;
            if (!string.IsNullOrEmpty(motionPath))
            {
                motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(motionPath);
                if (motion == null)
                {
                    throw new InvalidOperationException($"AnimationClip not found at '{motionPath}'.");
                }
            }

            // Undo登録
            Undo.RecordObject(stateMachine, "Add AnimatorState");

            // State作成
            AnimatorState state = stateMachine.AddState(stateName);
            if (motion != null)
            {
                state.motion = motion;
            }

            // 保存
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return $"Added state '{stateName}' to layer {layerIndex} of '{controllerPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== 5. Transition作成 ==========

    [McpServerTool, Description("Add a transition between states in an AnimatorController. Supports conditions. Supports Undo.")]
    public async ValueTask<string> Anim_AddTransition(
        [Description("Path to the AnimatorController asset (e.g. 'Assets/Animations/MyController.controller').")]
        string controllerPath,
        [Description("Source state name (use 'Any State' for any state transition).")]
        string sourceStateName,
        [Description("Destination state name.")]
        string destinationStateName,
        [Description("Parameter name for condition (optional, can be empty for no condition).")]
        string conditionParameter = "",
        [Description("Condition mode: 'Greater', 'Less', 'Equals', 'NotEqual', 'If' (bool true), 'IfNot' (bool false). Required if conditionParameter is specified.")]
        string conditionMode = "",
        [Description("Threshold value for Int/Float conditions (default: 0).")]
        float conditionThreshold = 0f,
        [Description("Layer index (default: 0 for Base Layer).")]
        int layerIndex = 0,
        [Description("Has exit time (default: false).")]
        bool hasExitTime = false,
        [Description("Exit time (default: 0).")]
        float exitTime = 0f,
        [Description("Transition duration (default: 0 for instant).")]
        float duration = 0f)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // パス検証
            if (string.IsNullOrEmpty(controllerPath))
            {
                throw new ArgumentException("controllerPath cannot be null or empty.");
            }

            // AnimatorController読み込み
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException($"AnimatorController not found at '{controllerPath}'.");
            }

            // Layer取得
            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
            {
                throw new ArgumentException($"Invalid layerIndex {layerIndex}. AnimatorController has {controller.layers.Length} layer(s).");
            }

            AnimatorControllerLayer layer = controller.layers[layerIndex];
            AnimatorStateMachine stateMachine = layer.stateMachine;

            // 遷移元State取得
            AnimatorState sourceState = null;
            bool isAnyState = sourceStateName.ToLower() == "any state" || sourceStateName.ToLower() == "anystate";

            if (!isAnyState)
            {
                sourceState = stateMachine.states.FirstOrDefault(s => s.state.name == sourceStateName).state;
                if (sourceState == null)
                {
                    throw new InvalidOperationException($"Source state '{sourceStateName}' not found in layer {layerIndex}.");
                }
            }

            // 遷移先State取得
            AnimatorState destinationState = stateMachine.states.FirstOrDefault(s => s.state.name == destinationStateName).state;
            if (destinationState == null)
            {
                throw new InvalidOperationException($"Destination state '{destinationStateName}' not found in layer {layerIndex}.");
            }

            // Undo登録
            Undo.RecordObject(stateMachine, "Add AnimatorTransition");

            // Transition作成
            AnimatorStateTransition transition;
            if (isAnyState)
            {
                transition = stateMachine.AddAnyStateTransition(destinationState);
            }
            else
            {
                transition = sourceState.AddTransition(destinationState);
            }

            // Transitionプロパティ設定
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.duration = duration;

            // Condition追加（オプション）
            if (!string.IsNullOrEmpty(conditionParameter))
            {
                // パラメータ存在確認
                var param = controller.parameters.FirstOrDefault(p => p.name == conditionParameter);
                if (param.name == null)
                {
                    throw new InvalidOperationException($"Parameter '{conditionParameter}' not found in controller.");
                }

                // Condition mode解析
                AnimatorConditionMode mode = ParseConditionMode(conditionMode, param.type);

                // Condition追加
                transition.AddCondition(mode, conditionThreshold, conditionParameter);
            }

            // 保存
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string source = isAnyState ? "Any State" : sourceStateName;
            return $"Added transition from '{source}' to '{destinationStateName}' in layer {layerIndex} of '{controllerPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== 6. AnimationCurve設定 ==========

    [McpServerTool, Description("Set an animation curve (keyframes) to an AnimationClip. Supports Undo.")]
    public async ValueTask<string> Anim_SetCurve(
        [Description("Path to the AnimationClip asset (e.g. 'Assets/Animations/MyClip.anim').")]
        string clipPath,
        [Description("GameObject hierarchy path (e.g. 'ChildObject' or empty for root).")]
        string relativePath,
        [Description("Type name (e.g. 'Transform', 'UnityEngine.Transform').")]
        string typeName,
        [Description("Property name (e.g. 'm_LocalPosition.x', 'm_LocalRotation.y').")]
        string propertyName,
        [Description("Keyframe times (comma-separated, e.g. '0,0.5,1').")]
        string keyframeTimes,
        [Description("Keyframe values (comma-separated, e.g. '0,1,0').")]
        string keyframeValues)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // パス検証
            if (string.IsNullOrEmpty(clipPath))
            {
                throw new ArgumentException("clipPath cannot be null or empty.");
            }

            // AnimationClip読み込み
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                throw new InvalidOperationException($"AnimationClip not found at '{clipPath}'.");
            }

            // プロパティ名検証
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("propertyName cannot be null or empty.");
            }

            // Type検証
            Type type = Type.GetType(typeName);
            if (type == null)
            {
                // 短縮名対応（Transform等）
                type = Type.GetType("UnityEngine." + typeName + ", UnityEngine");
            }
            if (type == null)
            {
                throw new ArgumentException($"Type '{typeName}' not found. Use full type name (e.g., 'UnityEngine.Transform').");
            }

            // Keyframe配列解析
            string[] timesStr = keyframeTimes.Split(',');
            string[] valuesStr = keyframeValues.Split(',');

            if (timesStr.Length != valuesStr.Length)
            {
                throw new ArgumentException($"Keyframe times and values count mismatch ({timesStr.Length} vs {valuesStr.Length}).");
            }

            if (timesStr.Length == 0)
            {
                throw new ArgumentException("At least one keyframe is required.");
            }

            Keyframe[] keyframes = new Keyframe[timesStr.Length];
            for (int i = 0; i < timesStr.Length; i++)
            {
                if (!float.TryParse(timesStr[i].Trim(), out float time))
                {
                    throw new ArgumentException($"Invalid keyframe time '{timesStr[i]}' at index {i}.");
                }
                if (!float.TryParse(valuesStr[i].Trim(), out float value))
                {
                    throw new ArgumentException($"Invalid keyframe value '{valuesStr[i]}' at index {i}.");
                }
                keyframes[i] = new Keyframe(time, value);
            }

            AnimationCurve curve = new AnimationCurve(keyframes);

            // Undo登録
            Undo.RecordObject(clip, "Set AnimationCurve");

            // Curve設定
            string path = string.IsNullOrEmpty(relativePath) ? "" : relativePath;
            clip.SetCurve(path, type, propertyName, curve);

            // 保存
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return $"Set curve for property '{propertyName}' on '{clipPath}' ({keyframes.Length} keyframes).";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== 7. AnimationEvent追加 ==========

    [McpServerTool, Description("Add an AnimationEvent to an AnimationClip at the specified time. Supports Undo.")]
    public async ValueTask<string> Anim_AddEvent(
        [Description("Path to the AnimationClip asset (e.g. 'Assets/Animations/MyClip.anim').")]
        string clipPath,
        [Description("Time in seconds when the event should fire.")]
        float time,
        [Description("Function name to call (e.g. 'OnAnimationComplete').")]
        string functionName,
        [Description("String parameter to pass to the function (optional, can be empty).")]
        string stringParameter = "",
        [Description("Int parameter to pass to the function (optional, default: 0).")]
        int intParameter = 0,
        [Description("Float parameter to pass to the function (optional, default: 0).")]
        float floatParameter = 0f)
    {
        try
        {
            await UniTask.SwitchToMainThread();

            // パス検証
            if (string.IsNullOrEmpty(clipPath))
            {
                throw new ArgumentException("clipPath cannot be null or empty.");
            }

            // AnimationClip読み込み
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                throw new InvalidOperationException($"AnimationClip not found at '{clipPath}'.");
            }

            // 関数名検証
            if (string.IsNullOrEmpty(functionName))
            {
                throw new ArgumentException("functionName cannot be null or empty.");
            }

            // AnimationEvent作成
            AnimationEvent animEvent = new AnimationEvent();
            animEvent.time = time;
            animEvent.functionName = functionName;

            // パラメータ設定
            if (!string.IsNullOrEmpty(stringParameter))
                animEvent.stringParameter = stringParameter;
            if (intParameter != 0)
                animEvent.intParameter = intParameter;
            if (floatParameter != 0f)
                animEvent.floatParameter = floatParameter;

            // Undo登録
            Undo.RecordObject(clip, "Add AnimationEvent");

            // Event追加
            var events = clip.events.ToList();
            events.Add(animEvent);
            clip.events = events.ToArray();

            // 保存
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return $"Added AnimationEvent '{functionName}' at time {time} to '{clipPath}'.";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    // ========== 共通ユーティリティ ==========

    /// <summary>
    /// Condition文字列をAnimatorConditionModeに変換
    /// </summary>
    private AnimatorConditionMode ParseConditionMode(string mode, AnimatorControllerParameterType paramType)
    {
        string modeLower = mode.ToLower();

        // Trigger型の場合
        if (paramType == AnimatorControllerParameterType.Trigger)
        {
            return AnimatorConditionMode.If;
        }

        // Bool型の場合
        if (paramType == AnimatorControllerParameterType.Bool)
        {
            if (modeLower == "true" || modeLower == "if")
                return AnimatorConditionMode.If;
            if (modeLower == "false" || modeLower == "ifnot")
                return AnimatorConditionMode.IfNot;
        }

        // Int/Float型の場合
        switch (modeLower)
        {
            case "greater":
            case ">":
                return AnimatorConditionMode.Greater;
            case "less":
            case "<":
                return AnimatorConditionMode.Less;
            case "equals":
            case "==":
                return AnimatorConditionMode.Equals;
            case "notequal":
            case "!=":
                return AnimatorConditionMode.NotEqual;
            default:
                throw new ArgumentException($"Invalid condition mode '{mode}' for parameter type {paramType}.");
        }
    }

    /// <summary>
    /// ディレクトリを再帰的に作成
    /// </summary>
    private void CreateDirectoryRecursive(string path)
    {
        string[] folders = path.Split('/');
        string currentPath = folders[0]; // "Assets"

        for (int i = 1; i < folders.Length; i++)
        {
            string nextPath = currentPath + "/" + folders[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                string guid = AssetDatabase.CreateFolder(currentPath, folders[i]);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new InvalidOperationException($"Failed to create folder '{nextPath}'.");
                }
            }
            currentPath = nextPath;
        }
    }
}
