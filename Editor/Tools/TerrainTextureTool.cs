using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

/// <summary>
/// Terrainテクスチャ・草・ディテール操作用MCPツール。
/// プロシージャルカラーテクスチャの生成と高さ/傾斜ベースの自動塗り分け。
/// </summary>
[McpServerToolType, Description("Terrain texture painting, grass, and detail tools for natural landscape coloring.")]
internal sealed class TerrainTextureTool
{
    [McpServerTool, Description("Auto-paint terrain with procedural color layers based on height and slope. Creates grass (low flat), forest floor (mid), rock (steep), and dirt (river bed) layers. No external textures needed.")]
    public async ValueTask<string> Terrain_AutoPaint(
        [Description("Slope threshold for rock in degrees (default 30)")] float rockSlopeThreshold = 30f,
        [Description("Height ratio below which river bed dirt is applied (0-1, default 0.18)")] float riverBedHeight = 0.18f)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return "Error: No active Terrain found.";

            var td = terrain.terrainData;

            // プロシージャルカラーテクスチャを生成してTerrainLayerに設定
            var grassLayer = CreateColorTerrainLayer("Grass", new Color(0.25f, 0.45f, 0.15f), "Assets/Terrain");
            var forestLayer = CreateColorTerrainLayer("ForestFloor", new Color(0.3f, 0.35f, 0.18f), "Assets/Terrain");
            var rockLayer = CreateColorTerrainLayer("Rock", new Color(0.45f, 0.42f, 0.38f), "Assets/Terrain");
            var dirtLayer = CreateColorTerrainLayer("Dirt", new Color(0.35f, 0.28f, 0.18f), "Assets/Terrain");

            td.terrainLayers = new TerrainLayer[] { grassLayer, forestLayer, rockLayer, dirtLayer };

            // Splatmap生成（高さ・傾斜ベース）
            int alphaRes = td.alphamapResolution;
            float[,,] splatmap = new float[alphaRes, alphaRes, 4];

            float[,] heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
            float heightmapScale = (float)(td.heightmapResolution - 1) / (alphaRes - 1);

            for (int z = 0; z < alphaRes; z++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    // 高さ取得
                    int hx = Mathf.RoundToInt(x * heightmapScale);
                    int hz = Mathf.RoundToInt(z * heightmapScale);
                    hx = Mathf.Clamp(hx, 0, td.heightmapResolution - 1);
                    hz = Mathf.Clamp(hz, 0, td.heightmapResolution - 1);
                    float h = heights[hz, hx];

                    // 傾斜取得
                    float slope = td.GetSteepness((float)x / alphaRes, (float)z / alphaRes);

                    // 塗り分けロジック
                    float grass = 0f, forest = 0f, rock = 0f, dirt = 0f;

                    if (h < riverBedHeight)
                    {
                        // 川床: 土
                        dirt = 1f;
                    }
                    else if (slope > rockSlopeThreshold)
                    {
                        // 急斜面: 岩
                        rock = 1f;
                    }
                    else if (slope > rockSlopeThreshold * 0.6f)
                    {
                        // 中傾斜: 岩と森林のブレンド
                        float t = (slope - rockSlopeThreshold * 0.6f) / (rockSlopeThreshold * 0.4f);
                        rock = t;
                        forest = 1f - t;
                    }
                    else
                    {
                        // 緩斜面: 高さに応じて草と森林をブレンド
                        float hNorm = Mathf.InverseLerp(riverBedHeight, 0.5f, h);
                        grass = 1f - hNorm * 0.6f;
                        forest = hNorm * 0.6f;
                    }

                    // 正規化
                    float total = grass + forest + rock + dirt;
                    if (total > 0f)
                    {
                        splatmap[z, x, 0] = grass / total;
                        splatmap[z, x, 1] = forest / total;
                        splatmap[z, x, 2] = rock / total;
                        splatmap[z, x, 3] = dirt / total;
                    }
                    else
                    {
                        splatmap[z, x, 0] = 1f;
                    }
                }
            }

            Undo.RegisterCompleteObjectUndo(td, "Terrain AutoPaint");
            td.SetAlphamaps(0, 0, splatmap);

            return $"Terrain auto-painted: 4 layers (Grass/ForestFloor/Rock/Dirt), rockSlope={rockSlopeThreshold}°, riverBed={riverBedHeight}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Add procedural grass detail to the terrain. Grass density is based on slope and height (flat grassy areas get more grass).")]
    public async ValueTask<string> Terrain_AddGrass(
        [Description("Max grass density per patch (1-16, default 8)")] int density = 8,
        [Description("Grass color (hex, default '#3A6B1A')")] string color = "#3A6B1A",
        [Description("Dry grass color (hex, default '#8B7D3C')")] string dryColor = "#8B7D3C",
        [Description("Min grass height (default 0.5)")] float minHeight = 0.5f,
        [Description("Max grass height (default 1.5)")] float maxHeight = 1.5f,
        [Description("Max slope for grass in degrees (default 25)")] float maxSlope = 25f)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return "Error: No active Terrain found.";

            var td = terrain.terrainData;

            // 草のDetailPrototype作成
            ColorUtility.TryParseHtmlString(color, out Color grassColor);
            ColorUtility.TryParseHtmlString(dryColor, out Color grassDryColor);

            // 草テクスチャ: 既存があれば再利用、なければ生成
            string grassTexPath = "Assets/Terrain/GrassBillboard.png";
            var grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(grassTexPath);
            if (grassTex == null)
            {
                var genTex = GenerateGrassBillboardTexture(grassColor);
                string fullPath = System.IO.Path.GetFullPath(grassTexPath);
                System.IO.File.WriteAllBytes(fullPath, genTex.EncodeToPNG());
                AssetDatabase.ImportAsset(grassTexPath, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(grassTexPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
                grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(grassTexPath);
            }

            if (grassTex == null)
                return "Error: Failed to create/load grass texture.";

            // プロトタイプが未設定の場合のみ初期化
            bool needsInit = td.detailPrototypes.Length == 0
                          || td.detailPrototypes[0].prototypeTexture == null;

            if (needsInit)
            {
                td.SetDetailResolution(256, 16);

                var grassProto = new DetailPrototype();
                grassProto.renderMode = DetailRenderMode.Grass;
                grassProto.prototypeTexture = grassTex;
                grassProto.healthyColor = grassColor;
                grassProto.dryColor = grassDryColor;
                grassProto.minHeight = minHeight;
                grassProto.maxHeight = maxHeight;
                grassProto.minWidth = 0.3f;
                grassProto.maxWidth = 0.8f;
                grassProto.noiseSpread = 0.3f;
                grassProto.useInstancing = false;
                grassProto.usePrototypeMesh = false;

                td.detailPrototypes = new DetailPrototype[] { grassProto };
                td.wavingGrassStrength = 0.5f;
                td.wavingGrassAmount = 0.3f;
                td.wavingGrassSpeed = 0.5f;
                td.wavingGrassTint = grassColor;

                td.RefreshPrototypes();

                // プロトタイプ設定後に一度保存してから密度マップを書く
                EditorUtility.SetDirty(td);
                AssetDatabase.SaveAssets();

                Debug.Log("[Terrain_AddGrass] Initialized DetailPrototype");
            }

            // 密度マップ生成
            int detailRes = td.detailResolution;
            int[,] grassMap = new int[detailRes, detailRes];

            float[,] heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);

            int filledCount = 0;
            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    float normX = (float)x / detailRes;
                    float normZ = (float)z / detailRes;

                    float slope = td.GetSteepness(normX, normZ);
                    int hx = Mathf.RoundToInt(normX * (td.heightmapResolution - 1));
                    int hz = Mathf.RoundToInt(normZ * (td.heightmapResolution - 1));
                    hx = Mathf.Clamp(hx, 0, td.heightmapResolution - 1);
                    hz = Mathf.Clamp(hz, 0, td.heightmapResolution - 1);
                    float h = heights[hz, hx];

                    if (slope < maxSlope && h > 0.15f)
                    {
                        float slopeFactor = 1f - (slope / maxSlope);
                        float noise = Mathf.PerlinNoise(normX * 10f, normZ * 10f);
                        grassMap[z, x] = Mathf.Clamp(Mathf.RoundToInt(density * slopeFactor * noise), 0, 16);
                        if (grassMap[z, x] > 0) filledCount++;
                    }
                }
            }

            Debug.Log($"[Terrain_AddGrass] DetailLayer filled: {filledCount}/{detailRes * detailRes}");

            td.SetDetailLayer(0, 0, 0, grassMap);

            // 草の描画距離設定
            terrain.detailObjectDistance = 120f;
            terrain.detailObjectDensity = 1f;

            // TerrainData更新を確定
            EditorUtility.SetDirty(td);
            terrain.Flush();

            // 確認ログ
            var protos = td.detailPrototypes;
            Debug.Log($"[Terrain_AddGrass] Prototypes={protos.Length}, " +
                      $"Tex={protos[0].prototypeTexture}, Mode={protos[0].renderMode}");

            return $"Grass added: density={density}, height={minHeight}-{maxHeight}m, maxSlope={maxSlope}°, " +
                   $"texture={grassTex.name}, resolution={td.detailResolution}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    /// <summary>草ビルボード用テクスチャをプロシージャル生成</summary>
    private static Texture2D GenerateGrassBillboardTexture(Color baseColor)
    {
        int w = 64, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        DrawGrassBlade(pixels, w, h, w / 2, 0, h, 3, baseColor, 0f);
        DrawGrassBlade(pixels, w, h, w / 3, 0, (int)(h * 0.85f), 2, baseColor * 0.85f, -0.15f);
        DrawGrassBlade(pixels, w, h, w * 2 / 3, 0, (int)(h * 0.9f), 2, baseColor * 0.9f, 0.1f);

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static void DrawGrassBlade(Color[] pixels, int texW, int texH,
        int centerX, int bottomY, int topY, int halfWidth, Color color, float lean)
    {
        for (int y = bottomY; y < topY; y++)
        {
            float t = (float)(y - bottomY) / (topY - bottomY);
            int w = Mathf.Max(1, Mathf.RoundToInt(halfWidth * (1f - t * 0.8f)));
            int cx = centerX + Mathf.RoundToInt(lean * (y - bottomY));
            Color c = Color.Lerp(color * 0.7f, color, t);
            c.a = 1f;

            for (int dx = -w; dx <= w; dx++)
            {
                int px = cx + dx;
                if (px >= 0 && px < texW && y >= 0 && y < texH)
                    pixels[y * texW + px] = c;
            }
        }
    }

    /// <summary>
    /// 単色のプロシージャルテクスチャからTerrainLayerを生成する。
    /// </summary>
    private static TerrainLayer CreateColorTerrainLayer(string name, Color color, string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            string[] parts = folder.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string check = string.Join("/", parts, 0, i + 1);
                if (!AssetDatabase.IsValidFolder(check))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = check;
            }
        }

        string texPath = $"{folder}/Tex_{name}.asset";
        string layerPath = $"{folder}/Layer_{name}.asset";

        // 既存アセットがあれば再利用
        var existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existingLayer != null)
        {
            // テクスチャの色を更新
            if (existingLayer.diffuseTexture != null)
            {
                var tex = existingLayer.diffuseTexture;
                var pixels = new Color[tex.width * tex.height];
                // 微妙なバリエーションを加える
                for (int i = 0; i < pixels.Length; i++)
                {
                    float noise = UnityEngine.Random.Range(-0.03f, 0.03f);
                    pixels[i] = new Color(
                        Mathf.Clamp01(color.r + noise),
                        Mathf.Clamp01(color.g + noise),
                        Mathf.Clamp01(color.b + noise), 1f);
                }
                tex.SetPixels(pixels);
                tex.Apply();
                EditorUtility.SetDirty(tex);
            }
            existingLayer.smoothness = 0f;
            existingLayer.metallic = 0f;
            EditorUtility.SetDirty(existingLayer);
            return existingLayer;
        }

        // プロシージャルテクスチャ生成（16x16、微妙なノイズ付き）
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        texture.name = $"Tex_{name}";
        texture.wrapMode = TextureWrapMode.Repeat;
        var pixelArray = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            float noise = UnityEngine.Random.Range(-0.03f, 0.03f);
            pixelArray[i] = new Color(
                Mathf.Clamp01(color.r + noise),
                Mathf.Clamp01(color.g + noise),
                Mathf.Clamp01(color.b + noise), 1f);
        }
        texture.SetPixels(pixelArray);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, texPath);

        // TerrainLayer作成
        var layer = new TerrainLayer();
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(5f, 5f);
        layer.tileOffset = Vector2.zero;
        layer.smoothness = 0f;
        layer.metallic = 0f;
        AssetDatabase.CreateAsset(layer, layerPath);

        AssetDatabase.SaveAssets();
        return layer;
    }

    // ==================== Detail Paint Tools ====================

    [McpServerTool, Description("List all detail prototypes registered on the active terrain. Shows index, texture name, renderMode, size, and color.")]
    public async ValueTask<string> Terrain_ListDetails()
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return "Error: No active Terrain found.";
            var td = terrain.terrainData;
            var protos = td.detailPrototypes;
            if (protos.Length == 0) return "No detail prototypes registered.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Detail Prototypes: {protos.Length}, Resolution: {td.detailResolution}");
            for (int i = 0; i < protos.Length; i++)
            {
                var p = protos[i];
                int[,] layer = td.GetDetailLayer(0, 0, td.detailResolution, td.detailResolution, i);
                int nonZero = 0, maxVal = 0;
                for (int z = 0; z < td.detailResolution; z++)
                    for (int x = 0; x < td.detailResolution; x++)
                    {
                        if (layer[z, x] > 0) nonZero++;
                        if (layer[z, x] > maxVal) maxVal = layer[z, x];
                    }
                sb.AppendLine($"  [{i}] tex={p.prototypeTexture?.name ?? "null"}, mode={p.renderMode}, H={p.minHeight}-{p.maxHeight}, W={p.minWidth}-{p.maxWidth}, healthy={p.healthyColor}, dry={p.dryColor}, filled={nonZero}/{td.detailResolution * td.detailResolution}, maxDensity={maxVal}");
            }
            return sb.ToString();
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }

    [McpServerTool, Description("Paint detail (grass/flower) at a world position with a circular brush. Like the Paint Detail tool in Unity Editor.")]
    public async ValueTask<string> Terrain_PaintDetail(
        [Description("Detail prototype index (use Terrain_ListDetails to see available indices)")]
        int detailIndex,
        [Description("World X position to paint at")]
        float worldX,
        [Description("World Z position to paint at")]
        float worldZ,
        [Description("Brush radius in meters (default 10)")]
        float radius = 10f,
        [Description("Density value (1-16, default 8)")]
        int density = 8,
        [Description("Brush falloff: 1.0=hard edge, 0.0=full falloff (default 0.5)")]
        float hardness = 0.5f)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return "Error: No active Terrain found.";
            var td = terrain.terrainData;
            var protos = td.detailPrototypes;
            if (detailIndex < 0 || detailIndex >= protos.Length)
                return $"Error: detailIndex {detailIndex} out of range (0-{protos.Length - 1}).";

            int detailRes = td.detailResolution;
            var tPos = terrain.transform.position;

            // ワールド座標→Detail座標
            float normX = (worldX - tPos.x) / td.size.x;
            float normZ = (worldZ - tPos.z) / td.size.z;
            int centerX = Mathf.RoundToInt(normX * detailRes);
            int centerZ = Mathf.RoundToInt(normZ * detailRes);
            float radiusInDetail = (radius / td.size.x) * detailRes;

            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radiusInDetail));
            int maxX = Mathf.Min(detailRes - 1, Mathf.CeilToInt(centerX + radiusInDetail));
            int minZ = Mathf.Max(0, Mathf.FloorToInt(centerZ - radiusInDetail));
            int maxZ = Mathf.Min(detailRes - 1, Mathf.CeilToInt(centerZ + radiusInDetail));

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;
            if (width <= 0 || height <= 0)
                return "Error: Paint area is outside terrain bounds.";

            int[,] map = td.GetDetailLayer(minX, minZ, width, height, detailIndex);
            int painted = 0;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (minX + x) - centerX;
                    float dz = (minZ + z) - centerZ;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);

                    if (dist <= radiusInDetail)
                    {
                        float t = dist / radiusInDetail;
                        float falloff = Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - hardness) / (1f - hardness + 0.001f)));
                        int val = Mathf.RoundToInt(density * falloff);
                        if (val > map[z, x])
                        {
                            map[z, x] = Mathf.Clamp(val, 0, 16);
                            painted++;
                        }
                    }
                }
            }

            td.SetDetailLayer(minX, minZ, detailIndex, map);
            ForceRefreshTerrainDetail(terrain, td);

            return $"Painted detail[{detailIndex}] ({protos[detailIndex].prototypeTexture?.name}) at ({worldX},{worldZ}), radius={radius}m, density={density}, painted={painted} cells.";
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }

    [McpServerTool, Description("Fill entire terrain with a detail layer using Perlin noise pattern. Efficient way to cover the whole terrain with grass/flowers.")]
    public async ValueTask<string> Terrain_FillDetail(
        [Description("Detail prototype index (use Terrain_ListDetails to see available indices)")]
        int detailIndex,
        [Description("Max density (1-16, default 8)")]
        int density = 8,
        [Description("Noise frequency: higher=more varied (default 8)")]
        float noiseFreq = 8f,
        [Description("Density threshold: lower=more coverage (0-1, default 0.2)")]
        float threshold = 0.2f,
        [Description("Random seed offset for unique patterns per layer (default 0)")]
        float seedOffset = 0f,
        [Description("If true, boost density near specified world positions (cowBoostPositions format: 'x1,z1;x2,z2;...')")]
        string boostPositions = "")
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return "Error: No active Terrain found.";
            var td = terrain.terrainData;
            var protos = td.detailPrototypes;
            if (detailIndex < 0 || detailIndex >= protos.Length)
                return $"Error: detailIndex {detailIndex} out of range (0-{protos.Length - 1}).";

            var tPos = terrain.transform.position;
            int detailRes = td.detailResolution;

            // ブースト位置パース
            var boostNorms = new System.Collections.Generic.List<Vector2>();
            if (!string.IsNullOrEmpty(boostPositions))
            {
                foreach (var pair in boostPositions.Split(';'))
                {
                    var parts = pair.Trim().Split(',');
                    if (parts.Length == 2 && float.TryParse(parts[0], out float bx) && float.TryParse(parts[1], out float bz))
                    {
                        boostNorms.Add(new Vector2(
                            (bx - tPos.x) / td.size.x,
                            (bz - tPos.z) / td.size.z));
                    }
                }
            }

            int[,] map = new int[detailRes, detailRes];
            int filled = 0;

            for (int z = 0; z < detailRes; z++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    float nx = (float)x / detailRes;
                    float nz = (float)z / detailRes;

                    float n1 = Mathf.PerlinNoise(nx * noiseFreq + seedOffset, nz * noiseFreq + seedOffset);
                    float n2 = Mathf.PerlinNoise(nx * noiseFreq * 2f + seedOffset + 100f, nz * noiseFreq * 2f + seedOffset + 100f);
                    float combined = n1 * 0.6f + n2 * 0.4f;

                    // ブースト計算
                    float boost = 0f;
                    foreach (var bp in boostNorms)
                    {
                        float d = Vector2.Distance(new Vector2(nx, nz), bp);
                        if (d < 0.25f) boost = Mathf.Max(boost, Mathf.SmoothStep(1f, 0f, d / 0.25f));
                    }

                    int maxD = Mathf.RoundToInt(Mathf.Lerp(density, Mathf.Min(16, density * 2), boost));

                    if (combined > threshold)
                    {
                        float factor = (combined - threshold) / (1f - threshold);
                        int val = Mathf.RoundToInt(maxD * factor);
                        if (boost > 0.5f && val < 3) val = 3;
                        map[z, x] = Mathf.Clamp(val, 0, 16);
                        if (map[z, x] > 0) filled++;
                    }
                    else if (boost > 0.3f)
                    {
                        map[z, x] = Mathf.RoundToInt(2 * boost);
                        if (map[z, x] > 0) filled++;
                    }
                }
            }

            td.SetDetailLayer(0, 0, detailIndex, map);
            ForceRefreshTerrainDetail(terrain, td);

            return $"Filled detail[{detailIndex}] ({protos[detailIndex].prototypeTexture?.name}): {filled}/{detailRes * detailRes} cells, density={density}, freq={noiseFreq}, threshold={threshold}.";
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }

    [McpServerTool, Description("Clear a detail layer (set all density to 0) on the active terrain.")]
    public async ValueTask<string> Terrain_ClearDetail(
        [Description("Detail prototype index to clear (-1 to clear all layers)")]
        int detailIndex = -1)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return "Error: No active Terrain found.";
            var td = terrain.terrainData;
            int detailRes = td.detailResolution;
            int[,] emptyMap = new int[detailRes, detailRes];

            int cleared = 0;
            if (detailIndex == -1)
            {
                for (int i = 0; i < td.detailPrototypes.Length; i++)
                {
                    td.SetDetailLayer(0, 0, i, emptyMap);
                    cleared++;
                }
            }
            else
            {
                if (detailIndex < 0 || detailIndex >= td.detailPrototypes.Length)
                    return $"Error: detailIndex {detailIndex} out of range.";
                td.SetDetailLayer(0, 0, detailIndex, emptyMap);
                cleared = 1;
            }

            ForceRefreshTerrainDetail(terrain, td);
            return $"Cleared {cleared} detail layer(s).";
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }

    [McpServerTool, Description("Save terrain data to disk with ForceReserialize to ensure all changes persist across domain reloads.")]
    public async ValueTask<string> Terrain_SaveData()
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return "Error: No active Terrain found.";
            var td = terrain.terrainData;
            string tdPath = AssetDatabase.GetAssetPath(td);

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssetIfDirty(td);
            AssetDatabase.ForceReserializeAssets(new[] { tdPath }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);

            return $"Terrain data saved and force-reserialized: {tdPath}";
        }
        catch (Exception e) { Debug.LogError(e); throw; }
    }

    /// <summary>
    /// SetDetailLayer後にTerrainのレンダリングキャッシュを強制更新する。
    /// EditorのPaint Detailツールと同等の更新処理を行う。
    /// </summary>
    private static void ForceRefreshTerrainDetail(Terrain terrain, TerrainData td)
    {
        EditorUtility.SetDirty(td);
        td.RefreshPrototypes();

        // TerrainDataを一時的に外して再設定 → 全内部キャッシュを再構築
        var savedTd = terrain.terrainData;
        terrain.terrainData = null;
        terrain.terrainData = savedTd;

        // Foliage描画をリセット
        terrain.drawTreesAndFoliage = false;
        terrain.drawTreesAndFoliage = true;

        terrain.Flush();

        // TerrainColliderも再設定
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null)
        {
            collider.terrainData = null;
            collider.terrainData = savedTd;
        }

        InternalEditorUtility.RepaintAllViews();
        SceneView.RepaintAll();
    }
}
