# Unity MCP作業ルール

## MCPツール呼び出し
ポート番号はプロジェクトごとに異なる。各プロジェクトの設定を参照すること。

## 主要ツール
- GetCurrentConsoleLogs, RefreshAssets
- Ins_* (GameObject/Component操作)
- Proj_* (シーン/アセット操作)
- ExecuteMenuItem (メニュー実行)

## MCPツール作成・更新
原則提案制。既存確認→ユーザー許可→作成→コンパイル確認→RefreshAssets→登録確認
サブモジュールリポジトリ: https://github.com/TohruNakajima/MCP_Sub.git

## 注意事項
- プロジェクト固有の情報（パス、シーン名、ポート番号、使用アセット等）はこのファイルに記述しない
- Proj_SaveScene()前にRefreshAssets禁止（アセット参照消失の原因）
- FindObjectOfType非推奨（FindFirstObjectByType使用）
