using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tool to build Unity player with EditorUserBuildSettings API.
/// </summary>
[McpServerToolType, Description("Build Unity player")]
internal sealed class BuildPlayerTool
{
    [McpServerTool, Description("Build Unity player for specified platform. Automatically adds current scene to build settings if not already added.")]
    public async ValueTask<string> BuildPlayer(
            string outputPath,
            string platform = "Windows",
            string architecture = "x64"
        )
        {
            try
            {
                await UniTask.SwitchToMainThread();
                return BuildPlayerOnMainThread(outputPath, platform);
            }
            catch (Exception e)
            {
                if (string.Equals(platform, "webgl", StringComparison.OrdinalIgnoreCase))
                    WebGlVoskBackupUtility.RestoreIfAbandoned();
                Debug.LogError($"[BuildPlayer] Exception: {e.Message}\n{e.StackTrace}");
                return $"Build error: {e.Message}";
            }
        }

        static string BuildPlayerOnMainThread(string outputPath, string platform)
        {
            try
            {
                // Parse platform
                BuildTarget buildTarget;
                switch (platform.ToLower())
                {
                    case "windows":
                        buildTarget = BuildTarget.StandaloneWindows64;
                        break;
                    case "windows64":
                        buildTarget = BuildTarget.StandaloneWindows64;
                        break;
                    case "windows32":
                        buildTarget = BuildTarget.StandaloneWindows;
                        break;
                    case "macos":
                    case "mac":
                        buildTarget = BuildTarget.StandaloneOSX;
                        break;
                    case "linux":
                        buildTarget = BuildTarget.StandaloneLinux64;
                        break;
                    case "webgl":
                        buildTarget = BuildTarget.WebGL;
                        break;
                    default:
                        return $"Unsupported platform: {platform}. Use 'Windows', 'Mac', 'Linux', or 'WebGL'.";
                }

                if (!IsBuildTargetAvailable(buildTarget))
                {
                    return DescribeMissingBuildSupport(buildTarget);
                }

                // Get current scene
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!currentScene.IsValid() || string.IsNullOrEmpty(currentScene.path))
                {
                    return "No active scene. Please open a scene before building.";
                }

                // Add scene to build settings if not already added
                var scenesInBuild = EditorBuildSettings.scenes.ToList();
                bool sceneExists = scenesInBuild.Any(s => s.path == currentScene.path);

                if (!sceneExists)
                {
                    Debug.Log($"[BuildPlayer] Adding scene to build settings: {currentScene.path}");
                    scenesInBuild.Add(new EditorBuildSettingsScene(currentScene.path, true));
                    EditorBuildSettings.scenes = scenesInBuild.ToArray();
                }

                // Ensure output path has correct extension
                string extension = "";
                bool isWebGL = false;
                switch (buildTarget)
                {
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        extension = ".exe";
                        break;
                    case BuildTarget.StandaloneOSX:
                        extension = ".app";
                        break;
                    case BuildTarget.StandaloneLinux64:
                        extension = "";
                        break;
                    case BuildTarget.WebGL:
                        extension = "";
                        isWebGL = true;
                        break;
                }

                if (!outputPath.EndsWith(extension) && !string.IsNullOrEmpty(extension))
                {
                    outputPath += extension;
                }

                // Create output directory if needed
                string outputDir = isWebGL ? outputPath : Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Debug.Log($"[BuildPlayer] Created output directory: {outputDir}");
                }

                // Setup build options
                BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = outputPath,
                    target = buildTarget,
                    options = BuildOptions.None
                };

                // Execute build
                Debug.Log($"[BuildPlayer] Starting build: {outputPath}");
                Debug.Log($"[BuildPlayer] Platform: {buildTarget}");
                Debug.Log($"[BuildPlayer] Scenes: {string.Join(", ", buildPlayerOptions.scenes)}");

                BuildReport report;
                try
                {
                    report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                }
                catch (Exception ex)
                {
                    if (buildTarget == BuildTarget.WebGL)
                        WebGlVoskBackupUtility.RestoreIfAbandoned();

                    if (ex is UnityException ue)
                    {
                        Debug.LogWarning($"[BuildPlayer] {ue.Message}");
                        if (buildTarget == BuildTarget.WebGL &&
                            (ue.Message.IndexOf("WebGL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             ue.Message.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0))
                            return DescribeMissingBuildSupport(buildTarget);
                        return $"Build error: {ue.Message}";
                    }

                    Debug.LogWarning($"[BuildPlayer] {ex.GetType().Name}: {ex.Message}");
                    return $"Build error: {ex.Message}";
                }

                if (report == null)
                {
                    if (buildTarget == BuildTarget.WebGL)
                        WebGlVoskBackupUtility.RestoreIfAbandoned();
                    return $"Build failed: BuildPipeline.BuildPlayer returned null. Build target '{buildTarget}' may not be supported or installed.";
                }

                BuildSummary summary = report.summary;

                if (buildTarget == BuildTarget.WebGL && summary.result != BuildResult.Succeeded)
                    WebGlVoskBackupUtility.RestoreIfAbandoned();

                if (summary.result == BuildResult.Succeeded)
                {
                    string sizeStr = (summary.totalSize / 1024.0 / 1024.0).ToString("F2");
                    return $"Build succeeded: {outputPath}\nSize: {sizeStr} MB\nTime: {summary.totalTime.TotalSeconds:F1}s";
                }
                else if (summary.result == BuildResult.Failed)
                {
                    return $"Build failed with {summary.totalErrors} errors";
                }
                else if (summary.result == BuildResult.Cancelled)
                {
                    return "Build cancelled by user";
                }
                else
                {
                    return $"Build finished with result: {summary.result}";
                }
            }
            catch (Exception e)
            {
                if (string.Equals(platform, "webgl", StringComparison.OrdinalIgnoreCase))
                    WebGlVoskBackupUtility.RestoreIfAbandoned();
                Debug.LogError($"[BuildPlayer] Exception: {e.Message}\n{e.StackTrace}");
                return $"Build error: {e.Message}";
            }
        }

        static bool IsBuildTargetAvailable(BuildTarget target)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                return false;
            if (target == BuildTarget.WebGL)
                return WebGlEditorModuleCheck.IsWebGlBuildPipelineReady();
            return true;
        }

        static string DescribeMissingBuildSupport(BuildTarget target)
        {
            if (target == BuildTarget.WebGL)
            {
                return "WebGL はこの Unity インストールでは利用できません（WebGL Build Support 未導入の可能性が高いです）。\n" +
                       "Unity Hub → 該当バージョンの歯車 → **Add modules** → **WebGL Build Support** にチェックを入れてインストールし、Unity を再起動してから再度ビルドしてください。";
            }

            return $"ビルドターゲット '{target}' がサポートされていないか、対応モジュールがインストールされていません。Unity Hub で **Add modules** を確認してください。";
        }
}
