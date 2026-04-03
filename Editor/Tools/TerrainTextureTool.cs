using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEngine;
using UnityEditor;

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

            var grassProto = new DetailPrototype();
            grassProto.renderMode = DetailRenderMode.GrassBillboard;
            grassProto.healthyColor = grassColor;
            grassProto.dryColor = grassDryColor;
            grassProto.minHeight = minHeight;
            grassProto.maxHeight = maxHeight;
            grassProto.minWidth = 0.3f;
            grassProto.maxWidth = 0.8f;
            grassProto.noiseSpread = 0.3f;

            td.detailPrototypes = new DetailPrototype[] { grassProto };
            td.SetDetailResolution(256, 16);

            // 傾斜・高さベースで草密度を設定
            int detailRes = td.detailResolution;
            int[,] grassMap = new int[detailRes, detailRes];

            float[,] heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);

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
                        grassMap[z, x] = Mathf.RoundToInt(density * slopeFactor * noise);
                    }
                }
            }

            Undo.RegisterCompleteObjectUndo(td, "Terrain AddGrass");
            td.SetDetailLayer(0, 0, 0, grassMap);

            // 草の描画距離設定
            terrain.detailObjectDistance = 80f;
            terrain.detailObjectDensity = 1f;

            return $"Grass added: density={density}, height={minHeight}-{maxHeight}m, maxSlope={maxSlope}°";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
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
        AssetDatabase.CreateAsset(layer, layerPath);

        AssetDatabase.SaveAssets();
        return layer;
    }
}
