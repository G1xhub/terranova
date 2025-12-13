using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;

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
    
    /// <summary>
    /// Draw the chunk
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
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
                
                // Apply lighting
                byte light = _lightLevels[x, y];
                float lightFactor = light / 255f;
                var color = new Color(lightFactor, lightFactor, lightFactor);
                
                spriteBatch.Draw(TextureManager.TileAtlas, destRect, sourceRect, color);
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
