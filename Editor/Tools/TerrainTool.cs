using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Terrain操作用MCPツール。
/// 地形の高さマップ設定、なだらかな丘・谷・川の生成が可能。
/// </summary>
[McpServerToolType, Description("Unity Terrain manipulation tools for creating landscapes with hills, valleys, and river beds.")]
internal sealed class TerrainTool
{
    [McpServerTool, Description("Get information about the active Terrain in the scene (size, resolution, height range).")]
    public async ValueTask<string> Terrain_GetInfo()
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return "Error: No active Terrain found in scene.";

            var td = terrain.terrainData;
            var heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
            float minH = float.MaxValue, maxH = float.MinValue;
            foreach (var h in heights)
            {
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }

            return $"Terrain: {terrain.name}\n" +
                   $"Size: {td.size.x} x {td.size.z} m (height range: {td.size.y}m)\n" +
                   $"Heightmap Resolution: {td.heightmapResolution}\n" +
                   $"Current Height: min={minH * td.size.y:F1}m, max={maxH * td.size.y:F1}m\n" +
                   $"Position: {terrain.transform.position}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Set terrain height using Perlin noise to create natural hills and valleys. baseHeight is the base elevation (0-1), amplitude controls hill height (0-1), frequency controls hill density (1-10), octaves adds detail layers (1-4), seed for randomization.")]
    public async ValueTask<string> Terrain_SetPerlinHeight(
        [Description("Base height (0.0-1.0, default 0.3)")] float baseHeight = 0.3f,
        [Description("Hill amplitude (0.0-1.0, default 0.15)")] float amplitude = 0.15f,
        [Description("Noise frequency - higher = more hills (1-10, default 3)")] float frequency = 3f,
        [Description("Detail octaves (1-4, default 3)")] int octaves = 3,
        [Description("Random seed (default 42)")] int seed = 42)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return "Error: No active Terrain found.";

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] heights = new float[res, res];

            float offsetX = seed * 0.7f;
            float offsetZ = seed * 1.3f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / res * frequency + offsetX;
                    float nz = (float)z / res * frequency + offsetZ;

                    float h = 0f;
                    float amp = 1f;
                    float freq = 1f;
                    float totalAmp = 0f;

                    for (int o = 0; o < octaves; o++)
                    {
                        h += Mathf.PerlinNoise(nx * freq, nz * freq) * amp;
                        totalAmp += amp;
                        amp *= 0.5f;
                        freq *= 2f;
                    }

                    h /= totalAmp;
                    heights[z, x] = baseHeight + h * amplitude;
                }
            }

            Undo.RegisterCompleteObjectUndo(td, "Terrain SetPerlinHeight");
            td.SetHeights(0, 0, heights);

            return $"Terrain height set: base={baseHeight}, amplitude={amplitude}, frequency={frequency}, octaves={octaves}, seed={seed}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Carve a river bed into the terrain along a path defined by start and end points. The river follows a natural meandering path with adjustable width and depth.")]
    public async ValueTask<string> Terrain_CarveRiver(
        [Description("River start X position in terrain space (0-1, default 0.1)")] float startX = 0.1f,
        [Description("River start Z position in terrain space (0-1, default 0.5)")] float startZ = 0.5f,
        [Description("River end X position in terrain space (0-1, default 0.9)")] float endX = 0.9f,
        [Description("River end Z position in terrain space (0-1, default 0.5)")] float endZ = 0.5f,
        [Description("River width in heightmap pixels (default 8)")] int width = 8,
        [Description("River depth as height reduction (0.0-0.2, default 0.05)")] float depth = 0.05f,
        [Description("Meander amount - how much the river curves (0-50, default 20)")] float meander = 20f,
        [Description("Meander frequency (1-5, default 2)")] float meanderFreq = 2f,
        [Description("Random seed for meander pattern (default 123)")] int seed = 123)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return "Error: No active Terrain found.";

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] heights = td.GetHeights(0, 0, res, res);

            Undo.RegisterCompleteObjectUndo(td, "Terrain CarveRiver");

            // 川のパスを生成（ベジェ的な補間 + PerlinNoiseでmeander）
            int steps = res * 2;
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;

                // 基本パス（直線補間）
                float px = Mathf.Lerp(startX, endX, t);
                float pz = Mathf.Lerp(startZ, endZ, t);

                // meander（蛇行）
                float noise = Mathf.PerlinNoise(t * meanderFreq + seed * 0.1f, seed * 0.7f) - 0.5f;
                // 進行方向に垂直な方向に蛇行
                float dx = endX - startX;
                float dz = endZ - startZ;
                float len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len > 0.001f)
                {
                    float perpX = -dz / len;
                    float perpZ = dx / len;
                    px += perpX * noise * meander / res;
                    pz += perpZ * noise * meander / res;
                }

                int cx = Mathf.RoundToInt(px * (res - 1));
                int cz = Mathf.RoundToInt(pz * (res - 1));

                // 川幅の範囲で高さを下げる
                for (int dxx = -width; dxx <= width; dxx++)
                {
                    for (int dzz = -width; dzz <= width; dzz++)
                    {
                        int ix = cx + dxx;
                        int iz = cz + dzz;
                        if (ix < 0 || ix >= res || iz < 0 || iz >= res) continue;

                        float dist = Mathf.Sqrt(dxx * dxx + dzz * dzz);
                        if (dist > width) continue;

                        // 中心ほど深く、端は浅いプロファイル
                        float falloff = 1f - (dist / width);
                        falloff = falloff * falloff; // 二乗で自然なプロファイル
                        float reduction = depth * falloff;

                        heights[iz, ix] = Mathf.Max(0f, heights[iz, ix] - reduction);
                    }
                }
            }

            td.SetHeights(0, 0, heights);
            return $"River carved: ({startX},{startZ}) -> ({endX},{endZ}), width={width}, depth={depth}, meander={meander}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Smooth the terrain heights to reduce sharp edges. Useful after carving rivers or setting heights.")]
    public async ValueTask<string> Terrain_Smooth(
        [Description("Number of smoothing passes (1-10, default 3)")] int passes = 3,
        [Description("Smoothing strength (0.0-1.0, default 0.5)")] float strength = 0.5f)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return "Error: No active Terrain found.";

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] heights = td.GetHeights(0, 0, res, res);

            Undo.RegisterCompleteObjectUndo(td, "Terrain Smooth");

            for (int pass = 0; pass < passes; pass++)
            {
                float[,] smoothed = new float[res, res];
                for (int z = 1; z < res - 1; z++)
                {
                    for (int x = 1; x < res - 1; x++)
                    {
                        float avg = (heights[z - 1, x] + heights[z + 1, x] +
                                     heights[z, x - 1] + heights[z, x + 1] +
                                     heights[z - 1, x - 1] + heights[z - 1, x + 1] +
                                     heights[z + 1, x - 1] + heights[z + 1, x + 1]) / 8f;
                        smoothed[z, x] = Mathf.Lerp(heights[z, x], avg, strength);
                    }
                }
                // 端のコピー
                for (int i = 0; i < res; i++)
                {
                    smoothed[0, i] = heights[0, i];
                    smoothed[res - 1, i] = heights[res - 1, i];
                    smoothed[i, 0] = heights[i, 0];
                    smoothed[i, res - 1] = heights[i, res - 1];
                }
                heights = smoothed;
            }

            td.SetHeights(0, 0, heights);
            return $"Terrain smoothed: {passes} passes, strength={strength}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }

    [McpServerTool, Description("Flatten a circular area on the terrain to a specific height. Useful for creating flat areas for buildings or clearings.")]
    public async ValueTask<string> Terrain_FlattenArea(
        [Description("Center X position in terrain space (0-1)")] float centerX = 0.5f,
        [Description("Center Z position in terrain space (0-1)")] float centerZ = 0.5f,
        [Description("Radius in heightmap pixels (default 20)")] int radius = 20,
        [Description("Target height (0-1, default -1 for auto-average)")] float targetHeight = -1f)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return "Error: No active Terrain found.";

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] heights = td.GetHeights(0, 0, res, res);

            int cx = Mathf.RoundToInt(centerX * (res - 1));
            int cz = Mathf.RoundToInt(centerZ * (res - 1));

            Undo.RegisterCompleteObjectUndo(td, "Terrain FlattenArea");

            // 自動高さ: エリア内の平均
            if (targetHeight < 0f)
            {
                float sum = 0f;
                int count = 0;
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int ix = cx + dx, iz = cz + dz;
                        if (ix < 0 || ix >= res || iz < 0 || iz >= res) continue;
                        if (dx * dx + dz * dz > radius * radius) continue;
                        sum += heights[iz, ix];
                        count++;
                    }
                targetHeight = count > 0 ? sum / count : 0.3f;
            }

            for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int ix = cx + dx, iz = cz + dz;
                    if (ix < 0 || ix >= res || iz < 0 || iz >= res) continue;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > radius) continue;

                    // 端をなだらかにブレンド
                    float blend = Mathf.Clamp01((radius - dist) / (radius * 0.3f));
                    heights[iz, ix] = Mathf.Lerp(heights[iz, ix], targetHeight, blend);
                }

            td.SetHeights(0, 0, heights);
            return $"Terrain flattened at ({centerX},{centerZ}), radius={radius}, height={targetHeight:F3}";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            throw;
        }
    }
}
