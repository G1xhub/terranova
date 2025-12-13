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
    private Texture2D? _lightTexture;
    private Color[] _lightTextureData;
    
    // Configuration
    private const float SunlightDecay = 0.92f;      // How much light decreases through solid tiles
    private const float AirLightDecay = 0.97f;       // Light decay through air
    private const byte MaxLight = 255;
    private const byte MinAmbient = 8;               // Minimum light in caves
    
    // Dirty region tracking for efficient updates
    private Rectangle _dirtyRegion;
    private bool _fullUpdateRequired = true;
    
    public LightingSystem(GameWorld world, GraphicsDevice graphicsDevice)
    {
        _world = world;
        _graphicsDevice = graphicsDevice;
        
        _lightMap = new byte[world.Width, world.Height];
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
    public void Update(float dayTime)
    {
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
        
        UpdateLightTexture();
    }
    
    private void CalculateFullLighting(float dayTime)
    {
        // Calculate sun brightness based on time of day
        float sunBrightness = CalculateSunBrightness(dayTime);
        byte skyLight = (byte)(MaxLight * sunBrightness);
        
        // Clear light map
        Array.Clear(_lightMap, 0, _lightMap.Length);
        
        // Phase 1: Sunlight from above
        for (int x = 0; x < _world.Width; x++)
        {
            byte currentLight = skyLight;
            
            for (int y = 0; y < _world.Height; y++)
            {
                var tile = _world.GetTile(x, y);
                
                if (tile == TileType.Air || TileProperties.IsLiquid(tile))
                {
                    _lightMap[x, y] = currentLight;
                    currentLight = (byte)(currentLight * AirLightDecay);
                }
                else
                {
                    // Solid tile blocks light
                    currentLight = (byte)(currentLight * SunlightDecay);
                    _lightMap[x, y] = Math.Max(currentLight, MinAmbient);
                    
                    // Check if tile emits light
                    int emittedLight = TileProperties.GetLightLevel(tile);
                    if (emittedLight > 0)
                    {
                        _lightMap[x, y] = Math.Max(_lightMap[x, y], (byte)(emittedLight * 20));
                    }
                }
            }
        }
        
        // Phase 2: Light propagation (multiple passes for spreading)
        PropagateLight(3);
        
        // Phase 3: Add light from light-emitting tiles
        AddEmissiveLights();
    }
    
    private void PropagateLight(int passes)
    {
        for (int pass = 0; pass < passes; pass++)
        {
            // Left to right, top to bottom
            for (int y = 1; y < _world.Height - 1; y++)
            {
                for (int x = 1; x < _world.Width - 1; x++)
                {
                    SpreadLightToNeighbor(x, y);
                }
            }
            
            // Right to left, bottom to top
            for (int y = _world.Height - 2; y > 0; y--)
            {
                for (int x = _world.Width - 2; x > 0; x--)
                {
                    SpreadLightToNeighbor(x, y);
                }
            }
        }
    }
    
    private void SpreadLightToNeighbor(int x, int y)
    {
        var tile = _world.GetTile(x, y);
        float decay = TileProperties.IsSolid(tile) ? SunlightDecay : AirLightDecay;
        
        // Get max light from neighbors
        byte maxNeighbor = 0;
        maxNeighbor = Math.Max(maxNeighbor, _lightMap[x - 1, y]);
        maxNeighbor = Math.Max(maxNeighbor, _lightMap[x + 1, y]);
        maxNeighbor = Math.Max(maxNeighbor, _lightMap[x, y - 1]);
        maxNeighbor = Math.Max(maxNeighbor, _lightMap[x, y + 1]);
        
        byte spreadLight = (byte)(maxNeighbor * decay);
        
        if (spreadLight > _lightMap[x, y])
        {
            _lightMap[x, y] = spreadLight;
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
        int radius = intensity;
        
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                
                if (x < 0 || x >= _world.Width || y < 0 || y >= _world.Height)
                    continue;
                
                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;
                
                float falloff = 1f - (distance / radius);
                byte light = (byte)(intensity * 20 * falloff);
                
                _lightMap[x, y] = Math.Max(_lightMap[x, y], light);
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
                
                // Invert for darkness overlay (255 = fully lit = no overlay)
                byte darkness = (byte)(255 - light);
                _lightTextureData[y * _world.Width + x] = new Color((byte)0, (byte)0, (byte)0, darkness);
            }
        }
        
        _lightTexture.SetData(_lightTextureData);
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
