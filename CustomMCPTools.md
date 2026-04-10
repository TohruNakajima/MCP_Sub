# カスタムMCPツール一覧

**サブモジュール**: https://github.com/TohruNakajima/MCP_Sub.git

## 重要ルール
- ❌ **手動作業の提案・依頼は絶対禁止（全てMCPツールで完結させる）**
- ❌ **TextMeshPro使用禁止（ユーザー明示指示がない限り旧UI（InputField/Text/Button）使用厳守）**
- ❌ **バッチビルド実行（-batchmode -quit）絶対禁止（Unity Editor GUI上で手動ビルドのみ許可）**
- ❌ **APIキー・機密情報のGitコミット絶対禁止（.gitignore必須確認）**
- ❌ **サブモジュール外でのMCPツール直接編集禁止**

## Inspector操作 (InspectorTool)
- Ins_GetSceneHierarchy: シーン階層ツリー取得
- Ins_GetGameObjectInfo: GameObject詳細情報取得
- Ins_GetMeshBounds: メッシュバウンド情報取得
- Ins_GetComponentProperties: コンポーネントのシリアライズプロパティ取得
- Ins_SetActive: GameObjectのアクティブ状態設定
- Ins_Rename: GameObjectリネーム
- Ins_SetPropertyValue: シリアライズプロパティ値設定
- Ins_AddComponent: コンポーネント追加
- Ins_RemoveComponent: コンポーネント削除
- Ins_SetComponentEnabled: コンポーネント有効/無効切り替え
- Ins_SetArraySize: 配列プロパティサイズ設定
- Ins_InsertArrayElement: 配列要素挿入
- Ins_DeleteArrayElement: 配列要素削除
- Ins_SetTag: タグ設定
- Ins_SetLayer: レイヤー設定
- Ins_SetStaticFlags: スタティックフラグ設定
- Ins_DeleteGameObject: GameObject削除
- Ins_InstantiatePrefab: プレハブインスタンス化
- Ins_AddUnityEventListener: UnityEventリスナー追加
- Ins_InvokeAssetMethod: ScriptableObjectメソッド呼び出し
- Ins_InvokeAssetMethodWithStringArg: 文字列引数付きメソッド呼び出し
- Ins_ImportJSONFromFile: JSONファイルからインポート
- Ins_SetMaterialProperty: マテリアルプロパティ設定
- Ins_SetPropertyValueBulk: 名前パターンマッチ一括プロパティ設定

## Hierarchy操作 (HierarchyTool)
- Hier_ListAllGameObjects: 全GameObject列挙（InstanceID付き）
- Hier_DeleteAllUnnamedGameObjects: 空名前GameObject一括削除
- Hier_DeleteByInstanceID: InstanceID指定削除
- Hier_SetParent: 親子関係設定
- Hier_SetSiblingIndex: 兄弟順序変更

## Project操作 (ProjectTool)
- Proj_LoadScene: シーンロード
- Proj_SelectAsset: アセット選択
- Proj_SaveScene: シーン保存
- Proj_SaveSceneAs: 名前を付けてシーン保存
- Proj_CreatePrefabVariant: プレハブバリアント作成
- Proj_RenameAsset: アセットリネーム

## GameObject作成 (CreateEmptyGameObjectTool)
- CreateGameObject: 空GameObject作成
- SetParent: 親子関係設定

## GameObject削除 (GameObjectDeleteTool)
- DeleteGameObjectByInstanceID: InstanceID指定削除

## メニュー操作 (MenuItemInvokerTool)
- ExecuteMenuItem: メニューアイテム実行

## Animation操作 (AnimationTool)
- Anim_CreateAnimatorController: AnimatorController作成
- Anim_CreateAnimationClip: AnimationClip作成
- Anim_AddParameter: パラメータ追加（Int/Float/Bool/Trigger）
- Anim_AddState: ステート追加（Motion設定含む）
- Anim_AddTransition: トランジション作成（Condition設定含む）
- Anim_SetCurve: キーフレーム設定
- Anim_AddEvent: AnimationEvent追加

## UI要素作成 (UIElementCreationTool)
- SetButtonTargetGraphic: ButtonのTargetGraphic設定
- AddButtonText: Button子要素にText追加
- CreateLegacyButton: レガシーButton作成
- UpdateButtonText: ButtonのText更新
- CreateSlider: Slider作成（完全構造）

## マテリアル操作 (MaterialConverterTool)
- Material_SetShader: マテリアルのシェーダー変更（テクスチャ保持、Undo対応）
- Material_UpgradeToURP: マテリアルをURP Litにアップグレード
- Material_FindProblems: 壊れたマテリアルをスキャン
- Material_ListShaders: 使用シェーダー名一覧

## Terrain操作 (TerrainTool)
- Terrain_GetInfo: Terrain情報取得
- Terrain_SetPerlinHeight: Perlinノイズ地形生成
- Terrain_CarveRiver: 川床彫刻
- Terrain_Smooth: 地形スムース
- Terrain_FlattenArea: 平坦化

## Terrainテクスチャ操作 (TerrainTextureTool)
- Terrain_AutoPaint: 自動テクスチャ塗り分け
- Terrain_AddGrass: 草自動生成

## Prefab操作
- Prefab_Instantiate (PrefabInstantiationTool): プレハブをシーンに配置
- CreateTreePrefab (PrefabCreationTool): FBXから年齢別ツリープレハブ作成
- CreateSingleMeshTreePrefab (SingleTreePrefabTool): 単一メッシュツリープレハブ作成

## Asset Store操作 (AssetStoreTool)
- DownloadAndImportPackage: パッケージのダウンロード＆インポート
- SearchCachedPackages: キャッシュ内パッケージ検索
- ListCachedPackages: キャッシュ内パッケージ一覧
- ImportUnityPackage: .unitypackageインポート
- OpenPackageManager: Package Manager起動
- OpenAssetStoreURL: Asset Store URLをブラウザで開く
- DebugInspectPackageManager: Package Manager内部調査

## PlayerSettings操作 (PlayerSettingsTool)
- GetWebGLCompression: WebGL圧縮形式取得
- SetWebGLCompression: WebGL圧縮形式設定
- GetDecompressionFallback: 解凍フォールバック取得
- SetDecompressionFallback: 解凍フォールバック設定

## MCP設定 (MCPSettingsTool)
- GetMCPSettings: MCP設定取得
- SetMCPPort: MCPポート設定
- SetMCPIPAddress: MCP IPアドレス設定

## ログ (LogToUnityConsoleTool)
- LogMessage: Unityコンソールにログ出力
- LogWarning: 警告出力
- LogError: エラー出力

## Forest操作
- DeleteByNamePattern (ForestCleanupTool): 名前パターンでGO一括削除
- AssignTreeIndices (ForestIndexAssignmentTool): ツリーインデックス一括割当
- PlantTrees (ForestPlantingTool): フィボナッチスパイラルで植林

## その他
- RefreshAssets (RefreshAssetsTool): アセット更新
- RefreshMCPTools (RefreshMCPToolsTool): MCPツール再登録＋サーバー再起動
- TVE_ConvertPrefab (TVEAssetConverterTool): TheVisualEngineでプレハブ変換

新規ツール作成時は必ずこの一覧に追記すること
