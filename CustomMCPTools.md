# カスタムMCPツール一覧

**ポート**: shrine_adventure/川のやつ/WoodSimulator=56780

## サブモジュール情報
- **リポジトリ**: https://github.com/TohruNakajima/MCP_Sub.git
- **重要**: ツール更新時は必ずサブモジュールに反映（詳細はUnityRules.md参照）

## 重要ルール
- ❌ **手動作業の提案・依頼は絶対禁止（全てMCPツールで完結させる）**
- ❌ **TextMeshPro使用禁止（ユーザー明示指示がない限り旧UI（InputField/Text/Button）使用厳守）**
- ❌ **バッチビルド実行（-batchmode -quit）絶対禁止（Unity Editor GUI上で手動ビルドのみ許可）**
- ❌ **APIキー・機密情報のGitコミット絶対禁止（.gitignore必須確認）**
- ❌ **サブモジュール外でのMCPツール直接編集禁止**

## Inspector操作
- Ins_InvokeAssetMethod: アセットメソッド呼び出し
- Ins_SetPropertyValue: プロパティ設定
- Ins_GetGameObjectInfo: GameObject情報取得
- Ins_GetComponentProperties: Component情報取得
- Ins_AddUnityEventListener: イベント登録

## Project操作
- Proj_LoadScene: シーンロード
- Proj_SelectAsset: アセット選択
- Proj_SaveScene: シーン保存
- Proj_CreatePrefabVariant: バリアント作成

## メニュー操作
- ExecuteMenuItem: メニュー実行
- CreateUtageProject: 宴プロジェクト作成

## GameObject/Component
- AttachScriptToObject: Component追加
- RemoveScriptFromObject: Component削除
- SetComponentField: フィールド設定
- ListComponentFields: フィールド一覧

## Animation操作
- Anim_CreateAnimatorController: AnimatorController作成
- Anim_CreateAnimationClip: AnimationClip作成
- Anim_AddParameter: Parameter追加（Int/Float/Bool/Trigger）
- Anim_AddState: State追加（Motion設定含む）
- Anim_AddTransition: Transition作成（Condition設定含む）
- Anim_SetCurve: AnimationClipにキーフレーム追加
- Anim_AddEvent: AnimationEvent追加

## その他
- RefreshAssets: アセット更新
- GetCurrentConsoleLogs: ログ取得
- CaptureGameView: スクショ
- CreateGameObject: GameObject作成（Empty/Cube/Sphere等）

新規作成時は必ず追記
