using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.World;

namespace TerraNova.Systems;

/// <summary>
/// Dynamic lighting system with light propagation
/// Uses a light map texture for efficient rendering
/// </summary>
public class LightingSystem : IDisposable
{
    private readonly GameWorld _world;
    private readonly GraphicsDevice _graphicsDevice;
    
    // Light map (stores light levels for each tile)
    private readonly byte[,] _lightMap;
    private readonly Color[,] _lightColorMap; // RGB light colors
    private Texture2D? _lightTexture;
    private Color[] _lightTextureData;
    
    // Configuration
    private const float SunlightDecay = 0.92f;      // How much light decreases through solid tiles
    private const float AirLightDecay = 0.98f;       // Light decay through air (increased for softer spread)
    private const byte MaxLight = 255;
    private const byte MinAmbient = 8;               // Minimum light in caves
    
    // Dirty region tracking for efficient updates
    private Rectangle _dirtyRegion;
    private bool _fullUpdateRequired = true;
    
    // Dynamic light intensity (flickering, pulsing)
    private float _timeAccumulator = 0f;
    private readonly Dictionary<(int x, int y), float> _lightIntensityModifiers = new();
    
    public LightingSystem(GameWorld world, GraphicsDevice graphicsDevice)
    {
        _world = world;
        _graphicsDevice = graphicsDevice;
        
        _lightMap = new byte[world.Width, world.Height];
        _lightColorMap = new Color[world.Width, world.Height];
        _lightTextureData = new Color[world.Width * world.Height];
        
        CreateLightTexture();
    }
    
    private void CreateLightTexture()
    {
        _lightTexture?.Dispose();
        _lightTexture = new Texture2D(_graphicsDevice, _world.Width, _world.Height);
    }
    
    /// <summary>
    /// Update lighting for the world
    /// </summary>
    public void Update(float dayTime, float deltaTime = 0f)
    {
        // Update time accumulator for dynamic effects
        _timeAccumulator += deltaTime;
        
        if (_fullUpdateRequired)
        {
            CalculateFullLighting(dayTime);
            _fullUpdateRequired = false;
        }
        else
        {
            // Incremental update for changed regions
            // TODO: Implement dirty region updates
        }
        
        // Update dynamic light intensity modifiers (flickering, pulsing)
        UpdateDynamicLightIntensity();
        
        UpdateLightTexture();
    }
    
    private void UpdateDynamicLightIntensity()
    {
        // Clear old modifiers and recalculate for light sources
        _lightIntensityModifiers.Clear();
        
        for (int y = 0; y < _world.Height; y++)
        {
            for (int x = 0; x < _world.Width; x++)
            {
                var tile = _world.GetTile(x, y);
                int lightLevel = TileProperties.GetLightLevel(tile);
                
                if (lightLevel > 0)
                {
                    float modifier = GetLightIntensityModifier(tile, x, y);
                    _lightIntensityModifiers[(x, y)] = modifier;
                }
            }
        }
    }
    
    private float GetLightIntensityModifier(TileType tile, int x, int y)
    {
        // Use position-based seed for consistent flickering per light source
        float seed = (x * 137.5f + y * 97.3f) * 0.01f;
        
        return tile switch
        {
            TileType.Torch => 0.85f + 0.15f * (MathF.Sin(_timeAccumulator * 8f + seed) * 0.5f + 0.5f), // Gentle flicker
            TileType.Furnace => 0.9f + 0.1f * (MathF.Sin(_timeAccumulator * 4f + seed) * 0.5f + 0.5f), // Slow pulse
            TileType.Lava => 0.88f + 0.12f * (MathF.Sin(_timeAccumulator * 6f + seed * 2f) * 0.5f + 0.5f), // Medium pulse
            _ => 1.0f // No flicker for other sources
        };
    }
    
    private void CalculateFullLighting(float dayTime)
    {
        // Calculate sun brightness and color based on time of day
        float sunBrightness = CalculateSunBrightness(dayTime);
        Color sunColor = CalculateSunColor(dayTime);
        byte skyLight = (byte)(MaxLight * sunBrightness);
        
        // Clear light maps
        Array.Clear(_lightMap, 0, _lightMap.Length);
        for (int y = 0; y < _world.Height; y++)
        {
            for (int x = 0; x < _world.Width; x++)
            {
                _lightColorMap[x, y] = Color.Black;
            }
        }
        
        // Phase 1: Sunlight from above with color
        for (int x = 0; x < _world.Width; x++)
        {
            byte currentLight = skyLight;
            Color currentColor = sunColor;
            
            // Get biome for ambient light
            var biome = GetBiomeAt(x, 0);
            Color ambientColor = GetBiomeAmbientColor(biome, dayTime);
            
            for (int y = 0; y < _world.Height; y++)
            {
                var tile = _world.GetTile(x, y);
                
                if (tile == TileType.Air || TileProperties.IsLiquid(tile))
                {
                    _lightMap[x, y] = currentLight;
                    // Blend sunlight with ambient
                    _lightColorMap[x, y] = Color.Lerp(ambientColor, currentColor, currentLight / 255f);
                    currentLight = (byte)(currentLight * AirLightDecay);
                    // Fade color towards ambient
                    currentColor = Color.Lerp(ambientColor, currentColor, AirLightDecay);
                }
                else
                {
                    // Solid tile blocks light
                    currentLight = (byte)(currentLight * SunlightDecay);
                    _lightMap[x, y] = Math.Max(currentLight, MinAmbient);
                    _lightColorMap[x, y] = Color.Lerp(ambientColor, currentColor, currentLight / 255f);
                    
                    // Check if tile emits light
                    int emittedLight = TileProperties.GetLightLevel(tile);
                    if (emittedLight > 0)
                    {
                        Color lightColor = GetLightColorForTile(tile);
                        byte lightValue = (byte)(emittedLight * 20);
                        _lightMap[x, y] = Math.Max(_lightMap[x, y], lightValue);
                        _lightColorMap[x, y] = Color.Lerp(_lightColorMap[x, y], lightColor, lightValue / 255f);
                    }
                }
            }
        }
        
        // Phase 2: Add light from light-emitting tiles with colors (do this before propagation)
        AddEmissiveLights();
        
        // Phase 3: Light propagation with color (multiple passes for softer spread)
        // This propagates both sunlight and light from sources
        PropagateLightWithColor(8); // More passes for better light spread
    }
    
    private BiomeType GetBiomeAt(int x, int y)
    {
        // Use world's biome system
        return _world.GetBiomeAt(x, y);
    }
    
    private Color GetBiomeAmbientColor(BiomeType biome, float dayTime)
    {
        float brightness = CalculateSunBrightness(dayTime);
        
        return biome switch
        {
            BiomeType.Desert => Color.Lerp(
                new Color(70, 55, 45), // Night: warmer dark tones
                new Color(255, 245, 210), // Day: warmer, more golden bright
                brightness
            ),
            BiomeType.Snow => Color.Lerp(
                new Color(45, 55, 75), // Night: cool dark (slightly warmer)
                new Color(245, 252, 255), // Day: cool bright
                brightness
            ),
            BiomeType.Jungle => Color.Lerp(
                new Color(35, 55, 35), // Night: green dark
                new Color(210, 255, 210), // Day: green bright
                brightness
            ),
            _ => Color.Lerp(
                new Color(45, 55, 65), // Night: slightly warmer neutral dark
                new Color(210, 225, 245), // Day: slightly warmer neutral bright
                brightness
            )
        };
    }
    
    private Color GetLightColorForTile(TileType tile)
    {
        return tile switch
        {
            TileType.Torch => new Color(255, 200, 120), // Warmer orange - more cozy
            TileType.Furnace => new Color(255, 120, 60), // Hotter orange-red for warmth
            TileType.Lava => new Color(255, 160, 60), // Brighter, warmer orange
            _ => Color.White
        };
    }
    
    private Color CalculateSunColor(float dayTime)
    {
        // dayTime: 0 = midnight, 0.25 = 6am, 0.5 = noon, 0.75 = 6pm
        
        if (dayTime >= 0.25f && dayTime <= 0.75f)
        {
            // Daytime - warm to cool with warmer transitions
            float t = (dayTime - 0.25f) / 0.5f;
            if (t < 0.3f) // Morning - warm golden sunrise
                return Color.Lerp(new Color(255, 210, 160), new Color(255, 245, 230), t / 0.3f);
            else if (t > 0.7f) // Evening - warm golden sunset (cozy feeling)
                return Color.Lerp(new Color(255, 245, 230), new Color(255, 200, 140), (t - 0.7f) / 0.3f);
            else // Midday - slightly warm white (not pure white)
                return new Color(255, 252, 248);
        }
        else
        {
            // Nighttime - warmer blue (less cold feeling)
            return new Color(90, 110, 160);
        }
    }
    
    private void PropagateLightWithColor(int passes)
    {
        for (int pass = 0; pass < passes; pass++)
        {
            // Left to right, top to bottom
            for (int y = 1; y < _world.Height - 1; y++)
            {
                for (int x = 1; x < _world.Width - 1; x++)
                {
                    SpreadLightWithColor(x, y);
                }
            }
            
            // Right to left, bottom to top
            for (int y = _world.Height - 2; y > 0; y--)
            {
                for (int x = _world.Width - 2; x > 0; x--)
                {
                    SpreadLightWithColor(x, y);
                }
            }
        }
    }
    
    private void SpreadLightWithColor(int x, int y)
    {
        var tile = _world.GetTile(x, y);
        bool isSolid = TileProperties.IsSolid(tile);
        
        // Different decay for solid vs air tiles
        float decay = isSolid ? SunlightDecay : AirLightDecay;
        
        // Get max light from all 8 neighbors (including diagonals) for smoother spread
        byte maxNeighbor = 0;
        Color neighborColor = Color.Black;
        
        var neighbors = new[]
        {
            (_lightMap[x - 1, y - 1], _lightColorMap[x - 1, y - 1], 1.414f), // Diagonal
            (_lightMap[x, y - 1], _lightColorMap[x, y - 1], 1.0f),           // Up
            (_lightMap[x + 1, y - 1], _lightColorMap[x + 1, y - 1], 1.414f), // Diagonal
            (_lightMap[x - 1, y], _lightColorMap[x - 1, y], 1.0f),           // Left
            (_lightMap[x + 1, y], _lightColorMap[x + 1, y], 1.0f),           // Right
            (_lightMap[x - 1, y + 1], _lightColorMap[x - 1, y + 1], 1.414f), // Diagonal
            (_lightMap[x, y + 1], _lightColorMap[x, y + 1], 1.0f),           // Down
            (_lightMap[x + 1, y + 1], _lightColorMap[x + 1, y + 1], 1.414f)  // Diagonal
        };
        
        foreach (var (light, color, dist) in neighbors)
        {
            if (light > maxNeighbor)
            {
                maxNeighbor = light;
                neighborColor = color;
            }
        }
        
        // Apply distance-based decay for diagonal neighbors
        // For now, use same decay for all (can be improved)
        byte spreadLight = (byte)(maxNeighbor * decay);
        
        // Only update if new light is brighter and significant
        if (spreadLight > _lightMap[x, y] && spreadLight > 5)
        {
            _lightMap[x, y] = spreadLight;
            // Blend colors based on light intensity
            float blendFactor = spreadLight / 255f;
            _lightColorMap[x, y] = Color.Lerp(_lightColorMap[x, y], neighborColor, blendFactor * 0.6f);
        }
    }
    
    private void AddEmissiveLights()
    {
        for (int y = 0; y < _world.Height; y++)
        {
            for (int x = 0; x < _world.Width; x++)
            {
                var tile = _world.GetTile(x, y);
                int lightLevel = TileProperties.GetLightLevel(tile);
                
                if (lightLevel > 0)
                {
                    AddPointLight(x, y, lightLevel);
                }
            }
        }
    }
    
    private void AddPointLight(int centerX, int centerY, int intensity)
    {
        var tile = _world.GetTile(centerX, centerY);
        Color lightColor = GetLightColorForTile(tile);
        
        // Increase radius for better light spread
        // Torch (12) -> 18 tiles, Furnace (8) -> 12 tiles, Lava (15) -> 22 tiles
        int baseRadius = intensity;
        int radius = baseRadius + (baseRadius / 2); // 50% more radius
        
        // Use BFS-like approach for better light propagation through air
        var queue = new Queue<(int x, int y, byte light, float distance)>();
        var visited = new HashSet<(int, int)>();
        
        // Start from center
        // Start from center with dynamic intensity modifier
        float intensityModifier = _lightIntensityModifiers.TryGetValue((centerX, centerY), out var mod) ? mod : 1.0f;
        byte centerLight = (byte)Math.Min(255, intensity * 25 * intensityModifier); // Apply dynamic modifier
        queue.Enqueue((centerX, centerY, centerLight, 0f));
        visited.Add((centerX, centerY));
        
        while (queue.Count > 0)
        {
            var (x, y, currentLight, dist) = queue.Dequeue();
            
            if (x < 0 || x >= _world.Width || y < 0 || y >= _world.Height)
                continue;
            
            if (dist > radius) continue;
            
            // Check if tile blocks light
            var tileAtPos = _world.GetTile(x, y);
            bool isSolid = TileProperties.IsSolid(tileAtPos);
            
            // Apply light if it's brighter than current
            if (currentLight > _lightMap[x, y])
            {
                _lightMap[x, y] = currentLight;
                // Blend light color
                float blendFactor = currentLight / 255f;
                _lightColorMap[x, y] = Color.Lerp(_lightColorMap[x, y], lightColor, blendFactor * 0.8f);
            }
            
            // Propagate to neighbors (only through air/non-solid tiles)
            if (!isSolid || (x == centerX && y == centerY)) // Allow light source itself to be solid
            {
                // Calculate light decay based on distance and tile type
                float decayFactor = isSolid ? 0.7f : 0.85f; // Less decay through air
                
                // Check 8 neighbors (including diagonals for smoother spread)
                var neighbors = new[]
                {
                    (x - 1, y - 1, 1.414f), // Diagonal
                    (x, y - 1, 1.0f),       // Up
                    (x + 1, y - 1, 1.414f), // Diagonal
                    (x - 1, y, 1.0f),       // Left
                    (x + 1, y, 1.0f),       // Right
                    (x - 1, y + 1, 1.414f), // Diagonal
                    (x, y + 1, 1.0f),       // Down
                    (x + 1, y + 1, 1.414f)  // Diagonal
                };
                
                foreach (var (nx, ny, stepDist) in neighbors)
                {
                    if (nx < 0 || nx >= _world.Width || ny < 0 || ny >= _world.Height)
                        continue;
                    
                    if (visited.Contains((nx, ny))) continue;
                    
                    float newDist = dist + stepDist;
                    if (newDist > radius) continue;
                    
                    // Calculate new light level with softer distance-based falloff
                    // Use smoother curve for more cozy, natural light spread
                    float normalizedDist = newDist / radius;
                    // Softer falloff: starts gentle, then drops more gradually
                    float falloff = 1f - (normalizedDist * normalizedDist); // Quadratic for softer edges
                    falloff = MathF.Pow(falloff, 1.2f); // Additional smoothing for cozy feel
                    
                    byte newLight = (byte)(currentLight * decayFactor * falloff);
                    
                    // Only propagate if light is significant
                    if (newLight > 10) // Minimum threshold
                    {
                        queue.Enqueue((nx, ny, newLight, newDist));
                        visited.Add((nx, ny));
                    }
                }
            }
        }
    }
    
    private float CalculateSunBrightness(float dayTime)
    {
        // dayTime: 0 = midnight, 0.25 = 6am, 0.5 = noon, 0.75 = 6pm
        
        if (dayTime >= 0.25f && dayTime <= 0.75f)
        {
            // Daytime
            float t = (dayTime - 0.25f) / 0.5f;
            return 0.3f + 0.7f * MathF.Sin(t * MathF.PI);
        }
        else
        {
            // Nighttime
            return 0.05f;
        }
    }
    
    private void UpdateLightTexture()
    {
        if (_lightTexture == null) return;
        
        for (int y = 0; y < _world.Height; y++)
        {
            for (int x = 0; x < _world.Width; x++)
            {
                byte light = _lightMap[x, y];
                Color lightColor = _lightColorMap[x, y];
                
                // Calculate darkness overlay with color tint
                float lightFactor = light / 255f;
                byte darkness = (byte)(255 - light);
                
                // Apply color tint to darkness (warmer/cooler shadows)
                Color darknessColor = new Color(
                    (byte)(255 - (lightColor.R * (1 - lightFactor))),
                    (byte)(255 - (lightColor.G * (1 - lightFactor))),
                    (byte)(255 - (lightColor.B * (1 - lightFactor))),
                    darkness
                );
                
                _lightTextureData[y * _world.Width + x] = darknessColor;
            }
        }
        
        _lightTexture.SetData(_lightTextureData);
    }
    
    /// <summary>
    /// Get light color at a tile position
    /// </summary>
    public Color GetLightColor(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= _world.Width || tileY < 0 || tileY >= _world.Height)
            return Color.Black;
        return _lightColorMap[tileX, tileY];
    }
    
    /// <summary>
    /// Draw the lighting overlay
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (_lightTexture == null) return;
        
        // Draw lighting texture scaled to world size
        var destRect = new Rectangle(0, 0, _world.PixelWidth, _world.PixelHeight);
        
        spriteBatch.Draw(_lightTexture, destRect, Color.White);
    }
    
    /// <summary>
    /// Get light level at a tile position (0-255)
    /// </summary>
    public byte GetLight(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= _world.Width || tileY < 0 || tileY >= _world.Height)
            return 0;
        return _lightMap[tileX, tileY];
    }
    
    /// <summary>
    /// Mark a region as needing light recalculation
    /// </summary>
    public void MarkDirty(int tileX, int tileY, int radius = 12)
    {
        var newDirty = new Rectangle(
            tileX - radius,
            tileY - radius,
            radius * 2,
            radius * 2
        );
        
        if (_dirtyRegion.IsEmpty)
            _dirtyRegion = newDirty;
        else
            _dirtyRegion = Rectangle.Union(_dirtyRegion, newDirty);
    }
    
    /// <summary>
    /// Force full lighting recalculation
    /// </summary>
    public void ForceFullUpdate()
    {
        _fullUpdateRequired = true;
    }
    
    public void Dispose()
    {
        _lightTexture?.Dispose();
    }
}
