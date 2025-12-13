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
    
    // Cached rendering data
    private readonly float[,] _aoCache; // Ambient occlusion cache
    private readonly float[,] _shadowCache; // Shadow cache
    private bool _shadowCacheDirty = true;
    
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
        _aoCache = new float[Size, Size];
        _shadowCache = new float[Size, Size];
        
        // Initialize light levels to bright by default (will be updated by lighting system)
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                _lightLevels[x, y] = 255;
                _aoCache[x, y] = -1f; // -1 means not calculated yet
                _shadowCache[x, y] = -1f;
            }
        
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
            
            // Invalidate AO and shadow cache for this tile and neighbors
            InvalidateCache(localX, localY);
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
        
        // Optimized: Single draw call with combined glow effect (blend of outer and inner)
        int glowSize = GameConfig.TileSize + 6; // Average size between outer and inner
        int glowOffset = (glowSize - GameConfig.TileSize) / 2;
        var glowRect = new Rectangle(
            destRect.X - glowOffset,
            destRect.Y - glowOffset,
            glowSize,
            glowSize
        );
        // Combined color: blend of outer (0.3) and inner (0.6) intensities
        var combinedGlowColor = glowColor * glowIntensity * 0.45f; // Average of 0.3 and 0.6
        spriteBatch.Draw(TextureManager.Pixel, glowRect, combinedGlowColor);
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
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, GameTime? gameTime = null, LightingSystem? lightingSystem = null, GameWorld? world = null)
    {
        // Early out if chunk is not visible
        if (!camera.IsVisible(Bounds))
            return;
        
        var visibleArea = camera.VisibleArea;
        
        // Recalculate cache if lighting changed
        if (IsLightingDirty)
        {
            _shadowCacheDirty = true;
            // Mark all shadow cache as invalid
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    _shadowCache[x, y] = -1f;
        }
        
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
                
                // Calculate animation frame for animated tiles
                int frameIndex = 0;
                if (TileProperties.IsAnimated(tile) && gameTime != null)
                {
                    frameIndex = GetAnimatedFrameIndex(tile, worldX, worldY, gameTime);
                }
                
                var sourceRect = TextureManager.GetTileRect(tile, frameIndex);
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
                    
                    // Apply biome-based color tint for underground biomes
                    var biome = world.GetBiomeAt(TileStartX + x, TileStartY + y);
                    if (biome == BiomeType.CrystalCave)
                    {
                        // Blue-purple crystal glow
                        Color crystalTint = new Color(100, 120, 200);
                        color = Color.Lerp(color, crystalTint, 0.15f);
                    }
                    else if (biome == BiomeType.MushroomCave)
                    {
                        // Purple-red mushroom glow
                        Color mushroomTint = new Color(150, 80, 120);
                        color = Color.Lerp(color, mushroomTint, 0.12f);
                    }
                    else if (biome == BiomeType.DeepCave)
                    {
                        // Very dark, desaturated
                        color = Color.Lerp(color, Color.Black, 0.2f);
                    }
                }
                
                // Apply ambient occlusion (soft shadows at corners and edges) - use cache
                if (_aoCache[x, y] < 0)
                {
                    _aoCache[x, y] = CalculateAmbientOcclusion(x, y);
                }
                float ao = _aoCache[x, y];
                color = Color.Lerp(color, Color.Black, ao * 0.25f); // Stronger darkening (was 0.15f)
                
                // Apply shadow under blocks (if there's air below) - use cache
                if (_shadowCacheDirty || _shadowCache[x, y] < 0)
                {
                    _shadowCache[x, y] = CalculateBlockShadow(x, y);
                }
                float shadow = _shadowCache[x, y];
                if (shadow > 0)
                {
                    // Gradient-based shadow for softer edges
                    float shadowIntensity = shadow * 0.35f; // Stronger shadow (was 0.2f)
                    color = Color.Lerp(color, Color.Black, shadowIntensity);
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
    
    private int GetAnimatedFrameIndex(TileType tile, int worldX, int worldY, GameTime gameTime)
    {
        // Calculate frame index based on time and position for variation
        float totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        int tileHash = (worldX + worldY * 1000) % 1000; // Vary animation per tile position
        
        switch (tile)
        {
            case TileType.Water:
                // 4 frames for wave animation
                float waterSpeed = 2.0f; // Animation speed
                int waterFrames = 4;
                return ((int)(totalSeconds * waterSpeed) + tileHash / 100) % waterFrames;
                
            case TileType.Lava:
                // 4 frames for bubbling animation
                float lavaSpeed = 1.5f;
                int lavaFrames = 4;
                return ((int)(totalSeconds * lavaSpeed) + tileHash / 100) % lavaFrames;
                
            case TileType.Grass:
                // 2 frames for gentle swaying
                float grassSpeed = 1.0f;
                int grassFrames = 2;
                return ((int)(totalSeconds * grassSpeed) + tileHash / 200) % grassFrames;
                
            case TileType.Leaves:
                // 2 frames for wind movement
                float leavesSpeed = 1.2f;
                int leavesFrames = 2;
                return ((int)(totalSeconds * leavesSpeed) + tileHash / 200) % leavesFrames;
                
            default:
                return 0;
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
            
            // Base shadow strength - stronger in darker areas
            float baseShadow = (1f - lightFactor) * 0.6f; // Increased from 0.5f
            
            // Check neighbors for gradient effect (softer shadow edges)
            float neighborShadow = 0f;
            int neighborCount = 0;
            
            // Check left and right neighbors
            var leftTile = GetTileLocal(localX - 1, localY);
            var rightTile = GetTileLocal(localX + 1, localY);
            
            if (TileProperties.IsSolid(leftTile))
            {
                byte leftLight = GetLightLocal(localX - 1, localY);
                neighborShadow += (1f - leftLight / 255f) * 0.3f;
                neighborCount++;
            }
            if (TileProperties.IsSolid(rightTile))
            {
                byte rightLight = GetLightLocal(localX + 1, localY);
                neighborShadow += (1f - rightLight / 255f) * 0.3f;
                neighborCount++;
            }
            
            // Average neighbor shadow for gradient
            if (neighborCount > 0)
            {
                neighborShadow /= neighborCount;
            }
            
            // Blend base shadow with neighbor shadow for softer gradient
            return MathHelper.Clamp(baseShadow + neighborShadow * 0.3f, 0f, 1f);
        }
        
        return 0f;
    }
    
    private void InvalidateCache(int localX, int localY)
    {
        // Invalidate cache for this tile and all neighbors (AO depends on neighbors)
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = localX + dx;
                int y = localY + dy;
                if (x >= 0 && x < Size && y >= 0 && y < Size)
                {
                    _aoCache[x, y] = -1f;
                    _shadowCache[x, y] = -1f;
                }
            }
        }
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
