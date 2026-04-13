# TozawaMCP サブモジュール設定

MCPツールキットのサブモジュール。プロジェクト共通のUnity MCPツール群を管理する。

## サブモジュール情報
- リポジトリ: https://github.com/TohruNakajima/MCP_Sub.git
- 配置先: Assets/TozawaMCP/

## 注意事項
- このサブモジュールは複数プロジェクトで共有される
- プロジェクト固有の情報（パス、シーン名、ポート番号等）をこのサブモジュール内に記述しないこと
- プロジェクト固有のルールは各プロジェクトの開発ログやグローバルCLAUDE.mdに記述すること

## MCPツール更新手順
```bash
cd Assets/TozawaMCP
git add . && git commit -m "説明" && git push origin main
```
その後、プロジェクトルートでサブモジュール参照更新commit
