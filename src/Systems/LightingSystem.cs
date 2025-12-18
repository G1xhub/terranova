using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.World;
using System;

namespace TerraNova.Systems;

/// <summary>
/// Dynamic lighting system with RGB color support and light propagation
/// </summary>
public class LightingSystem : IDisposable
{
    private readonly GameWorld _world;
    private readonly GraphicsDevice _graphicsDevice;
    
    // RGB Light map (stores light color for each tile)
    private readonly Color[,] _lightMap;
    private Texture2D? _lightTexture;
    private Color[] _lightTextureData;

    public Texture2D LightMap => _lightTexture!;
    
    // Configuration
    private const float SunlightDecay = 0.85f;      // More dramatic falloff
    private const float AirLightDecay = 0.96f;       
    private const byte MaxLight = 255;
    private const byte MinAmbient = 15;               
    
    // Dirty region tracking for efficient updates
    private Rectangle _dirtyRegion;
    private bool _fullUpdateRequired = true;
    
    // Sunlight color based on time of day
    private Color _currentSunColor = Color.White;
    
    public LightingSystem(GameWorld world, GraphicsDevice graphicsDevice)
    {
        _world = world;
        _graphicsDevice = graphicsDevice;
        
        _lightMap = new Color[world.Width, world.Height];
        _lightTextureData = new Color[world.Width * world.Height];
        
        CreateLightTexture();
    }
    
    private void CreateLightTexture()
    {
        _lightTexture?.Dispose();
        _lightTexture = new Texture2D(_graphicsDevice, _world.Width, _world.Height);
    }
    
    public void Update(float dayTime)
    {
        // Always full update for now to ensure smoothness, can optimize later if needed
        CalculateFullLighting(dayTime);
        UpdateLightTexture();
    }
    
    private void CalculateFullLighting(float dayTime)
    {
        _currentSunColor = CalculateSunColor(dayTime);
        float sunBrightness = CalculateSunBrightness(dayTime);
        
        // Clear light map
        Color ambientColor = new Color(MinAmbient, MinAmbient, MinAmbient);
        for (int y = 0; y < _world.Height; y++)
        {
            for (int x = 0; x < _world.Width; x++)
            {
                _lightMap[x, y] = ambientColor;
            }
        }
        
        // Phase 1: Sunlight from above
        // Parallelize for speed? No, column dependency prevents simple parallelization in Y.
        // X is independent though.
        for (int x = 0; x < _world.Width; x++)
        {
            float currentIntensity = sunBrightness;
            
            for (int y = 0; y < _world.Height; y++)
            {
                var tile = _world.GetTile(x, y);
                
                // Light passes through
                if (tile == TileType.Air || !TileProperties.IsSolid(tile) || tile == TileType.WoodPlatform)
                {
                    byte lightValue = (byte)(MaxLight * currentIntensity);
                    Color sunLight = MultiplyColor(_currentSunColor, lightValue);
                    _lightMap[x, y] = MaxColor(_lightMap[x, y], sunLight);
                    
                    currentIntensity *= AirLightDecay;
                }
                else
                {
                    // Solid tile blocks light but gets lit itself
                    currentIntensity *= SunlightDecay;
                    byte lightValue = (byte)Math.Max(MaxLight * currentIntensity, MinAmbient);
                    Color sunLight = MultiplyColor(_currentSunColor, lightValue);
                    _lightMap[x, y] = MaxColor(_lightMap[x, y], sunLight);
                    
                    // Emissive check
                    int emittedLight = TileProperties.GetLightLevel(tile);
                    if (emittedLight > 0)
                    {
                        Color emitColor = GetTileLightColor(tile);
                        // Make light sources VERY bright
                        _lightMap[x, y] = MaxColor(_lightMap[x, y], emitColor);
                    }
                }
            }
        }
        
        // Phase 2: Propagation
        PropagateLight(4); // More passes for smoother spreading
        
        // Phase 3: Point Lights (Torches, etc)
        AddEmissiveLights();
    }
    
    private void PropagateLight(int passes)
    {
        for (int pass = 0; pass < passes; pass++)
        {
            // Forward pass
            for (int y = 0; y < _world.Height; y++)
            {
                for (int x = 0; x < _world.Width; x++)
                {
                    // Check Left and Top
                    if (x > 0) SpreadLight(x, y, x - 1, y);
                    if (y > 0) SpreadLight(x, y, x, y - 1);
                }
            }
            
            // Backward pass
            for (int y = _world.Height - 1; y >= 0; y--)
            {
                for (int x = _world.Width - 1; x >= 0; x--)
                {
                    // Check Right and Bottom
                    if (x < _world.Width - 1) SpreadLight(x, y, x + 1, y);
                    if (y < _world.Height - 1) SpreadLight(x, y, x, y + 1);
                }
            }
        }
    }
    
    private void SpreadLight(int x, int y, int nx, int ny)
    {
        // Spread from neighbor (nx, ny) to current (x, y)
        Color neighborLight = _lightMap[nx, ny];
        
        // If neighbor is dark, nothing to spread
        if (neighborLight.R <= MinAmbient && neighborLight.G <= MinAmbient && neighborLight.B <= MinAmbient) return;
        
        var tile = _world.GetTile(x, y);
        float decay = TileProperties.IsSolid(tile) ? SunlightDecay : AirLightDecay;
        
        Color spread = new Color(
            (byte)(neighborLight.R * decay),
            (byte)(neighborLight.G * decay),
            (byte)(neighborLight.B * decay)
        );
        
        _lightMap[x, y] = MaxColor(_lightMap[x, y], spread);
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
                    Color lightColor = GetTileLightColor(tile);
                    AddPointLight(x, y, lightLevel, lightColor);
                }
            }
        }
    }
    
    private void AddPointLight(int centerX, int centerY, int intensity, Color color)
    {
        int radius = intensity * 2; // Increased radius for better atmosphere
        
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                
                if (x < 0 || x >= _world.Width || y < 0 || y >= _world.Height) continue;
                
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;
                
                float falloff = 1f - (dist / radius);
                falloff = falloff * falloff; // Exponential falloff looks more natural
                
                Color lightAtPoint = MultiplyColor(color, (byte)(255 * falloff));
                _lightMap[x, y] = MaxColor(_lightMap[x, y], lightAtPoint);
            }
        }
    }
    
    public static Color GetTileLightColor(TileType type) => type switch
    {
        TileType.Torch => new Color(255, 220, 180),      
        TileType.Lava => new Color(255, 100, 50),        
        TileType.Furnace => new Color(255, 180, 100),    
        _ => Color.White
    };
    
    private float CalculateSunBrightness(float dayTime)
    {
        if (dayTime >= 0.2f && dayTime <= 0.8f) // Wider day
        {
            float t = (dayTime - 0.2f) / 0.6f;
            return 0.1f + 0.9f * (float)Math.Sin(t * Math.PI);
        }
        return 0.05f; // Darker nights
    }
    
    private Color CalculateSunColor(float dayTime)
    {
        // Simple Sunrise/Sunset logic
        if (dayTime > 0.2f && dayTime < 0.3f) return Color.Lerp(Color.OrangeRed, Color.White, (dayTime - 0.2f) * 10);
        if (dayTime > 0.7f && dayTime < 0.8f) return Color.Lerp(Color.White, Color.OrangeRed, (dayTime - 0.7f) * 10);
        if (dayTime < 0.2f || dayTime > 0.8f) return new Color(30, 30, 50); // Moonlight
        return Color.White;
    }
    
    private void UpdateLightTexture()
    {
        // Generate texture for Multiply blending
        // The texture should contain the LIGHT COLOR directly.
        // When drawing: (WorldColor * LightMapColor)
        
        for (int i = 0; i < _lightTextureData.Length; i++)
        {
            int x = i % _world.Width;
            int y = i / _world.Width;
            _lightTextureData[i] = _lightMap[x, y];
        }
        
        _lightTexture?.SetData(_lightTextureData);
    }
    
    // Helper: multiply color by intensity
    private static Color MultiplyColor(Color color, byte intensity)
    {
        float factor = intensity / 255f;
        return new Color(
            (byte)(color.R * factor),
            (byte)(color.G * factor),
            (byte)(color.B * factor)
        );
    }
    
    private static Color MaxColor(Color a, Color b)
    {
        return new Color(Math.Max(a.R, b.R), Math.Max(a.G, b.G), Math.Max(a.B, b.B));
    }
    
    public void ForceFullUpdate() => _fullUpdateRequired = true;
    
    public void Dispose()
    {
        _lightTexture?.Dispose();
    }
}
