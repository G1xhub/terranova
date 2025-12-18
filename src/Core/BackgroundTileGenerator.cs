using Microsoft.Xna.Framework;
using TerraNova.World;

namespace TerraNova.Core;

/// <summary>
/// Generates procedural background tiles for sky, landscape, mountains, and underground
/// </summary>
public static class BackgroundTileGenerator
{
    private static SimplexNoise? _skyNoise;
    private static SimplexNoise? _landscapeNoise;
    private static SimplexNoise? _mountainNoise;
    private static SimplexNoise? _undergroundNoise;
    
    private const int TileSize = 16;
    
    public static void Initialize(int seed)
    {
        _skyNoise = new SimplexNoise(seed + 100);
        _landscapeNoise = new SimplexNoise(seed + 200);
        _mountainNoise = new SimplexNoise(seed + 300);
        _undergroundNoise = new SimplexNoise(seed + 400);
    }
    
    /// <summary>
    /// Generate a sky tile - simple solid color (no flickering)
    /// </summary>
    public static Color[] GenerateSkyTile(int tileX, int tileY)
    {
        var colors = new Color[TileSize * TileSize];
        
        // Simple sky blue - solid color, no noise, no flickering
        Color skyColor = new Color(135, 206, 250);
        
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = skyColor;
        }
        
        return colors;
    }
    
    /// <summary>
    /// Generate a landscape tile (grass, dirt) with noise variation - optimized
    /// </summary>
    public static Color[] GenerateLandscapeTile(int tileX, int tileY)
    {
        var colors = new Color[TileSize * TileSize];
        
        // Landscape colors
        Color grassColor = new Color(86, 152, 23);
        Color dirtColor = new Color(139, 90, 43);
        
        // Pre-calculate noise per tile (larger scale = fewer calls)
        double tileNoise = _landscapeNoise!.Noise2D(tileX * 0.25, tileY * 0.25);
        double tileDetail = _landscapeNoise.Noise2D(tileX * 0.6, tileY * 0.6);
        
        // Mix grass and dirt based on noise
        float mix = (float)((tileNoise + 1.0) * 0.5); // Normalize to 0-1
        Color baseColor = Color.Lerp(grassColor, dirtColor, mix * 0.3f);
        
        // Add detail variation
        float detail = (float)(tileDetail * 0.1);
        baseColor = new Color(
            Math.Clamp((int)(baseColor.R + detail * 30), 0, 255),
            Math.Clamp((int)(baseColor.G + detail * 30), 0, 255),
            Math.Clamp((int)(baseColor.B + detail * 30), 0, 255)
        );
        
        // Use same base color for entire tile with subtle pattern variation
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                // Simple pattern-based variation
                float pattern = ((x + y * 2) % 4) * 0.02f - 0.03f;
                Color pixelColor = new Color(
                    Math.Clamp((int)(baseColor.R + pattern * 20), 0, 255),
                    Math.Clamp((int)(baseColor.G + pattern * 20), 0, 255),
                    Math.Clamp((int)(baseColor.B + pattern * 20), 0, 255)
                );
                
                colors[y * TileSize + x] = pixelColor;
            }
        }
        
        return colors;
    }
    
    /// <summary>
    /// Generate a mountain tile (darker stone) with noise for silhouette - optimized
    /// </summary>
    public static Color[] GenerateMountainTile(int tileX, int tileY)
    {
        var colors = new Color[TileSize * TileSize];
        
        // Mountain colors - darker stone
        Color mountainDark = new Color(60, 60, 70);
        Color mountainLight = new Color(100, 100, 110);
        
        // Pre-calculate noise per tile (larger scale, fewer calls)
        double mountainNoise = _mountainNoise!.RidgedNoise(tileX * 0.2, tileY * 0.2, 2); // Reduced octaves
        double detailNoise = _mountainNoise.Noise2D(tileX * 0.4, tileY * 0.4);
        
        // Create mountain silhouette effect
        float mountainFactor = (float)Math.Clamp(mountainNoise, 0, 1);
        Color baseColor = Color.Lerp(mountainDark, mountainLight, mountainFactor);
        
        // Add texture detail
        float detail = (float)(detailNoise * 0.15);
        baseColor = new Color(
            Math.Clamp((int)(baseColor.R + detail * 40), 0, 255),
            Math.Clamp((int)(baseColor.G + detail * 40), 0, 255),
            Math.Clamp((int)(baseColor.B + detail * 40), 0, 255)
        );
        
        // Use same base color for entire tile with subtle variation
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                // Simple pattern-based variation
                float pattern = ((x * 2 + y) % 5) * 0.02f - 0.04f;
                Color pixelColor = new Color(
                    Math.Clamp((int)(baseColor.R + pattern * 30), 0, 255),
                    Math.Clamp((int)(baseColor.G + pattern * 30), 0, 255),
                    Math.Clamp((int)(baseColor.B + pattern * 30), 0, 255)
                );
                
                colors[y * TileSize + x] = pixelColor;
            }
        }
        
        return colors;
    }
    
    /// <summary>
    /// Generate an underground rock tile - simple dark stone (no flickering)
    /// </summary>
    public static Color[] GenerateUndergroundTile(int tileX, int tileY)
    {
        var colors = new Color[TileSize * TileSize];
        
        // Simple dark stone color - deterministic based on tile position
        // Use a simple hash for subtle but consistent variation
        int hash = (tileX * 73856093) ^ (tileY * 19349663);
        float variation = ((hash & 0xFF) / 255f) * 0.15f; // 0 to 0.15
        
        int baseR = (int)(25 + variation * 20);
        int baseG = (int)(25 + variation * 20);
        int baseB = (int)(30 + variation * 20);
        
        Color rockColor = new Color(baseR, baseG, baseB);
        
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = rockColor;
        }
        
        return colors;
    }
}

