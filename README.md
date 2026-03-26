# TozawaMCP-Toolkit

Unity MCP (Model Context Protocol) サーバー環境をサブモジュール1つで即座にセットアップするツールキット。

## 含まれるコンポーネント

| コンポーネント | 説明 |
|---|---|
| カスタムMCPツール | InspectorTool (9メソッド), LogToUnityConsole, CreateEmptyGameObject, RefreshAssets |
| CustomMcpToolBuilder | `[McpServerToolType]` 属性の自動検出・登録ビルダー |
| McpLogBridge | ファイルベースのコンソールログブリッジ |
| NuGet DLL群 (21個) | ModelContextProtocol, Microsoft.Extensions.* 等 |
| McpPackageInstaller | manifest.json 自動設定スクリプト（UniTask + フォーク版NMCP） |

## 自動セットアップ

サブモジュール追加後にUnityを開くと、`McpPackageInstaller` が自動的に以下をmanifest.jsonに追加します:

- **UniTask**: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`
- **UnityNaturalMCP (fork)**: `https://github.com/nakagimadevelop002-glitch/UnityNaturalMCP.git?path=/UnityNaturalMCPServer`

## インストール手順

### 新規プロジェクト
```bash
cd <Unityプロジェクトルート>
git submodule add https://github.com/nakagimadevelop002-glitch/TozawaMCP-Toolkit.git Assets/TozawaMCP
```
Unityエディタを開く → manifest.jsonが自動更新 → パッケージ解決 → 完了

### 既存プロジェクト（NuGet/UniTask/NMCP導入済み）
1. manifest.json から以下を**削除**:
   - `com.cysharp.unitask`
   - `jp.notargs.unity-natural-mcp`
   - `com.github-glitchenzo.nugetforunity`
2. `Assets/Packages/` フォルダを削除
3. `Assets/packages.config` を削除
4. サブモジュールを追加:
   ```bash
   git submodule add https://github.com/nakagimadevelop002-glitch/TozawaMCP-Toolkit.git Assets/TozawaMCP
   ```
5. Unityエディタを開いて確認

## プロジェクト固有ツールの追加方法

サブモジュール外にMCPツールを追加する場合、別のasmdef + ビルダーが必要です:

1. 新しいフォルダ（例: `Assets/Editor/MyProjectMcpTools/`）を作成
2. 以下を作成:
   - `MyProjectMcpTools.Editor.asmdef`（references: `UnityNaturalMCP.Editor`, `UniTask`）
   - `MyProjectMcpToolBuilder.cs`（`McpBuilderScriptableObject` を継承）
   - ツールの `.cs` ファイル
3. Unity上で CreateAssetMenu > UnityNaturalMCP から `.asset` を作成

## NMCPフォーク変更点

元リポジトリ: `notargs/UnityNaturalMCP`

- `McpUnityEditorTool.cs` から `RefreshAssets` メソッドを削除（カスタム `RefreshAssetsTool` で代替）
- `UnityNaturalMCP.Editor.asmdef` から存在しない `ModelContextProtocol.Core.dll` 参照を削除

## サブモジュール更新

ツールキットの最新版を取得:
```bash
cd Assets/TozawaMCP
git pull origin main
cd ../..
git add Assets/TozawaMCP
git commit -m "Update TozawaMCP-Toolkit"
```
