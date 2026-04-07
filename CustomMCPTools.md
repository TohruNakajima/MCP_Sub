# カスタムMCPツール一覧

**サブモジュール**: https://github.com/TohruNakajima/MCP_Sub.git

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

## Hierarchy操作
- Hier_ListAllGameObjects: 全GameObject列挙（InstanceID、階層構造表示）
- Hier_DeleteAllUnnamedGameObjects: 空名前GameObject一括削除（Undo対応）
- Hier_DeleteByInstanceID: InstanceID指定削除（Undo対応）
- Hier_SetParent: 親子関係設定（ドラッグ&ドロップ相当）
- Hier_SetSiblingIndex: GameObject順序変更（同一親配下での並び替え、Undo対応）

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

## UI要素作成
- CreateSlider: Slider作成（Background/Fill/Handle完全構造）
- CreateLegacyButton: Button作成（Image+Text付き）
- SetButtonTargetGraphic: ButtonのTargetGraphic設定
- AddButtonText: Button子要素にText追加
- UpdateButtonText: ButtonのText更新

## Prefab操作
- Prefab_Instantiate: Edit ModeでPrefabをシーンにインスタンス化

## Terrain操作
- Terrain_GetInfo: Terrain情報取得
- Terrain_SetPerlinHeight: Perlinノイズ地形生成
- Terrain_CarveRiver: 川床彫刻
- Terrain_Smooth: 地形スムース
- Terrain_FlattenArea: 平坦化
- Terrain_AutoPaint: 自動テクスチャ塗り分け
- Terrain_AddGrass: 草自動生成

## その他
- RefreshAssets: アセット更新
- GetCurrentConsoleLogs: ログ取得
- CaptureGameView: スクショ
- CreateGameObject: GameObject作成（Empty/Cube/Sphere等）

新規作成時は必ず追記
