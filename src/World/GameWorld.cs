using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;

namespace TerraNova.World;

/// <summary>
/// Main world class - manages chunks, provides tile access, handles generation
/// </summary>
public class GameWorld
{
    // World dimensions in tiles
    public int Width { get; }
    public int Height { get; }
    
    // World dimensions in pixels
    public int PixelWidth => Width * GameConfig.TileSize;
    public int PixelHeight => Height * GameConfig.TileSize;
    
    // World dimensions in chunks
    public int ChunksX { get; }
    public int ChunksY { get; }
    
    // Chunk storage
    private readonly Chunk[,] _chunks;
    private readonly HashSet<Chunk> _loadedChunks = new();
    private readonly HashSet<Chunk> _dirtyChunks = new();
    
    // World seed
    public int Seed { get; }
    
    // Generation
    private WorldGenerator? _generator;
    
    // State
    public bool LightingDirty { get; set; } = true;
    public int LoadedChunkCount => _loadedChunks.Count;
    
    // Load distance in chunks
    private const int LoadDistance = 4;
    private const int UnloadDistance = 6;
    
    public GameWorld(int width, int height, int seed)
    {
        Width = width;
        Height = height;
        Seed = seed;
        
        ChunksX = (width + Chunk.Size - 1) / Chunk.Size;
        ChunksY = (height + Chunk.Size - 1) / Chunk.Size;
        
        _chunks = new Chunk[ChunksX, ChunksY];
        
        // Pre-create all chunks
        for (int cy = 0; cy < ChunksY; cy++)
        {
            for (int cx = 0; cx < ChunksX; cx++)
            {
                _chunks[cx, cy] = new Chunk(cx, cy);
            }
        }
    }
    
    /// <summary>
    /// Generate the entire world
    /// </summary>
    public void Generate()
    {
        _generator = new WorldGenerator(this, Seed);
        _generator.Generate();
        
        // Mark all chunks as dirty for lighting
        foreach (var chunk in _chunks)
        {
            chunk.IsLightingDirty = true;
            _loadedChunks.Add(chunk);
        }
        
        LightingDirty = true;
    }
    
    /// <summary>
    /// Get tile at world coordinates
    /// </summary>
    public TileType GetTile(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return TileType.Air;
        
        int chunkX = tileX / Chunk.Size;
        int chunkY = tileY / Chunk.Size;
        int localX = tileX % Chunk.Size;
        int localY = tileY % Chunk.Size;
        
        return _chunks[chunkX, chunkY].GetTileLocal(localX, localY);
    }
    
    /// <summary>
    /// Set tile at world coordinates
    /// </summary>
    public void SetTile(int tileX, int tileY, TileType type)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return;
        
        int chunkX = tileX / Chunk.Size;
        int chunkY = tileY / Chunk.Size;
        int localX = tileX % Chunk.Size;
        int localY = tileY % Chunk.Size;
        
        var chunk = _chunks[chunkX, chunkY];
        chunk.SetTileLocal(localX, localY, type);
        _dirtyChunks.Add(chunk);
        
        // Mark neighboring chunks as dirty for lighting
        MarkNeighborsDirty(chunkX, chunkY);
        LightingDirty = true;
    }
    
    /// <summary>
    /// Check if tile at position is solid
    /// </summary>
    public bool IsSolid(int tileX, int tileY)
    {
        return TileProperties.IsSolid(GetTile(tileX, tileY));
    }
    
    /// <summary>
    /// Check if tile at position is a platform
    /// </summary>
    public bool IsPlatform(int tileX, int tileY)
    {
        return TileProperties.IsPlatform(GetTile(tileX, tileY));
    }
    
    /// <summary>
    /// Check if tile at position is liquid
    /// </summary>
    public bool IsLiquid(int tileX, int tileY)
    {
        return TileProperties.IsLiquid(GetTile(tileX, tileY));
    }
    
    /// <summary>
    /// Get light level at world coordinates (0-255)
    /// </summary>
    public byte GetLight(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return 0;
        
        int chunkX = tileX / Chunk.Size;
        int chunkY = tileY / Chunk.Size;
        int localX = tileX % Chunk.Size;
        int localY = tileY % Chunk.Size;
        
        return _chunks[chunkX, chunkY].GetLightLocal(localX, localY);
    }
    
    /// <summary>
    /// Set light level at world coordinates
    /// </summary>
    public void SetLight(int tileX, int tileY, byte light)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return;
        
        int chunkX = tileX / Chunk.Size;
        int chunkY = tileY / Chunk.Size;
        int localX = tileX % Chunk.Size;
        int localY = tileY % Chunk.Size;
        
        _chunks[chunkX, chunkY].SetLightLocal(localX, localY, light);
    }
    
    /// <summary>
    /// Find a suitable spawn point on the surface
    /// </summary>
    public Vector2 FindSpawnPoint()
    {
        int centerX = Width / 2;
        
        // Search for surface at center
        for (int y = 0; y < Height; y++)
        {
            if (IsSolid(centerX, y))
            {
                // Found surface, spawn above it
                return new Vector2(
                    centerX * GameConfig.TileSize,
                    (y - 3) * GameConfig.TileSize
                );
            }
        }
        
        // Fallback
        return new Vector2(centerX * GameConfig.TileSize, 100 * GameConfig.TileSize);
    }
    
    /// <summary>
    /// Update chunk loading based on camera position
    /// </summary>
    public void UpdateChunkLoading(Vector2 cameraCenter)
    {
        int centerChunkX = (int)(cameraCenter.X / GameConfig.TileSize / Chunk.Size);
        int centerChunkY = (int)(cameraCenter.Y / GameConfig.TileSize / Chunk.Size);
        
        // Load nearby chunks
        for (int cy = centerChunkY - LoadDistance; cy <= centerChunkY + LoadDistance; cy++)
        {
            for (int cx = centerChunkX - LoadDistance; cx <= centerChunkX + LoadDistance; cx++)
            {
                if (cx >= 0 && cx < ChunksX && cy >= 0 && cy < ChunksY)
                {
                    var chunk = _chunks[cx, cy];
                    if (!chunk.IsLoaded)
                    {
                        LoadChunk(chunk);
                    }
                    _loadedChunks.Add(chunk);
                }
            }
        }
        
        // Unload far chunks
        var chunksToUnload = new List<Chunk>();
        foreach (var chunk in _loadedChunks)
        {
            int distX = Math.Abs(chunk.ChunkX - centerChunkX);
            int distY = Math.Abs(chunk.ChunkY - centerChunkY);
            
            if (distX > UnloadDistance || distY > UnloadDistance)
            {
                chunksToUnload.Add(chunk);
            }
        }
        
        foreach (var chunk in chunksToUnload)
        {
            UnloadChunk(chunk);
            _loadedChunks.Remove(chunk);
        }
    }
    
    private void LoadChunk(Chunk chunk)
    {
        // In a full implementation, this would load from disk
        // For now, chunks are always in memory
    }
    
    private void UnloadChunk(Chunk chunk)
    {
        if (chunk.IsModified)
        {
            // In a full implementation, save to disk here
        }
        chunk.Unload();
    }
    
    private void MarkNeighborsDirty(int chunkX, int chunkY)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = chunkX + dx;
                int cy = chunkY + dy;
                
                if (cx >= 0 && cx < ChunksX && cy >= 0 && cy < ChunksY)
                {
                    _chunks[cx, cy].IsLightingDirty = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Get chunk at chunk coordinates
    /// </summary>
    public Chunk? GetChunk(int chunkX, int chunkY)
    {
        if (chunkX < 0 || chunkX >= ChunksX || chunkY < 0 || chunkY >= ChunksY)
            return null;
        return _chunks[chunkX, chunkY];
    }
    
    /// <summary>
    /// Get chunk containing world tile position
    /// </summary>
    public Chunk? GetChunkAt(int tileX, int tileY)
    {
        return GetChunk(tileX / Chunk.Size, tileY / Chunk.Size);
    }
    
    /// <summary>
    /// Draw visible chunks
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        // Calculate visible chunk range
        var visible = camera.VisibleArea;
        int startChunkX = Math.Max(0, visible.Left / GameConfig.TileSize / Chunk.Size);
        int startChunkY = Math.Max(0, visible.Top / GameConfig.TileSize / Chunk.Size);
        int endChunkX = Math.Min(ChunksX - 1, visible.Right / GameConfig.TileSize / Chunk.Size);
        int endChunkY = Math.Min(ChunksY - 1, visible.Bottom / GameConfig.TileSize / Chunk.Size);
        
        // #region agent log
        TerraNovaGame.AgentLog("GameWorld.Draw", "visible-range", new
        {
            visibleLeft = visible.Left,
            visibleTop = visible.Top,
            visibleRight = visible.Right,
            visibleBottom = visible.Bottom,
            startChunkX,
            startChunkY,
            endChunkX,
            endChunkY
        }, "H3-camera-visible");
        // #endregion
        
        // Draw chunks back to front
        for (int cy = startChunkY; cy <= endChunkY; cy++)
        {
            for (int cx = startChunkX; cx <= endChunkX; cx++)
            {
                _chunks[cx, cy].Draw(spriteBatch, camera);
            }
        }
    }
    
    /// <summary>
    /// Perform a raycast through the world
    /// </summary>
    public bool Raycast(Vector2 start, Vector2 direction, float maxDistance, out Vector2 hitPoint, out TileType hitTile)
    {
        hitPoint = start;
        hitTile = TileType.Air;
        
        direction.Normalize();
        
        float distance = 0;
        float stepSize = GameConfig.TileSize / 4f; // Quarter tile precision
        
        while (distance < maxDistance)
        {
            var checkPos = start + direction * distance;
            int tileX = (int)(checkPos.X / GameConfig.TileSize);
            int tileY = (int)(checkPos.Y / GameConfig.TileSize);
            
            var tile = GetTile(tileX, tileY);
            if (TileProperties.IsSolid(tile))
            {
                hitPoint = checkPos;
                hitTile = tile;
                return true;
            }
            
            distance += stepSize;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check collision between a rectangle and solid tiles
    /// </summary>
    public bool CheckCollision(Rectangle bounds)
    {
        int startX = bounds.Left / GameConfig.TileSize;
        int startY = bounds.Top / GameConfig.TileSize;
        int endX = bounds.Right / GameConfig.TileSize;
        int endY = bounds.Bottom / GameConfig.TileSize;
        
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (IsSolid(x, y))
                    return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Get all solid tile rectangles intersecting with bounds
    /// </summary>
    public IEnumerable<Rectangle> GetCollidingTiles(Rectangle bounds)
    {
        int startX = bounds.Left / GameConfig.TileSize;
        int startY = bounds.Top / GameConfig.TileSize;
        int endX = bounds.Right / GameConfig.TileSize;
        int endY = bounds.Bottom / GameConfig.TileSize;
        
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (IsSolid(x, y))
                {
                    yield return new Rectangle(
                        x * GameConfig.TileSize,
                        y * GameConfig.TileSize,
                        GameConfig.TileSize,
                        GameConfig.TileSize
                    );
                }
            }
        }
    }
}
