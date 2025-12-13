using TerraNova.Core;

namespace TerraNova.World;

/// <summary>
/// Procedural world generator using multiple noise layers
/// </summary>
public class WorldGenerator
{
    private readonly GameWorld _world;
    private readonly int _seed;
    
    // Noise generators for different features
    private readonly SimplexNoise _terrainNoise;
    private readonly SimplexNoise _caveNoise;
    private readonly SimplexNoise _oreNoise;
    private readonly SimplexNoise _biomeNoise;
    private readonly SimplexNoise _detailNoise;
    
    // Pre-calculated surface heights
    private int[]? _surfaceHeights;
    private BiomeType[]? _biomes;
    
    // Configuration from GameConfig
    private readonly int _surfaceLevel;
    private readonly int _undergroundLevel;
    private readonly int _cavernLevel;
    private readonly int _underworldLevel;
    
    public WorldGenerator(GameWorld world, int seed)
    {
        _world = world;
        _seed = seed;
        
        // Initialize noise generators with different seeds
        _terrainNoise = new SimplexNoise(seed);
        _caveNoise = new SimplexNoise(seed + 1);
        _oreNoise = new SimplexNoise(seed + 2);
        _biomeNoise = new SimplexNoise(seed + 3);
        _detailNoise = new SimplexNoise(seed + 4);
        
        // Get level configurations
        var config = TerraNovaGame.Config;
        _surfaceLevel = config.SurfaceLevel;
        _undergroundLevel = config.UndergroundLevel;
        _cavernLevel = config.CavernLevel;
        _underworldLevel = config.UnderworldLevel;
    }
    
    /// <summary>
    /// Generate the entire world
    /// </summary>
    public void Generate()
    {
        Console.WriteLine($"Generating world {_world.Width}x{_world.Height} with seed {_seed}...");
        TerraNovaGame.AgentLog("WorldGenerator.Generate", "start", new
        {
            _world.Width,
            _world.Height,
            _seed
        }, "H1-world-empty");
        
        // Phase 1: Generate heightmap and biomes
        GenerateHeightmapAndBiomes();
        
        // Phase 2: Fill terrain
        FillTerrain();
        
        // Phase 3: Generate caves
        GenerateCaves();
        
        // Phase 4: Place ores
        PlaceOres();
        
        // Phase 5: Generate structures (trees, buildings, etc.)
        GenerateStructures();
        
        // Phase 6: Place liquids
        PlaceLiquids();
        
        // Phase 7: Final pass (cleanup, validation)
        FinalPass();
        
        // Store biome data in world
        if (_biomes != null)
        {
            _world.SetBiomes(_biomes);
        }
        
        Console.WriteLine("World generation complete!");
    }
    
    private void GenerateHeightmapAndBiomes()
    {
        Console.WriteLine("  Generating heightmap and biomes...");
        
        _surfaceHeights = new int[_world.Width];
        _biomes = new BiomeType[_world.Width];
        
        for (int x = 0; x < _world.Width; x++)
        {
            // Multi-octave terrain height - reduced for flatter surface
            double height = 0;
            height += _terrainNoise.Noise2D(x * 0.003, 0) * 30;   // Large hills (reduced from 80)
            height += _terrainNoise.Noise2D(x * 0.01, 0) * 15;    // Medium variation (reduced from 30)
            height += _terrainNoise.Noise2D(x * 0.05, 0) * 5;      // Small bumps (reduced from 10)
            height += _detailNoise.Noise2D(x * 0.1, 0) * 2;       // Micro detail (reduced from 4)
            
            _surfaceHeights[x] = _surfaceLevel + (int)height;
            
            // Determine biome
            double biomeValue = _biomeNoise.Noise2D(x * 0.002, 0);
            double biomeValue2 = _biomeNoise.Noise2D(x * 0.008, 100);
            
            if (biomeValue < -0.4)
                _biomes[x] = BiomeType.Snow;
            else if (biomeValue > 0.4)
                _biomes[x] = BiomeType.Desert;
            else if (biomeValue2 > 0.5 && biomeValue > -0.1)
                _biomes[x] = BiomeType.Jungle;
            else
                _biomes[x] = BiomeType.Forest;
        }
    }
    
    private void FillTerrain()
    {
        Console.WriteLine("  Filling terrain...");
        
        for (int x = 0; x < _world.Width; x++)
        {
            int surface = _surfaceHeights![x];
            var biome = _biomes![x];
            
            for (int y = 0; y < _world.Height; y++)
            {
                TileType tile;
                
                // Bedrock at bottom
                if (y >= _world.Height - 5)
                {
                    tile = y >= _world.Height - 2 ? TileType.Bedrock : 
                           (Random.Shared.NextDouble() < 0.7 ? TileType.Bedrock : TileType.Obsidian);
                }
                // Above surface = air
                else if (y < surface)
                {
                    tile = TileType.Air;
                }
                // Surface layer
                else if (y == surface)
                {
                    tile = GetSurfaceTile(biome);
                }
                // Subsurface layers
                else if (y < surface + GetDirtDepth(x, surface))
                {
                    tile = GetSubsurfaceTile(biome);
                }
                // Underground/Cavern - stone
                else
                {
                    tile = TileType.Stone;
                }
                
                _world.SetTile(x, y, tile);
            }
        }
    }
    
    private int GetDirtDepth(int x, int surface)
    {
        // Variable dirt layer depth
        return 4 + (int)(_detailNoise.Noise2D(x * 0.1, surface * 0.1) * 4);
    }
    
    private TileType GetSurfaceTile(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Snow => TileType.Snow,
            BiomeType.Desert => TileType.Sand,
            BiomeType.Jungle => TileType.JungleGrass,
            _ => TileType.Grass
        };
    }
    
    private TileType GetSubsurfaceTile(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Snow => TileType.Snow,
            BiomeType.Desert => TileType.Sand,
            BiomeType.Jungle => TileType.Mud,
            _ => TileType.Dirt
        };
    }
    
    private void GenerateCaves()
    {
        Console.WriteLine("  Generating caves...");
        
        for (int x = 0; x < _world.Width; x++)
        {
            int surface = _surfaceHeights![x];
            
            for (int y = surface + 15; y < _world.Height - 10; y++)
            {
                // Multi-layered cave noise for organic shapes
                double cave1 = _caveNoise.Noise2D(x * 0.03, y * 0.03);
                double cave2 = _caveNoise.Noise2D(x * 0.06, y * 0.06) * 0.5;
                double cave3 = _caveNoise.Noise2D(x * 0.12, y * 0.12) * 0.25;
                
                double caveValue = cave1 + cave2 + cave3;
                
                // Caves get larger and more common deeper down
                int depth = y - surface;
                double depthFactor = Math.Min(1.0, depth / 200.0);
                double threshold = 0.4 - depthFactor * 0.15;
                
                // Worm-like horizontal tunnels
                double worm = _caveNoise.Noise2D(x * 0.02, y * 0.005);
                if (Math.Abs(worm) < 0.05 && depth > 30)
                {
                    _world.SetTile(x, y, TileType.Air);
                    continue;
                }
                
                if (caveValue > threshold)
                {
                    _world.SetTile(x, y, TileType.Air);
                }
            }
        }
    }
    
    private void PlaceOres()
    {
        Console.WriteLine("  Placing ores...");
        
        var oreConfigs = new[]
        {
            (TileType.Coal, 20, 0.02, 0.65, 6),        // Coal: shallow, common, large veins
            (TileType.CopperOre, 40, 0.025, 0.70, 5),  // Copper: medium depth
            (TileType.IronOre, 80, 0.02, 0.75, 4),     // Iron: deeper
            (TileType.GoldOre, 150, 0.015, 0.82, 3),   // Gold: deep, rare
            (TileType.DiamondOre, 250, 0.01, 0.90, 2), // Diamond: very deep, very rare
        };
        
        foreach (var (oreType, minDepth, scale, threshold, veinSize) in oreConfigs)
        {
            PlaceOreType(oreType, minDepth, scale, threshold, veinSize);
        }
    }
    
    private void PlaceOreType(TileType oreType, int minDepth, double scale, double threshold, int maxVeinSize)
    {
        for (int x = 0; x < _world.Width; x++)
        {
            int surface = _surfaceHeights![x];
            
            for (int y = surface + minDepth; y < _world.Height - 10; y++)
            {
                var tile = _world.GetTile(x, y);
                if (tile != TileType.Stone) continue;
                
                double oreValue = _oreNoise.Noise2D(x * scale + (int)oreType * 100, y * scale);
                
                if (oreValue > threshold)
                {
                    // Place ore vein
                    PlaceVein(x, y, oreType, maxVeinSize);
                }
            }
        }
    }
    
    private void PlaceVein(int startX, int startY, TileType oreType, int maxSize)
    {
        int placed = 0;
        var queue = new Queue<(int x, int y)>();
        var visited = new HashSet<(int, int)>();
        
        queue.Enqueue((startX, startY));
        
        while (queue.Count > 0 && placed < maxSize)
        {
            var (x, y) = queue.Dequeue();
            
            if (visited.Contains((x, y))) continue;
            visited.Add((x, y));
            
            if (_world.GetTile(x, y) != TileType.Stone) continue;
            
            _world.SetTile(x, y, oreType);
            placed++;
            
            // Spread to neighbors with decreasing probability
            var neighbors = new[] { (x-1, y), (x+1, y), (x, y-1), (x, y+1) };
            foreach (var neighbor in neighbors)
            {
                if (Random.Shared.NextDouble() < 0.6)
                    queue.Enqueue(neighbor);
            }
        }
    }
    
    private void GenerateStructures()
    {
        Console.WriteLine("  Generating structures...");
        
        // Generate trees
        GenerateTrees();
        
        // Generate underground cabins
        GenerateCabins();
        
        // Generate surface structures (TODO)
        // GenerateSurfaceStructures();
    }
    
    private void GenerateTrees()
    {
        for (int x = 5; x < _world.Width - 5; x++)
        {
            var biome = _biomes![x];
            if (biome == BiomeType.Desert) continue; // No trees in desert
            
            // Tree spawn chance based on biome
            double treeChance = biome switch
            {
                BiomeType.Forest => 0.06,
                BiomeType.Jungle => 0.10,
                BiomeType.Snow => 0.03,
                _ => 0.04
            };
            
            if (Random.Shared.NextDouble() > treeChance) continue;
            
            int surface = _surfaceHeights![x];
            
            // Make sure surface is grass/jungle grass
            var surfaceTile = _world.GetTile(x, surface);
            if (surfaceTile != TileType.Grass && surfaceTile != TileType.JungleGrass && surfaceTile != TileType.Snow)
                continue;
            
            // Generate tree
            int treeHeight = biome == BiomeType.Jungle ? 
                8 + Random.Shared.Next(6) : 
                5 + Random.Shared.Next(4);
            
            // Trunk
            for (int h = 1; h <= treeHeight; h++)
            {
                int y = surface - h;
                if (y >= 0)
                    _world.SetTile(x, y, TileType.Wood);
            }
            
            // Leaves (canopy)
            int leafY = surface - treeHeight;
            int leafRadius = biome == BiomeType.Jungle ? 3 : 2;
            
            for (int dy = -leafRadius; dy <= 1; dy++)
            {
                for (int dx = -leafRadius; dx <= leafRadius; dx++)
                {
                    // Skip corners for rounded shape
                    if (Math.Abs(dx) == leafRadius && Math.Abs(dy) == leafRadius) continue;
                    if (Math.Abs(dx) == leafRadius && dy == 1) continue;
                    
                    int lx = x + dx;
                    int ly = leafY + dy;
                    
                    if (lx >= 0 && lx < _world.Width && ly >= 0)
                    {
                        if (_world.GetTile(lx, ly) == TileType.Air)
                            _world.SetTile(lx, ly, TileType.Leaves);
                    }
                }
            }
            
            // Skip some space to avoid overlapping trees
            x += leafRadius + 2;
        }
    }
    
    private void GenerateCabins()
    {
        // Underground wooden cabins with chests
        int cabinCount = _world.Width / 200;
        
        for (int i = 0; i < cabinCount; i++)
        {
            int x = Random.Shared.Next(50, _world.Width - 50);
            int y = _surfaceHeights![x] + 50 + Random.Shared.Next(100);
            
            if (y >= _world.Height - 20) continue;
            
            // Simple cabin: 8x5 room
            int width = 8;
            int height = 5;
            
            // Clear interior
            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    _world.SetTile(x + dx, y + dy, TileType.Air);
                }
            }
            
            // Floor and ceiling
            for (int dx = -1; dx <= width; dx++)
            {
                _world.SetTile(x + dx, y - 1, TileType.Wood);
                _world.SetTile(x + dx, y + height, TileType.Wood);
            }
            
            // Walls
            for (int dy = 0; dy < height; dy++)
            {
                _world.SetTile(x - 1, y + dy, TileType.Wood);
                _world.SetTile(x + width, y + dy, TileType.Wood);
            }
            
            // Place chest
            _world.SetTile(x + width / 2, y + height - 1, TileType.Chest);
            
            // Place torch
            _world.SetTile(x + 2, y + 1, TileType.Torch);
        }
    }
    
    private void PlaceLiquids()
    {
        Console.WriteLine("  Placing liquids...");
        
        // Surface water pools
        for (int x = 1; x < _world.Width - 1; x++)
        {
            int h = _surfaceHeights![x];
            int hLeft = _surfaceHeights[x - 1];
            int hRight = _surfaceHeights[x + 1];
            
            // Find depressions
            if (h > hLeft && h > hRight)
            {
                if (Random.Shared.NextDouble() < 0.08) // 8% chance
                {
                    int waterLevel = Math.Max(hLeft, hRight);
                    
                    // Fill with water
                    for (int wx = x - 4; wx <= x + 4; wx++)
                    {
                        if (wx < 0 || wx >= _world.Width) continue;
                        
                        for (int wy = waterLevel; wy <= h + 3; wy++)
                        {
                            var tile = _world.GetTile(wx, wy);
                            if (tile == TileType.Air || tile == TileType.Dirt)
                                _world.SetTile(wx, wy, TileType.Water);
                        }
                    }
                }
            }
        }
        
        // Underground water/lava pools
        for (int x = 10; x < _world.Width - 10; x += 20)
        {
            for (int y = _undergroundLevel; y < _world.Height - 50; y += 20)
            {
                double poolNoise = _detailNoise.Noise2D(x * 0.05, y * 0.05);
                
                if (poolNoise > 0.6)
                {
                    // Water in upper areas, lava in lower
                    TileType liquid = y > _cavernLevel ? TileType.Lava : TileType.Water;
                    
                    // Fill a small pool
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            int px = x + dx;
                            int py = y + dy;
                            
                            if (_world.GetTile(px, py) == TileType.Air)
                                _world.SetTile(px, py, liquid);
                        }
                    }
                }
            }
        }
    }
    
    private void FinalPass()
    {
        Console.WriteLine("  Final pass...");
        
        // Convert exposed dirt to grass
        for (int x = 0; x < _world.Width; x++)
        {
            for (int y = 0; y < _world.Height - 1; y++)
            {
                if (_world.GetTile(x, y) == TileType.Air && _world.GetTile(x, y + 1) == TileType.Dirt)
                {
                    _world.SetTile(x, y + 1, TileType.Grass);
                }
            }
        }
        
        // Add world details: grass patches, flowers, stones, tile variations
        AddWorldDetails();
    }
    
    private void AddWorldDetails()
    {
        Console.WriteLine("  Adding world details...");
        
        for (int x = 0; x < _world.Width; x++)
        {
            int surface = _surfaceHeights![x];
            var biome = _biomes![x];
            
            // Only add details on surface
            if (surface < 0 || surface >= _world.Height) continue;
            
            var surfaceTile = _world.GetTile(x, surface);
            
            // Add details to grass surfaces
            if (surfaceTile == TileType.Grass || surfaceTile == TileType.JungleGrass)
            {
                // Grass patches - denser grass in some areas
                double grassDetail = _detailNoise.Noise2D(x * 0.2, surface * 0.2);
                if (grassDetail > 0.6 && Random.Shared.NextDouble() < 0.3)
                {
                    // Create a small grass patch (visual variation handled in rendering)
                    // The grass tile itself will have variation based on position
                }
                
                // Flowers - random placement on grass
                if (biome == BiomeType.Forest || biome == BiomeType.Jungle)
                {
                    double flowerNoise = _detailNoise.Noise2D(x * 0.15, surface * 0.15 + 500);
                    if (flowerNoise > 0.7 && Random.Shared.NextDouble() < 0.05)
                    {
                        // Place flower above grass (in air)
                        if (surface > 0 && _world.GetTile(x, surface - 1) == TileType.Air)
                        {
                            // Use a decorative approach: store flower data in a separate system
                            // For now, we'll use visual variation in the grass tile rendering
                        }
                    }
                }
                
                // Small stones on surface
                double stoneNoise = _detailNoise.Noise2D(x * 0.25, surface * 0.25 + 1000);
                if (stoneNoise > 0.75 && Random.Shared.NextDouble() < 0.02)
                {
                    // Place small stone (visual variation in grass tile)
                    // Similar to flowers, handled via rendering variation
                }
            }
            
            // Add variation to stone tiles underground
            for (int y = surface + 20; y < Math.Min(surface + 100, _world.Height - 1); y++)
            {
                var tile = _world.GetTile(x, y);
                if (tile == TileType.Stone)
                {
                    // Add visual variation based on position
                    // This will be handled in the texture generation
                }
            }
        }
    }
    
    private void GenerateUndergroundBiomes()
    {
        Console.WriteLine("  Generating underground biomes...");
        
        var undergroundBiomes = new BiomeType[_world.Width * _world.Height];
        
        for (int x = 0; x < _world.Width; x++)
        {
            int surface = _surfaceHeights![x];
            
            for (int y = surface + 20; y < _world.Height - 10; y++)
            {
                int depth = y - surface;
                var tile = _world.GetTile(x, y);
                
                // Only assign biomes to air tiles (caves)
                if (tile != TileType.Air) continue;
                
                // Use noise to determine biome type
                double biomeNoise = _biomeNoise.Noise2D(x * 0.01, y * 0.01);
                double crystalNoise = _detailNoise.Noise2D(x * 0.05, y * 0.05);
                double mushroomNoise = _detailNoise.Noise2D(x * 0.08, y * 0.08 + 1000);
                
                BiomeType biome = BiomeType.Cave; // Default
                
                // Crystal caves - deeper, rare
                if (depth > 150 && crystalNoise > 0.7)
                {
                    biome = BiomeType.CrystalCave;
                }
                // Mushroom caves - medium depth, common
                else if (depth > 80 && depth < 200 && mushroomNoise > 0.6)
                {
                    biome = BiomeType.MushroomCave;
                }
                // Deep caves - very deep
                else if (depth > 250)
                {
                    biome = BiomeType.DeepCave;
                }
                // Regular caves
                else if (depth > 30)
                {
                    biome = BiomeType.Cave;
                }
                
                int index = y * _world.Width + x;
                if (index >= 0 && index < undergroundBiomes.Length)
                {
                    undergroundBiomes[index] = biome;
                }
            }
        }
        
        _world.SetUndergroundBiomes(undergroundBiomes);
    }
    
    private void PlaceCrystalsAndMushrooms()
    {
        Console.WriteLine("  Placing crystals and mushrooms...");
        
        for (int x = 0; x < _world.Width; x++)
        {
            int surface = _surfaceHeights![x];
            
            for (int y = surface + 20; y < _world.Height - 10; y++)
            {
                var tile = _world.GetTile(x, y);
                if (tile != TileType.Air) continue;
                
                var biome = _world.GetBiomeAt(x, y);
                int depth = y - surface;
                
                // Place crystals in crystal caves
                if (biome == BiomeType.CrystalCave)
                {
                    double crystalChance = _detailNoise.Noise2D(x * 0.1, y * 0.1);
                    
                    // Check if there's a solid surface below
                    if (_world.GetTile(x, y + 1) != TileType.Air && crystalChance > 0.75)
                    {
                        // Random crystal type
                        double crystalType = _detailNoise.Noise2D(x * 0.2, y * 0.2);
                        TileType crystalTile = crystalType < -0.3 ? TileType.BlueCrystal :
                                               crystalType > 0.3 ? TileType.RedCrystal :
                                               TileType.Crystal;
                        
                        _world.SetTile(x, y, crystalTile);
                    }
                }
                
                // Place mushrooms in mushroom caves
                if (biome == BiomeType.MushroomCave)
                {
                    double mushroomChance = _detailNoise.Noise2D(x * 0.15, y * 0.15);
                    
                    // Check if there's a solid surface below
                    if (_world.GetTile(x, y + 1) != TileType.Air && mushroomChance > 0.7)
                    {
                        // 30% chance for glowing mushroom
                        TileType mushroomTile = mushroomChance > 0.85 ? TileType.GlowingMushroom : TileType.Mushroom;
                        _world.SetTile(x, y, mushroomTile);
                    }
                }
                
                // Also place some crystals in deep caves
                if (biome == BiomeType.DeepCave && depth > 300)
                {
                    double crystalChance = _detailNoise.Noise2D(x * 0.12, y * 0.12);
                    if (_world.GetTile(x, y + 1) != TileType.Air && crystalChance > 0.8)
                    {
                        _world.SetTile(x, y, TileType.Crystal);
                    }
                }
            }
        }
    }
}

public enum BiomeType
{
    Forest,
    Desert,
    Snow,
    Jungle,
    Corruption, // Evil biome
    Crimson,    // Alternative evil biome
    Hallow,      // Post-hardmode biome
    
    // Underground biomes
    Cave,
    CrystalCave,
    MushroomCave,
    DeepCave
}
