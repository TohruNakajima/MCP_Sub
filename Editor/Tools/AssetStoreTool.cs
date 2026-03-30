using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Asset Storeパッケージのダウンロード・インポート自動化MCPツール。
/// </summary>
[McpServerToolType, Description("Download, search, import, and manage Asset Store packages")]
internal sealed class AssetStoreTool
{
    private static string GetAssetStoreCachePath()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(roaming, "Unity", "Asset Store-5.x");
    }

    private static string ParseProductId(string url)
    {
        var match = Regex.Match(url, @"-(\d+)$");
        if (match.Success) return match.Groups[1].Value;
        if (Regex.IsMatch(url.Trim(), @"^\d+$")) return url.Trim();
        return null;
    }

    private static HashSet<string> GetExistingCacheFiles()
    {
        var cachePath = GetAssetStoreCachePath();
        if (!Directory.Exists(cachePath))
            return new HashSet<string>();
        return new HashSet<string>(Directory.GetFiles(cachePath, "*.unitypackage", SearchOption.AllDirectories));
    }

    /// <summary>ServicesContainerのフィールドからサービスインスタンスを直接取得</summary>
    private static object FindServiceInstance(string typeName, out string error)
    {
        error = "";
        try
        {
            // ServicesContainer型を取得
            Type containerType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                var c = types.FirstOrDefault(x => x.Name == "ServicesContainer");
                if (c != null) { containerType = c; break; }
            }

            if (containerType == null) { error = "ServicesContainer type not found."; return null; }

            // ScriptableSingleton<T>.instance は基底クラスのstaticプロパティ → FlattenHierarchy必須
            var instanceProp = containerType.GetProperty("instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (instanceProp == null) { error = "instance property not found (with FlattenHierarchy)."; return null; }

            var container = instanceProp.GetValue(null);
            if (container == null) { error = "ServicesContainer instance is null."; return null; }

            // フィールド名マッピング: TypeName → m_SerializedTypeName
            var fieldName = $"m_Serialized{typeName}";
            var field = containerType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (field != null)
            {
                var service = field.GetValue(container);
                if (service != null) return service;
                error = $"{fieldName} field is null.";
                return null;
            }

            // フォールバック: Resolve<T>()
            var resolveMethod = containerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethod);

            if (resolveMethod != null)
            {
                Type targetType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    var t = types.FirstOrDefault(x => x.Name == typeName);
                    if (t != null) { targetType = t; break; }
                }

                if (targetType != null)
                {
                    var genericResolve = resolveMethod.MakeGenericMethod(targetType);
                    var service = genericResolve.Invoke(container, null);
                    if (service != null) return service;
                }
            }

            error = $"Field '{fieldName}' not found and Resolve failed.";
            return null;
        }
        catch (Exception e)
        {
            error = $"FindService error: {e.InnerException?.Message ?? e.Message}";
            return null;
        }
    }

    /// <summary>リフレクションでダウンロードを実行</summary>
    private static bool TryTriggerDownloadViaReflection(string productId, out string message)
    {
        message = "";
        if (!long.TryParse(productId, out long productIdLong))
        {
            message = $"Cannot parse productId '{productId}' as long.";
            return false;
        }

        var downloadManager = FindServiceInstance("AssetStoreDownloadManager", out message);
        if (downloadManager == null) return false;

        try
        {
            var downloadMethod = downloadManager.GetType().GetMethod("Download",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(long) }, null);

            if (downloadMethod != null)
            {
                downloadMethod.Invoke(downloadManager, new object[] { productIdLong });
                message = $"Download triggered for product {productId}.";
                return true;
            }

            message = "Download(Int64) method not found on AssetStoreDownloadManager.";
            return false;
        }
        catch (Exception e)
        {
            message = $"Download invocation failed: {e.InnerException?.Message ?? e.Message}";
            return false;
        }
    }

    /// <summary>Package Managerウィンドウをリフレクションで開く</summary>
    private static bool OpenPackageManagerWindow()
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }

                var windowType = types.FirstOrDefault(t =>
                    t.Name == "PackageManagerWindow" && t.Namespace != null && t.Namespace.Contains("PackageManager"));

                if (windowType != null && typeof(EditorWindow).IsAssignableFrom(windowType))
                {
                    EditorWindow.GetWindow(windowType, false, "Package Manager", true);
                    Debug.Log("[AssetStore] Package Manager window opened via EditorWindow.GetWindow.");
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AssetStore] Failed to open Package Manager window: {e.Message}");
        }
        return false;
    }

    /// <summary>Package Manager初期化を待ってからダウンロード開始</summary>
    private static void StartDelayedDownload(long productId, bool interactive, int timeoutSeconds)
    {
        // まずPackage Managerウィンドウを開く
        OpenPackageManagerWindow();

        double startTime = EditorApplication.timeSinceStartup;
        int retryCount = 0;
        EditorApplication.CallbackFunction delayedStart = null;

        delayedStart = () =>
        {
            if (EditorApplication.timeSinceStartup - startTime > 30)
            {
                EditorApplication.update -= delayedStart;
                Debug.LogWarning($"[AssetStore] ServicesContainer initialization timed out for product {productId}.");
                return;
            }

            retryCount++;
            if (retryCount % 30 != 0) return;

            bool downloadOk = TryTriggerDownloadViaReflection(productId.ToString(), out string msg);
            if (downloadOk)
            {
                EditorApplication.update -= delayedStart;
                Debug.Log($"[AssetStore] {msg}");
                StartDownloadMonitor(productId, interactive, timeoutSeconds);
            }
            else
            {
                Debug.Log($"[AssetStore] Retry {retryCount / 30}: {msg}");
            }
        };

        EditorApplication.update += delayedStart;
    }

    /// <summary>リフレクションでインポートを実行</summary>
    private static bool TryTriggerInstallViaReflection(long productIdLong, bool interactive, out string message)
    {
        message = "";
        var installer = FindServiceInstance("AssetStorePackageInstaller", out message);
        if (installer == null) return false;

        try
        {
            var installMethod = installer.GetType().GetMethod("Install",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(long), typeof(bool) }, null);

            if (installMethod != null)
            {
                installMethod.Invoke(installer, new object[] { productIdLong, interactive });
                message = $"Install triggered for product {productIdLong}.";
                return true;
            }

            message = "Install(Int64, Boolean) method not found.";
            return false;
        }
        catch (Exception e)
        {
            message = $"Install invocation failed: {e.InnerException?.Message ?? e.Message}";
            return false;
        }
    }

    [McpServerTool, Description("Download and import an Asset Store package automatically. Triggers download via internal API and auto-imports when complete. Use Asset Store URL or product ID.")]
    public async ValueTask<string> AssetStore_DownloadAndImport(
        [Description("Asset Store URL (e.g. 'https://assetstore.unity.com/packages/.../name-12345') or product ID (e.g. '12345').")]
        string urlOrId,
        [Description("If true, show the import dialog. If false, import all assets silently. Default: false.")]
        bool interactive = false,
        [Description("Timeout in seconds for monitoring download completion. Default: 300 (5 minutes).")]
        int timeoutSeconds = 300)
    {
        await UniTask.SwitchToMainThread();

        var productId = ParseProductId(urlOrId);
        if (string.IsNullOrEmpty(productId))
            return $"Could not parse product ID from: {urlOrId}";

        long productIdLong = long.Parse(productId);

        // kharmaプロトコルでPackage Managerを該当アセットで開く（ServicesContainer初期化も兼ねる）
        Application.OpenURL($"com.unity3d.kharma:content/{productId}");

        // 遅延実行でServicesContainer初期化を待ってからダウンロード開始
        StartDelayedDownload(productIdLong, interactive, timeoutSeconds);

        return $"Product ID: {productId}\nPackage Managerを初期化中。ダウンロード→インポートを自動実行します（タイムアウト: {timeoutSeconds}秒）。";
    }

    private static void StartDownloadMonitor(long productId, bool interactive, int timeoutSeconds)
    {
        double startTime = EditorApplication.timeSinceStartup;
        float lastCheckTime = 0;
        bool importTriggered = false;
        EditorApplication.CallbackFunction monitor = null;

        monitor = () =>
        {
            if (EditorApplication.timeSinceStartup - startTime > timeoutSeconds)
            {
                EditorApplication.update -= monitor;
                Debug.Log($"[AssetStore] Monitor timed out for product {productId} ({timeoutSeconds}s)");
                return;
            }

            if (EditorApplication.timeSinceStartup - lastCheckTime < 2f)
                return;
            lastCheckTime = (float)EditorApplication.timeSinceStartup;

            if (importTriggered) return;

            try
            {
                // ダウンロード状態をチェック
                var downloadManager = FindServiceInstance("AssetStoreDownloadManager", out _);
                if (downloadManager == null) return;

                var getOpMethod = downloadManager.GetType().GetMethod("GetDownloadOperation",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getOpMethod == null) return;

                // Nullable<long>パラメータ
                var operation = getOpMethod.Invoke(downloadManager, new object[] { (long?)productId });
                if (operation == null)
                {
                    // ダウンロードオペレーションが無い = まだ開始されてないか完了済み
                    // キャッシュにファイルがあるか確認
                    var cache = FindServiceInstance("AssetStoreCache", out _);
                    if (cache == null) return;

                    var getLocalInfo = cache.GetType().GetMethod("GetLocalInfo",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (getLocalInfo == null) return;

                    var localInfo = getLocalInfo.Invoke(cache, new object[] { (long?)productId });
                    if (localInfo != null)
                    {
                        // ローカルにある → インポート実行
                        importTriggered = true;
                        EditorApplication.update -= monitor;
                        Debug.Log($"[AssetStore] Package {productId} is available locally. Triggering install...");
                        TryTriggerInstallViaReflection(productId, interactive, out var installMsg);
                        Debug.Log($"[AssetStore] {installMsg}");
                    }
                    return;
                }

                // ダウンロード進行状況を確認（isInProgressプロパティ等）
                var stateProp = operation.GetType().GetProperty("state",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (stateProp != null)
                {
                    var state = stateProp.GetValue(operation)?.ToString();
                    if (state == "Completed" || state == "4") // Completed state
                    {
                        importTriggered = true;
                        EditorApplication.update -= monitor;
                        Debug.Log($"[AssetStore] Download completed for {productId}. Triggering install...");
                        TryTriggerInstallViaReflection(productId, interactive, out var installMsg);
                        Debug.Log($"[AssetStore] {installMsg}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetStore] Monitor error: {e.Message}");
            }
        };

        EditorApplication.update += monitor;
        Debug.Log($"[AssetStore] Download monitor started for product {productId}");
    }

    [McpServerTool, Description("Search for downloaded .unitypackage files in the local Asset Store cache.")]
    public async ValueTask<string> AssetStore_SearchCache(
        [Description("Keyword to search for in package file names. Case-insensitive.")]
        string keyword)
    {
        await UniTask.SwitchToMainThread();

        var cachePath = GetAssetStoreCachePath();
        if (!Directory.Exists(cachePath))
            return $"Asset Store cache not found: {cachePath}";

        var files = Directory.GetFiles(cachePath, "*.unitypackage", SearchOption.AllDirectories);
        var matches = files
            .Where(f => Path.GetFileNameWithoutExtension(f).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();

        if (matches.Length == 0)
        {
            if (files.Length == 0) return "No .unitypackage files found in cache.";
            return $"No matches for '{keyword}'. Available ({files.Length}):\n" +
                   string.Join("\n", files.Select(f => $"  {Path.GetFileNameWithoutExtension(f)}"));
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {matches.Length} package(s) matching '{keyword}':");
        foreach (var m in matches)
        {
            var info = new FileInfo(m);
            sb.AppendLine($"  [{info.Length / 1024 / 1024}MB] {Path.GetFileNameWithoutExtension(m)}");
            sb.AppendLine($"    Path: {m}");
        }
        return sb.ToString();
    }

    [McpServerTool, Description("List all downloaded .unitypackage files in the local Asset Store cache.")]
    public async ValueTask<string> AssetStore_ListCache()
    {
        await UniTask.SwitchToMainThread();

        var cachePath = GetAssetStoreCachePath();
        if (!Directory.Exists(cachePath))
            return $"Asset Store cache not found: {cachePath}";

        var files = Directory.GetFiles(cachePath, "*.unitypackage", SearchOption.AllDirectories);
        if (files.Length == 0) return "No .unitypackage files in cache.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Cached packages ({files.Length}):");
        foreach (var f in files.OrderBy(x => x))
        {
            var info = new FileInfo(f);
            sb.AppendLine($"  [{info.Length / 1024 / 1024}MB] {Path.GetFileNameWithoutExtension(f)}");
        }
        return sb.ToString();
    }

    [McpServerTool, Description("Import a .unitypackage file into the project. Can use full path or search by keyword in cache.")]
    public async ValueTask<string> AssetStore_ImportPackage(
        [Description("Full path to .unitypackage file, OR a keyword to search in cache.")]
        string packagePathOrKeyword,
        [Description("If true, show import dialog. Default: false.")]
        bool interactive = false)
    {
        await UniTask.SwitchToMainThread();

        string packagePath = packagePathOrKeyword;

        if (!File.Exists(packagePath))
        {
            var cachePath = GetAssetStoreCachePath();
            if (!Directory.Exists(cachePath))
                return $"File not found and cache does not exist: {cachePath}";

            var files = Directory.GetFiles(cachePath, "*.unitypackage", SearchOption.AllDirectories);
            var matches = files
                .Where(f => Path.GetFileNameWithoutExtension(f).IndexOf(packagePathOrKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            if (matches.Length == 0)
                return $"No package found matching '{packagePathOrKeyword}'.";
            if (matches.Length > 1)
                return $"Multiple matches:\n" + string.Join("\n", matches.Select(m => $"  {Path.GetFileNameWithoutExtension(m)}"));

            packagePath = matches[0];
        }

        try
        {
            AssetDatabase.ImportPackage(packagePath, interactive);
            return $"Import started: {Path.GetFileNameWithoutExtension(packagePath)}";
        }
        catch (Exception e)
        {
            return $"Import failed: {e.Message}";
        }
    }

    [McpServerTool, Description("Open Unity Package Manager window.")]
    public async ValueTask<string> AssetStore_OpenPackageManager()
    {
        await UniTask.SwitchToMainThread();
        try
        {
            EditorApplication.ExecuteMenuItem("Window/Package Manager");
        }
        catch
        {
            // フォールバック: kharmaプロトコルでPMを起動
            Application.OpenURL("com.unity3d.kharma:content/0");
        }
        return "Package Manager opened.";
    }

    [McpServerTool, Description("Open an Asset Store URL in the browser.")]
    public async ValueTask<string> AssetStore_OpenURL(
        [Description("Asset Store URL")]
        string url)
    {
        await UniTask.SwitchToMainThread();
        Application.OpenURL(url);
        return $"Opened: {url}";
    }

    [McpServerTool, Description("Debug: Inspect ServicesContainer and PackageManagerWindow internals.")]
    public async ValueTask<string> AssetStore_DebugInternalAPI()
    {
        await UniTask.SwitchToMainThread();

        var sb = new System.Text.StringBuilder();
        var bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // ServicesContainer調査
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }

            var sc = types.FirstOrDefault(t => t.Name == "ServicesContainer");
            if (sc == null) continue;

            sb.AppendLine($"ServicesContainer: {sc.FullName}");
            sb.AppendLine($"  BaseType: {sc.BaseType?.FullName}");
            sb.AppendLine($"  IsAbstract: {sc.IsAbstract}, IsSealed: {sc.IsSealed}");

            sb.AppendLine("  Properties:");
            foreach (var p in sc.GetProperties(bf))
                sb.AppendLine($"    {(p.GetMethod?.IsStatic == true ? "static " : "")}{p.PropertyType.Name} {p.Name}");

            sb.AppendLine("  Fields:");
            foreach (var f in sc.GetFields(bf))
                sb.AppendLine($"    {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}");

            sb.AppendLine("  Methods:");
            foreach (var m in sc.GetMethods(bf).Where(m => m.DeclaringType == sc).Take(20))
            {
                var parms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                sb.AppendLine($"    {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({parms})");
            }

            // instanceプロパティの値を試す
            var instProp = sc.GetProperty("instance", bf);
            if (instProp != null)
            {
                try
                {
                    var val = instProp.GetValue(null);
                    sb.AppendLine($"  instance value: {val ?? "null"}");
                }
                catch (Exception e)
                {
                    sb.AppendLine($"  instance access error: {e.Message}");
                }
            }

            // PackageManagerWindow調査
            var pmw = types.FirstOrDefault(t => t.Name == "PackageManagerWindow");
            if (pmw != null)
            {
                sb.AppendLine($"\nPackageManagerWindow: {pmw.FullName}");
                sb.AppendLine("  Fields containing 'service' or 'container':");
                foreach (var f in pmw.GetFields(bf))
                {
                    if (f.Name.ToLower().Contains("service") || f.Name.ToLower().Contains("container")
                        || f.FieldType.Name.Contains("Services") || f.FieldType.Name.Contains("Container"))
                        sb.AppendLine($"    {f.FieldType.Name} {f.Name}");
                }
            }

            break;
        }

        return sb.ToString();
    }
}
