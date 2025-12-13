using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.Systems;

namespace TerraNova.World;

/// <summary>
/// A chunk of the world - stores tiles in a fixed-size grid
/// Chunks are loaded/unloaded based on player position for performance
/// </summary>
public class Chunk
{
    public const int Size = 32; // 32x32 tiles per chunk
    
    // Position in chunk coordinates (not pixels or tiles)
    public int ChunkX { get; }
    public int ChunkY { get; }
    
    // Tile data
    private readonly TileType[,] _tiles;
    private readonly byte[,] _wallTiles;  // Background walls
    private readonly byte[,] _lightLevels; // Cached light values
    
    // State flags
    public bool IsModified { get; private set; }
    public bool IsLightingDirty { get; set; } = true;
    public bool IsLoaded { get; private set; }
    
    // Bounding box in world pixels
    public Rectangle Bounds { get; }
    
    // Tile position bounds
    public int TileStartX => ChunkX * Size;
    public int TileStartY => ChunkY * Size;
    public int TileEndX => TileStartX + Size;
    public int TileEndY => TileStartY + Size;
    
    public Chunk(int chunkX, int chunkY)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        
        _tiles = new TileType[Size, Size];
        _wallTiles = new byte[Size, Size];
        _lightLevels = new byte[Size, Size];
        
        // Initialize light levels to bright by default (will be updated by lighting system)
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                _lightLevels[x, y] = 255;
        
        int pixelX = chunkX * Size * GameConfig.TileSize;
        int pixelY = chunkY * Size * GameConfig.TileSize;
        Bounds = new Rectangle(pixelX, pixelY, Size * GameConfig.TileSize, Size * GameConfig.TileSize);
        
        IsLoaded = true;
    }
    
    /// <summary>
    /// Get tile at local chunk coordinates
    /// </summary>
    public TileType GetTileLocal(int localX, int localY)
    {
        if (localX < 0 || localX >= Size || localY < 0 || localY >= Size)
            return TileType.Air;
        return _tiles[localX, localY];
    }
    
    /// <summary>
    /// Set tile at local chunk coordinates
    /// </summary>
    public void SetTileLocal(int localX, int localY, TileType type)
    {
        if (localX < 0 || localX >= Size || localY < 0 || localY >= Size)
            return;
        
        if (_tiles[localX, localY] != type)
        {
            _tiles[localX, localY] = type;
            IsModified = true;
            IsLightingDirty = true;
        }
    }
    
    /// <summary>
    /// Get wall tile at local chunk coordinates
    /// </summary>
    public byte GetWallLocal(int localX, int localY)
    {
        if (localX < 0 || localX >= Size || localY < 0 || localY >= Size)
            return 0;
        return _wallTiles[localX, localY];
    }
    
    /// <summary>
    /// Set wall tile at local chunk coordinates
    /// </summary>
    public void SetWallLocal(int localX, int localY, byte wallType)
    {
        if (localX < 0 || localX >= Size || localY < 0 || localY >= Size)
            return;
        
        if (_wallTiles[localX, localY] != wallType)
        {
            _wallTiles[localX, localY] = wallType;
            IsModified = true;
        }
    }
    
    /// <summary>
    /// Get cached light level at local coordinates
    /// </summary>
    public byte GetLightLocal(int localX, int localY)
    {
        if (localX < 0 || localX >= Size || localY < 0 || localY >= Size)
            return 0;
        return _lightLevels[localX, localY];
    }
    
    /// <summary>
    /// Set light level at local coordinates
    /// </summary>
    public void SetLightLocal(int localX, int localY, byte light)
    {
        if (localX < 0 || localX >= Size || localY < 0 || localY >= Size)
            return;
        _lightLevels[localX, localY] = light;
    }
    
    /// <summary>
    /// Fill entire chunk with a tile type (for generation)
    /// </summary>
    public void Fill(TileType type)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                _tiles[x, y] = type;
        IsModified = true;
        IsLightingDirty = true;
    }
    
    /// <summary>
    /// Check if a world tile position is within this chunk
    /// </summary>
    public bool ContainsTile(int worldTileX, int worldTileY)
    {
        return worldTileX >= TileStartX && worldTileX < TileEndX &&
               worldTileY >= TileStartY && worldTileY < TileEndY;
    }
    
    /// <summary>
    /// Convert world tile coordinates to local chunk coordinates
    /// </summary>
    public (int localX, int localY) WorldToLocal(int worldTileX, int worldTileY)
    {
        return (worldTileX - TileStartX, worldTileY - TileStartY);
    }
    
    private void DrawGlowEffect(SpriteBatch spriteBatch, Rectangle destRect, TileType tile, int lightLevel, float lightFactor)
    {
        // Get glow color based on tile type
        Color glowColor = GetGlowColor(tile);
        
        // Calculate glow intensity based on light level and current lighting
        float glowIntensity = (lightLevel / 15f) * lightFactor * 0.7f; // Increased from 0.6f for more visible glow
        glowIntensity = MathHelper.Clamp(glowIntensity, 0f, 1f);
        
        // Draw multiple glow layers for softer, more cozy effect
        // Outer glow (larger, more transparent)
        int outerGlowSize = GameConfig.TileSize + 8;
        int outerGlowOffset = (outerGlowSize - GameConfig.TileSize) / 2;
        var outerGlowRect = new Rectangle(
            destRect.X - outerGlowOffset,
            destRect.Y - outerGlowOffset,
            outerGlowSize,
            outerGlowSize
        );
        var outerGlowColor = glowColor * glowIntensity * 0.3f; // Soft outer halo
        spriteBatch.Draw(TextureManager.Pixel, outerGlowRect, outerGlowColor);
        
        // Inner glow (smaller, more intense)
        int innerGlowSize = GameConfig.TileSize + 4;
        int innerGlowOffset = (innerGlowSize - GameConfig.TileSize) / 2;
        var innerGlowRect = new Rectangle(
            destRect.X - innerGlowOffset,
            destRect.Y - innerGlowOffset,
            innerGlowSize,
            innerGlowSize
        );
        var innerGlowColor = glowColor * glowIntensity * 0.6f; // Brighter inner glow
        spriteBatch.Draw(TextureManager.Pixel, innerGlowRect, innerGlowColor);
    }
    
    private void DrawHeatEffect(SpriteBatch spriteBatch, Rectangle destRect, TileType tile)
    {
        // Draw heat distortion/wave effect around fire sources
        // Create a gradient overlay that suggests heat rising
        
        Color heatColor = tile == TileType.Furnace 
            ? new Color(255, 100, 30, 40) // Hot orange-red
            : new Color(255, 150, 50, 50); // Bright orange for lava
        
        // Draw multiple layers for heat wave effect
        for (int i = 0; i < 3; i++)
        {
            int heatSize = GameConfig.TileSize + 6 + i * 2;
            int heatOffset = (heatSize - GameConfig.TileSize) / 2;
            var heatRect = new Rectangle(
                destRect.X - heatOffset,
                destRect.Y - heatOffset - i * 2, // Slight upward offset for rising heat
                heatSize,
                heatSize
            );
            
            float alpha = (0.15f - i * 0.05f);
            var heatColorWithAlpha = heatColor * alpha;
            spriteBatch.Draw(TextureManager.Pixel, heatRect, heatColorWithAlpha);
        }
    }
    
    private static Color GetGlowColor(TileType tile)
    {
        return tile switch
        {
            TileType.Torch => new Color(255, 210, 130), // Warmer, more cozy orange
            TileType.Furnace => new Color(255, 130, 70), // Hotter orange-red for warmth
            TileType.Lava => new Color(255, 170, 70), // Brighter, warmer orange
            TileType.CopperOre => new Color(255, 190, 120), // Warmer copper glow
            TileType.IronOre => new Color(210, 210, 230), // Slightly warmer gray-blue
            TileType.GoldOre => new Color(255, 230, 120), // Warmer golden yellow
            TileType.DiamondOre => new Color(120, 210, 255), // Slightly warmer blue-white
            TileType.Coal => new Color(160, 160, 160), // Slightly warmer gray
            _ => Color.White
        };
    }
    
    /// <summary>
    /// Draw the chunk
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, LightingSystem? lightingSystem = null, GameWorld? world = null)
    {
        // Early out if chunk is not visible
        if (!camera.IsVisible(Bounds))
            return;
        
        var visibleArea = camera.VisibleArea;
        
        // Calculate visible tile range within chunk
        int startX = Math.Max(0, (visibleArea.Left - Bounds.Left) / GameConfig.TileSize);
        int startY = Math.Max(0, (visibleArea.Top - Bounds.Top) / GameConfig.TileSize);
        int endX = Math.Min(Size, (visibleArea.Right - Bounds.Left) / GameConfig.TileSize + 1);
        int endY = Math.Min(Size, (visibleArea.Bottom - Bounds.Top) / GameConfig.TileSize + 1);
        
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var tile = _tiles[x, y];
                if (tile == TileType.Air) continue;
                
                int worldX = (TileStartX + x) * GameConfig.TileSize;
                int worldY = (TileStartY + y) * GameConfig.TileSize;
                
                var sourceRect = TextureManager.GetTileRect(tile);
                var destRect = new Rectangle(worldX, worldY, GameConfig.TileSize, GameConfig.TileSize);
                
                // Apply lighting with color
                byte light = _lightLevels[x, y];
                float lightFactor = light / 255f;
                
                // Get light color from lighting system if available
                Color lightColor = lightingSystem?.GetLightColor(TileStartX + x, TileStartY + y) ?? Color.White;
                
                // Apply light color tint
                var color = new Color(
                    (byte)(lightColor.R * lightFactor),
                    (byte)(lightColor.G * lightFactor),
                    (byte)(lightColor.B * lightFactor)
                );
                
                // Apply heat-based warm color filter
                if (world != null)
                {
                    float heatInfluence = world.GetHeatInfluence(TileStartX + x, TileStartY + y);
                    if (heatInfluence > 0.1f)
                    {
                        // Blend with warm tones (orange/red tint) for cozy feeling
                        Color warmTint = new Color(255, 200, 150);
                        color = Color.Lerp(color, warmTint, heatInfluence * 0.3f); // 30% max warm tint
                    }
                }
                
                // Apply ambient occlusion (soft shadows at corners and edges)
                float ao = CalculateAmbientOcclusion(x, y);
                color = Color.Lerp(color, Color.Black, ao * 0.15f); // Subtle darkening
                
                // Apply shadow under blocks (if there's air below)
                float shadow = CalculateBlockShadow(x, y);
                if (shadow > 0)
                {
                    color = Color.Lerp(color, Color.Black, shadow * 0.2f);
                }
                
                spriteBatch.Draw(TextureManager.TileAtlas, destRect, sourceRect, color);
                
                // Draw glow effect for emissive tiles
                int lightLevel = TileProperties.GetLightLevel(tile);
                if (lightLevel > 0)
                {
                    DrawGlowEffect(spriteBatch, destRect, tile, lightLevel, lightFactor);
                    
                    // Draw heat visualization for fire sources
                    if (tile == TileType.Furnace || tile == TileType.Lava)
                    {
                        DrawHeatEffect(spriteBatch, destRect, tile);
                    }
                }
            }
        }
    }
    
    private float CalculateAmbientOcclusion(int localX, int localY)
    {
        // Calculate soft shadows at corners and edges for depth
        float ao = 0f;
        
        // Check corners (diagonal neighbors)
        if (GetTileLocal(localX - 1, localY - 1) != TileType.Air) ao += 0.3f;
        if (GetTileLocal(localX + 1, localY - 1) != TileType.Air) ao += 0.3f;
        if (GetTileLocal(localX - 1, localY + 1) != TileType.Air) ao += 0.3f;
        if (GetTileLocal(localX + 1, localY + 1) != TileType.Air) ao += 0.3f;
        
        // Check edges (orthogonal neighbors)
        if (GetTileLocal(localX - 1, localY) != TileType.Air) ao += 0.15f;
        if (GetTileLocal(localX + 1, localY) != TileType.Air) ao += 0.15f;
        if (GetTileLocal(localX, localY - 1) != TileType.Air) ao += 0.15f;
        if (GetTileLocal(localX, localY + 1) != TileType.Air) ao += 0.15f;
        
        // Normalize and clamp
        ao = MathHelper.Clamp(ao, 0f, 1f);
        return ao;
    }
    
    private float CalculateBlockShadow(int localX, int localY)
    {
        // Calculate shadow under blocks (if there's air below)
        var currentTile = GetTileLocal(localX, localY);
        if (!TileProperties.IsSolid(currentTile))
            return 0f;
        
        // Check if there's air below
        var tileBelow = GetTileLocal(localX, localY + 1);
        if (tileBelow == TileType.Air)
        {
            // Shadow intensity based on light level
            byte light = GetLightLocal(localX, localY);
            float lightFactor = light / 255f;
            return (1f - lightFactor) * 0.5f; // Stronger shadow in darker areas
        }
        
        return 0f;
    }
    
    /// <summary>
    /// Save chunk data to byte array for serialization
    /// </summary>
    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        // Write header
        writer.Write(ChunkX);
        writer.Write(ChunkY);
        
        // Write tiles using RLE compression
        TileType currentTile = _tiles[0, 0];
        int count = 0;
        
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (_tiles[x, y] == currentTile && count < 255)
                {
                    count++;
                }
                else
                {
                    writer.Write((byte)currentTile);
                    writer.Write((byte)count);
                    currentTile = _tiles[x, y];
                    count = 1;
                }
            }
        }
        
        // Write last run
        writer.Write((byte)currentTile);
        writer.Write((byte)count);
        
        // Write walls (similar RLE)
        // ... (simplified for now)
        
        return ms.ToArray();
    }
    
    /// <summary>
    /// Load chunk data from byte array
    /// </summary>
    public void Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        // Read and verify header
        int chunkX = reader.ReadInt32();
        int chunkY = reader.ReadInt32();
        
        if (chunkX != ChunkX || chunkY != ChunkY)
            throw new InvalidDataException("Chunk position mismatch");
        
        // Read tiles
        int x = 0, y = 0;
        while (y < Size)
        {
            var tile = (TileType)reader.ReadByte();
            int count = reader.ReadByte();
            
            for (int i = 0; i < count && y < Size; i++)
            {
                _tiles[x, y] = tile;
                x++;
                if (x >= Size)
                {
                    x = 0;
                    y++;
                }
            }
        }
        
        IsModified = false;
        IsLightingDirty = true;
    }
    
    public void MarkClean()
    {
        IsModified = false;
    }
    
    public void Unload()
    {
        IsLoaded = false;
    }
}
