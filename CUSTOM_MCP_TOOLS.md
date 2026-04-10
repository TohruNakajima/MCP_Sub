# カスタムMCPツール作成ガイド

## プロジェクト情報
- **ツールキットリポジトリ**: `https://github.com/nakagimadevelop002-glitch/TozawaMCP-Toolkit.git`
- **サブモジュール配置先**: `Assets/TozawaMCP/`
- **ツール配置先**: `Assets/TozawaMCP/Editor/Tools/`
- **アセンブリ**: `TozawaMcpTools.Editor`
- **ベースフレームワーク**: UnityNaturalMCP (フォーク版 `nakagimadevelop002-glitch/UnityNaturalMCP`)

## Git管理
- **GitHubアカウント**: `nakagimadevelop002-glitch`
- **認証方式**: Classic Personal Access Token（リモートURLに埋め込み済み）

## アーキテクチャ

### 自動登録の仕組み
`CustomMcpToolBuilder.cs` が同一アセンブリ内の `[McpServerToolType]` 属性付きクラスを自動検出し、MCPサーバーに登録する。
- **新しいツールを追加する際に `CustomMcpToolBuilder.cs` の変更は不要**
- `CustomMcpToolBuilder.asset`（ScriptableObject）がUnityNaturalMCPによって読み込まれ、ビルダーが実行される

### 別アセンブリにツールを作る場合
プロジェクト固有のツール（サブモジュール外）を作る場合は、そのアセンブリ用に別途 `McpBuilderScriptableObject` を継承したビルダーと対応する `.asset` が必要になる。

## 新しいMCPツールの作成手順

### サブモジュール内にツールを追加する場合
`Assets/TozawaMCP/Editor/Tools/` に新しい `.cs` ファイルを作成し、以下の規約に従う：

```csharp
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEngine;

[McpServerToolType, Description("ツールクラスの説明")]
internal sealed class MyNewTool
{
    [McpServerTool, Description("メソッドの説明（MCPクライアントに表示される）")]
    public async ValueTask<string> MyToolMethod(
        [Description("パラメータの説明")]
        string param1 = "default")
    {
        try
        {
            await UniTask.SwitchToMainThread();
            // Unity APIの呼び出し（メインスレッド上で実行される）
            return "結果メッセージ";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
```

### 2. コンパイル
スクリプト作成・変更後、Unityエディタをクリックしてコンパイルを実行する。

### 3. MCPサーバー再起動
**自動方式（推奨）**: `RefreshMCPTools` ツールを使用
```bash
curl -X POST http://localhost:56780/mcp/ -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"RefreshMCPTools","arguments":{}},"id":1}'
```

**手動方式**: UnityのProject Settings > UnityNaturalMCP からサーバーを再起動し、ツールが登録されたことをログで確認する。

## 必須規約

### クラスレベル
- `[McpServerToolType]` 属性を必ず付与
- `[Description("...")]` でクラスの概要を記述
- `internal sealed class` を推奨

### メソッドレベル
- `[McpServerTool]` 属性を必ず付与
- `[Description("...")]` でメソッドの説明を記述（MCPクライアントがツール一覧に表示する）
- 戻り値は `ValueTask<string>`
- パラメータには `[Description("...")]` を付与

### メインスレッド切り替え
- Unity APIを使用する場合は `await UniTask.SwitchToMainThread()` でメインスレッドに切り替える
- `try-catch` で例外をハンドリングし、`Debug.LogError` でログ出力後に `throw` する

### Undo対応
- シーンやアセットを変更する操作には `Undo.RegisterCreatedObjectUndo` 等でUndo登録を行う

## 依存関係（asmdef）
`TozawaMcpTools.Editor.asmdef` の設定：
- **references**: `UnityNaturalMCP.Editor`, `UniTask`
- **precompiledReferences**: `ModelContextProtocol.dll`, `System.ComponentModel.Annotations.dll`
- **includePlatforms**: `Editor`

新しいUnity APIを使う場合（例: `UnityEditor` 名前空間）は、using追加のみで対応可能。asmdefの変更は基本的に不要。

## 自動セットアップ（McpPackageInstaller）
`Editor/Setup/McpPackageInstaller.cs` がUnityエディタ起動時に `manifest.json` を自動チェックし、UniTask と フォーク版NMCP のパッケージ参照がなければ自動追加する。

## NMCPフォーク変更履歴

| 対象 | 変更内容 | 理由 |
|---|---|---|
| `McpUnityEditorTool.RefreshAssets` | フォーク版で削除済み | 機能不全のため、カスタム版 `RefreshAssetsTool` に置き換え |
| `UnityNaturalMCP.Editor.asmdef` | `ModelContextProtocol.Core.dll` 参照削除 | 実体DLLが存在しないため |
