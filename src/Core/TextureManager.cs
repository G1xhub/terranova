using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.World;
using TerraNova.Entities;
using System;
using System.IO;

namespace TerraNova.Core;

/// <summary>
/// Manages all game textures with detailed Terraria-style procedural generation
/// </summary>
public static class TextureManager
{
    private static GraphicsDevice _graphicsDevice = null!;
    private static ContentManager _content = null!;
    private static Random _random = new Random(12345); // Consistent seed for textures
    
    // Paths
    private const string GeneratedDir = "Content/Generated";
    private const string TileAtlasFile = "tile_atlas.png";
    private const string PlayerFile = "player.png";
    private const string PlayerSpriteSheetFile = "player_spritesheet.png";
    private const string ParticleFile = "particle.png";
    private const string ParallaxFileFormat = "parallax_{0}.png";
    private const string UISlotFile = "ui_slot.png";
    private const string UISelectedSlotFile = "ui_slot_selected.png";
    private const string UIHotbarFile = "ui_hotbar.png";
    
    // Main textures
    public static Texture2D TileAtlas { get; private set; } = null!;
    public static Texture2D PlayerSprite { get; private set; } = null!;
    public static Texture2D PlayerSpriteSheet { get; private set; } = null!;
    public static Texture2D ParticleTexture { get; private set; } = null!;
    public static Texture2D Pixel { get; private set; } = null!;
    public static IReadOnlyList<Texture2D> ParallaxLayers => _parallaxLayers;
    public static Texture2D UISlot { get; private set; } = null!;
    public static Texture2D UISelectedSlot { get; private set; } = null!;
    public static Texture2D UIHotbar { get; private set; } = null!;
    public static IReadOnlyDictionary<ItemType, Texture2D> ItemIcons => _itemIcons;
    
    // #region agent log helper
    private static readonly object _agentLogLock = new();
    private const string AgentLogPath = ".cursor/debug.log";
    private const string AgentSession = "debug-session";
    private const string AgentRun = "run-pre-fix";
    
    private static void AgentLog(string location, string message, object data, string hypothesisId)
    {
        try
        {
            var payload = new
            {
                sessionId = AgentSession,
                runId = AgentRun,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            var line = System.Text.Json.JsonSerializer.Serialize(payload) + "\n";
            lock (_agentLogLock)
            {
                File.AppendAllText(AgentLogPath, line);
            }
        }
        catch
        {
            // ignore
        }
    }
    // #endregion
    
    private static readonly List<Texture2D> _parallaxLayers = new();
    private static readonly Dictionary<ItemType, Texture2D> _itemIcons = new();
    
    // Tile size
    private const int TileSize = 16;
    private const int AtlasColumns = 16;
    private const int AtlasRows = 16;
    
    private static Dictionary<TileType, Rectangle> _tileRects = new();
    
    public static void Initialize(GraphicsDevice graphicsDevice, ContentManager content)
    {
        _graphicsDevice = graphicsDevice;
        _content = content;
        
        Directory.CreateDirectory(GeneratedDir);
        
        // Create pixel texture
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });
        
        // Build tile rect lookup independent of texture data
        BuildTileRects();
        
        // Load or generate assets
        TileAtlas = LoadOrGenerate(TileAtlasFile, GenerateDetailedTileAtlas);
        PlayerSprite = LoadOrGenerate(PlayerFile, GeneratePlayerSprite);
        PlayerSpriteSheet = LoadOrGenerate(PlayerSpriteSheetFile, GeneratePlayerSpriteSheet);
        ParticleTexture = LoadOrGenerate(ParticleFile, GenerateParticleTexture);
        LoadParallaxLayers();
        UISlot = LoadOrGenerate(UISlotFile, () => GenerateUISlot());
        UISelectedSlot = LoadOrGenerate(UISelectedSlotFile, () => GenerateUISlot(selected: true));
        UIHotbar = LoadOrGenerate(UIHotbarFile, GenerateUIHotbar);
        
        GenerateItemIcons();
        
        AgentLog("TextureManager.Initialize", "textures-loaded", new
        {
            parallaxCount = _parallaxLayers.Count,
            iconCount = _itemIcons.Count
        }, "H4-texture-load");
    }
    
    private static Texture2D LoadOrGenerate(string fileName, Func<Texture2D> generator)
    {
        var path = Path.Combine(GeneratedDir, fileName);
        
        if (File.Exists(path))
        {
            using var fs = File.OpenRead(path);
            return Texture2D.FromStream(_graphicsDevice, fs);
        }
        
        var tex = generator();
        SaveTexture(tex, path);
        return tex;
    }
    
    private static void SaveTexture(Texture2D texture, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
        texture.SaveAsPng(fs, texture.Width, texture.Height);
    }
    
    private static void BuildTileRects()
    {
        _tileRects = new Dictionary<TileType, Rectangle>();
        int index = 0;
        foreach (TileType tile in Enum.GetValues<TileType>())
        {
            int col = index % AtlasColumns;
            int row = index / AtlasColumns;
            int startX = col * TileSize;
            int startY = row * TileSize;
            _tileRects[tile] = new Rectangle(startX, startY, TileSize, TileSize);
            index++;
        }
    }
    
    private static Texture2D GenerateDetailedTileAtlas()
    {
        int atlasSize = TileSize * AtlasColumns;
        var texture = new Texture2D(_graphicsDevice, atlasSize, atlasSize);
        Color[] data = new Color[atlasSize * atlasSize];
        
        // Fill with transparent
        for (int i = 0; i < data.Length; i++)
            data[i] = Color.Transparent;
        
        int index = 0;
        foreach (TileType tile in Enum.GetValues<TileType>())
        {
            int col = index % AtlasColumns;
            int row = index / AtlasColumns;
            int startX = col * TileSize;
            int startY = row * TileSize;
            
            if (tile != TileType.Air)
            {
                GenerateDetailedTile(data, atlasSize, startX, startY, tile);
            }
            
            index++;
        }
        
        texture.SetData(data);
        return texture;
    }
    
    private static void GenerateDetailedTile(Color[] data, int atlasSize, int startX, int startY, TileType tile)
    {
        var baseColor = GetTileBaseColor(tile);
        var darkColor = Darken(baseColor, 0.3f);
        var lightColor = Lighten(baseColor, 0.2f);
        var highlightColor = Lighten(baseColor, 0.4f);
        
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                Color pixelColor = baseColor;
                
                switch (tile)
                {
                    case TileType.Grass:
                        pixelColor = GenerateGrassPixel(x, y, baseColor, darkColor, lightColor);
                        break;
                    case TileType.Dirt:
                        int dirtX = startX / TileSize;
                        int dirtY = startY / TileSize;
                        pixelColor = GenerateDirtPixel(x, y, baseColor, darkColor, lightColor, dirtX, dirtY);
                        break;
                    case TileType.Stone:
                        int stoneX = startX / TileSize;
                        int stoneY = startY / TileSize;
                        pixelColor = GenerateStonePixel(x, y, baseColor, darkColor, lightColor, stoneX, stoneY);
                        break;
                    case TileType.Sand:
                        int sandX = startX / TileSize;
                        int sandY = startY / TileSize;
                        pixelColor = GenerateSandPixel(x, y, baseColor, darkColor, lightColor, sandX, sandY);
                        break;
                    case TileType.Snow:
                        int snowX = startX / TileSize;
                        int snowY = startY / TileSize;
                        pixelColor = GenerateSnowPixel(x, y, baseColor, darkColor, highlightColor, snowX, snowY);
                        break;
                    case TileType.Wood:
                        int woodX = startX / TileSize;
                        int woodY = startY / TileSize;
                        pixelColor = GenerateWoodPixel(x, y, baseColor, darkColor, lightColor, woodX, woodY);
                        break;
                    case TileType.Leaves:
                        int leavesX = startX / TileSize;
                        int leavesY = startY / TileSize;
                        pixelColor = GenerateLeavesPixel(x, y, baseColor, darkColor, lightColor, leavesX, leavesY);
                        break;
                    case TileType.CopperOre:
                    case TileType.IronOre:
                    case TileType.GoldOre:
                    case TileType.DiamondOre:
                    case TileType.Coal:
                        int oreX = startX / TileSize;
                        int oreY = startY / TileSize;
                        pixelColor = GenerateOrePixel(x, y, tile, baseColor, oreX, oreY);
                        break;
                    case TileType.Water:
                        int waterX = startX / TileSize;
                        int waterY = startY / TileSize;
                        pixelColor = GenerateWaterPixel(x, y, baseColor, waterX, waterY);
                        break;
                    case TileType.Lava:
                        int lavaX = startX / TileSize;
                        int lavaY = startY / TileSize;
                        pixelColor = GenerateLavaPixel(x, y, baseColor, lavaX, lavaY);
                        break;
                    case TileType.Torch:
                        pixelColor = GenerateTorchPixel(x, y);
                        break;
                    default:
                        int defaultX = startX / TileSize;
                        int defaultY = startY / TileSize;
                        pixelColor = GenerateDefaultPixel(x, y, baseColor, darkColor, lightColor, defaultX, defaultY);
                        break;
                }
                
                data[(startY + y) * atlasSize + (startX + x)] = pixelColor;
            }
        }
    }
    
    private static Color GenerateGrassPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 17 + tileY * 23) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Top part is grass, bottom is dirt
        if (y < 4)
        {
            // Grass blades at top - clearer definition
            if (y == 0)
            {
                // Individual grass blades with better contrast
                if ((x + y * 2) % 3 == 0 && localRandom.NextDouble() > 0.4)
                    return Lighten(baseColor, 0.4f);
                if ((x * 2 + y) % 4 == 0 && localRandom.NextDouble() > 0.6)
                    return Darken(baseColor, 0.2f);
                
                // Add small flowers occasionally (colored pixels)
                if (variationSeed % 50 == 0 && x % 4 == 0 && localRandom.NextDouble() > 0.8)
                {
                    // Flower colors: red, yellow, blue, white
                    var flowerColors = new[] {
                        new Color(255, 50, 50),   // Red
                        new Color(255, 255, 100), // Yellow
                        new Color(100, 150, 255), // Blue
                        new Color(255, 255, 255)  // White
                    };
                    return flowerColors[variationSeed % flowerColors.Length];
                }
            }
            if (y < 3)
            {
                // More structured variation
                float noise = (float)(localRandom.NextDouble() * 0.4 - 0.2);
                var color = AddNoise(baseColor, noise);
                // Add subtle highlights
                if ((x + y) % 5 == 0 && localRandom.NextDouble() > 0.7)
                    color = Lighten(color, 0.15f);
                return color;
            }
            return baseColor;
        }
        else
        {
            // Dirt underneath - clearer transition
            var dirtColor = new Color(139, 90, 43);
            float noise = (float)(localRandom.NextDouble() * 0.3 - 0.15);
            var color = AddNoise(dirtColor, noise);
            
            // Add small stones/pebbles for texture
            if (variationSeed % 30 == 0 && localRandom.NextDouble() > 0.85)
            {
                // Small stone - darker gray
                return new Color(100, 100, 100);
            }
            if (localRandom.NextDouble() > 0.9)
                color = Darken(color, 0.15f);
            return color;
        }
    }
    
    private static Color GenerateDirtPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor, int tileX = 0, int tileY = 0)
    {
        float noise = (float)(_random.NextDouble() * 0.3 - 0.15);
        var color = AddNoise(baseColor, noise);
        
        // More defined pebbles/variation with better contrast
        if (_random.NextDouble() > 0.88)
            color = Darken(color, 0.3f);
        if (_random.NextDouble() > 0.92)
            color = Lighten(color, 0.2f);
        
        // Add subtle texture pattern
        if ((x * 2 + y * 3) % 7 == 0 && _random.NextDouble() > 0.7)
            color = Darken(color, 0.1f);
            
        return color;
    }
    
    private static Color GenerateStonePixel(int x, int y, Color baseColor, Color darkColor, Color lightColor, int tileX = 0, int tileY = 0)
    {
        // Create a rocky texture with clearer cracks and highlights
        float noise = (float)(_random.NextDouble() * 0.25 - 0.125);
        var color = AddNoise(baseColor, noise);
        
        // More defined cracks/lines with better contrast
        if ((x + y) % 5 == 0 && _random.NextDouble() > 0.5)
            color = Darken(color, 0.35f);
        if ((x * 2 + y * 3) % 6 == 0 && _random.NextDouble() > 0.65)
            color = Darken(color, 0.2f);
        
        // Stronger highlights for depth
        if ((x * 3 + y * 2) % 7 == 0 && _random.NextDouble() > 0.6)
            color = Lighten(color, 0.25f);
        if ((x + y * 2) % 8 == 0 && _random.NextDouble() > 0.75)
            color = Lighten(color, 0.15f);
            
        // Better edge shading
        if (x == 0 || y == 0)
            color = Lighten(color, 0.15f);
        if (x == TileSize - 1 || y == TileSize - 1)
            color = Darken(color, 0.1f);
            
        return color;
    }
    
    private static Color GenerateSandPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 13 + tileY * 17) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Natural sand color palette
        var sandLight = new Color(240, 220, 180);  // Light sand
        var sandMedium = baseColor;                 // Base sand (219, 194, 148)
        var sandDark = new Color(180, 160, 120);  // Dark sand
        var sandHighlight = new Color(255, 245, 220); // Highlight
        
        // Natural sand texture with wave-like patterns
        float wave1 = (float)Math.Sin(x * 0.4f + variationSeed * 0.1f) * 0.12f;
        float wave2 = (float)Math.Sin(y * 0.3f + variationSeed * 0.15f) * 0.08f;
        float wave3 = (float)Math.Sin((x + y) * 0.2f + variationSeed * 0.2f) * 0.05f;
        float combinedWave = wave1 + wave2 + wave3;
        
        var color = AddNoise(sandMedium, combinedWave);
        
        // Sandy specks - natural variation
        float speckNoise = (float)(localRandom.NextDouble());
        if (speckNoise > 0.88f) // Light specks
        {
            color = Lighten(color, 0.3f);
        }
        else if (speckNoise < 0.12f) // Dark specks
        {
            color = Darken(color, 0.2f);
        }
        
        // Wave patterns (wind-blown sand)
        float wavePattern = (float)Math.Sin(x * 0.5f + y * 0.3f + variationSeed * 0.1f);
        if (wavePattern > 0.6f)
        {
            color = Lighten(color, 0.15f);
        }
        else if (wavePattern < -0.6f)
        {
            color = Darken(color, 0.1f);
        }
        
        // Small pebbles/grains
        if ((x + y * 3) % 7 == 0 && localRandom.NextDouble() > 0.75)
        {
            color = Darken(color, 0.15f);
        }
        
        // Highlights for depth
        if ((x * 2 + y) % 9 == 0 && localRandom.NextDouble() > 0.7)
        {
            color = Lighten(color, 0.12f);
        }
        
        // 3D effect - lighter on top
        if (y < TileSize / 3)
        {
            color = Lighten(color, 0.1f);
        }
        // Darker on bottom
        else if (y > TileSize * 2 / 3)
        {
            color = Darken(color, 0.08f);
        }
        
        return color;
    }
    
    private static Color GenerateSnowPixel(int x, int y, Color baseColor, Color darkColor, Color highlightColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 11 + tileY * 19) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Natural snow color palette
        var snowPure = Color.White;
        var snowBase = baseColor;                    // Base snow (235, 245, 255)
        var snowBlue = new Color(220, 235, 250);    // Blue-tinted snow
        var snowShadow = new Color(200, 210, 220);  // Shadow areas
        
        // Natural snow texture with subtle variation
        float texture1 = (float)Math.Sin(x * 0.3f + variationSeed * 0.1f) * 0.08f;
        float texture2 = (float)Math.Sin(y * 0.4f + variationSeed * 0.15f) * 0.06f;
        float texture3 = (float)Math.Sin((x + y) * 0.25f + variationSeed * 0.2f) * 0.04f;
        float combinedTexture = texture1 + texture2 + texture3;
        
        var color = AddNoise(snowBase, combinedTexture);
        
        // Sparkle effect - bright highlights
        float sparkle = (float)(localRandom.NextDouble());
        if (sparkle > 0.94f) // Very bright sparkles
        {
            color = snowPure;
        }
        else if (sparkle > 0.90f) // Bright sparkles
        {
            color = new Color(250, 255, 255);
        }
        else if (sparkle > 0.88f) // Blue-tinted sparkles
        {
            color = snowBlue;
        }
        
        // Subtle shadows for depth (snow drifts)
        float shadowPattern = (float)Math.Sin(x * 0.2f + y * 0.3f + variationSeed * 0.1f);
        if (shadowPattern < -0.5f && localRandom.NextDouble() > 0.6)
        {
            color = Darken(color, 0.08f);
        }
        
        // Small shadows from tiny bumps
        if ((x + y * 2) % 7 == 0 && localRandom.NextDouble() > 0.75)
        {
            color = Darken(color, 0.05f);
        }
        
        // Highlights for 3D effect
        if ((x * 3 + y * 2) % 11 == 0 && localRandom.NextDouble() > 0.7)
        {
            color = Lighten(color, 0.1f);
        }
        
        // 3D effect - lighter on top (sunlight)
        if (y < TileSize / 4)
        {
            color = Lighten(color, 0.12f);
        }
        // Slightly darker on bottom
        else if (y > TileSize * 3 / 4)
        {
            color = Darken(color, 0.05f);
        }
        
        return color;
    }
    
    private static Color GenerateWoodPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 23 + tileY * 29) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Natural wood color palette
        var barkDark = new Color(100, 70, 45);     // Dark bark
        var barkMedium = baseColor;                 // Base wood (168, 122, 81)
        var barkLight = new Color(190, 145, 100);  // Light wood
        var barkHighlight = new Color(210, 165, 120); // Highlight
        
        // Vertical wood grain pattern (tree rings)
        // Multiple sine waves for natural variation
        float grain1 = (float)Math.Sin(x * 0.6f + variationSeed * 0.1f) * 0.15f;
        float grain2 = (float)Math.Sin(x * 1.2f + variationSeed * 0.2f) * 0.08f;
        float grain3 = (float)Math.Sin(x * 2.4f + variationSeed * 0.3f) * 0.04f;
        float combinedGrain = grain1 + grain2 + grain3;
        
        var color = AddNoise(barkMedium, combinedGrain);
        
        // Wood rings (growth rings) - darker lines
        // Vary ring spacing based on position
        int ringSpacing = 3 + (variationSeed % 3);
        if (x % ringSpacing == 0)
        {
            float ringIntensity = 0.12f + (float)(localRandom.NextDouble() * 0.08f);
            color = Darken(color, ringIntensity);
        }
        
        // Major growth rings (wider, darker)
        int majorRingSpacing = ringSpacing * 2 + 1;
        if (x % majorRingSpacing == 0 && localRandom.NextDouble() > 0.4)
        {
            color = Darken(color, 0.18f);
        }
        
        // Bark texture - horizontal variations
        float barkTexture = (float)Math.Sin(y * 0.5f + variationSeed * 0.15f) * 0.1f;
        color = AddNoise(color, barkTexture);
        
        // Natural knots (darker circular areas)
        // Position-based knots for consistency
        for (int i = 0; i < 2; i++)
        {
            int knotSeed = (variationSeed + i * 211) % 1000;
            var knotRandom = new Random(knotSeed);
            
            float knotX = (float)(knotRandom.NextDouble() * TileSize);
            float knotY = (float)(knotRandom.NextDouble() * TileSize);
            
            float dist = (float)Math.Sqrt((x - knotX) * (x - knotX) + (y - knotY) * (y - knotY));
            
            if (dist < 1.5f + knotRandom.NextDouble() * 1.0f)
            {
                // Knot center - very dark
                if (dist < 0.8f)
                {
                    color = Darken(barkDark, 0.2f);
                }
                // Knot edge - medium dark
                else
                {
                    color = Darken(color, 0.25f);
                }
            }
        }
        
        // 3D effect - lighter on left (assuming light from left)
        if (x < TileSize / 3)
        {
            color = Lighten(color, 0.12f);
        }
        // Darker on right
        else if (x > TileSize * 2 / 3)
        {
            color = Darken(color, 0.08f);
        }
        
        // Subtle highlights for depth
        if ((x * 3 + y * 2) % 11 == 0 && localRandom.NextDouble() > 0.7)
        {
            color = Lighten(color, 0.1f);
        }
        
        // Add natural color variation
        float colorVariation = (float)(localRandom.NextDouble() * 0.1 - 0.05);
        color = AddNoise(color, colorVariation);
        
        return color;
    }
    
    private static Color GenerateLeavesPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 19 + tileY * 31) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Natural leaf color palette - multiple green tones
        var lightGreen = new Color(80, 180, 60);   // Bright green
        var mediumGreen = baseColor;                // Base green (50, 130, 50)
        var darkGreen = new Color(30, 100, 30);    // Dark green
        var yellowGreen = new Color(120, 160, 40); // Yellow-green highlights
        
        // Create leaf-like patterns using elliptical shapes
        // Multiple "leaves" per tile for natural look
        bool isLeaf = false;
        Color leafColor = mediumGreen;
        
        // Create 3-4 leaf patterns per tile
        for (int i = 0; i < 4; i++)
        {
            int leafSeed = (variationSeed + i * 137) % 1000;
            var leafRandom = new Random(leafSeed);
            
            // Random leaf center within tile
            float leafCenterX = (float)(leafRandom.NextDouble() * TileSize);
            float leafCenterY = (float)(leafRandom.NextDouble() * TileSize);
            
            // Elliptical leaf shape (wider than tall)
            float dx = (x - leafCenterX) / 4f;
            float dy = (y - leafCenterY) / 6f;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            
            // Leaf shape: elliptical with some randomness
            if (dist < 2.5f + leafRandom.NextDouble() * 1.5f)
            {
                isLeaf = true;
                
                // Vary leaf color based on position and seed
                int colorVariant = leafSeed % 4;
                leafColor = colorVariant switch
                {
                    0 => lightGreen,
                    1 => mediumGreen,
                    2 => darkGreen,
                    _ => yellowGreen
                };
                
                // Add vein pattern (darker line through center)
                float veinDist = Math.Abs(dx);
                if (veinDist < 0.5f && dist < 2.0f)
                {
                    leafColor = Darken(leafColor, 0.2f);
                }
                
                // Add highlight on one side (3D effect)
                if (dx > 0 && dist < 1.5f)
                {
                    leafColor = Lighten(leafColor, 0.15f);
                }
                
                break;
            }
        }
        
        // If not part of a leaf, create natural gaps (transparency)
        if (!isLeaf)
        {
            // Natural hole pattern - not too many, not too few
            float holeNoise = (float)(localRandom.NextDouble());
            if (holeNoise > 0.92f) // ~8% chance for holes
            {
                return Color.Transparent;
            }
            
            // Background foliage - darker, less defined
            float backgroundNoise = (float)(localRandom.NextDouble() * 0.2 - 0.1);
            return AddNoise(darkGreen, backgroundNoise);
        }
        
        // Add subtle noise to leaf color for texture
        float textureNoise = (float)(localRandom.NextDouble() * 0.15 - 0.075);
        leafColor = AddNoise(leafColor, textureNoise);
        
        // Add depth with subtle shadows
        if ((x + y) % 3 == 0 && localRandom.NextDouble() > 0.7)
        {
            leafColor = Darken(leafColor, 0.1f);
        }
        
        return leafColor;
    }
    
    private static Color GenerateOrePixel(int x, int y, TileType oreType, Color oreColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 37 + tileY * 41) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Stone background with better texture
        var stoneColor = new Color(105, 105, 105);
        var stoneDark = new Color(85, 85, 85);
        var stoneLight = new Color(125, 125, 125);
        
        // Better stone texture
        float stoneTexture1 = (float)Math.Sin(x * 0.5f + variationSeed * 0.1f) * 0.1f;
        float stoneTexture2 = (float)Math.Sin(y * 0.4f + variationSeed * 0.15f) * 0.08f;
        float stoneTexture3 = (float)Math.Sin((x + y) * 0.3f + variationSeed * 0.2f) * 0.05f;
        float combinedStoneTexture = stoneTexture1 + stoneTexture2 + stoneTexture3;
        
        var color = AddNoise(stoneColor, combinedStoneTexture);
        
        // Stone cracks and details
        if ((x + y) % 5 == 0 && localRandom.NextDouble() > 0.6)
        {
            color = Darken(color, 0.15f);
        }
        if ((x * 2 + y) % 7 == 0 && localRandom.NextDouble() > 0.7)
        {
            color = Lighten(color, 0.1f);
        }
        
        // Ore veins pattern - more defined and natural
        bool isOre = false;
        float centerX = TileSize / 2f;
        float centerY = TileSize / 2f;
        float dist = (float)Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
        
        // Create multiple ore clusters for natural look
        for (int i = 0; i < 3; i++)
        {
            int clusterSeed = (variationSeed + i * 151) % 1000;
            var clusterRandom = new Random(clusterSeed);
            
            float clusterX = centerX + (float)(clusterRandom.NextDouble() - 0.5) * 6f;
            float clusterY = centerY + (float)(clusterRandom.NextDouble() - 0.5) * 6f;
            float clusterDist = (float)Math.Sqrt((x - clusterX) * (x - clusterX) + (y - clusterY) * (y - clusterY));
            
            // Ore cluster
            if (clusterDist < 3.5f + clusterRandom.NextDouble() * 2f)
            {
                if (clusterRandom.NextDouble() > 0.25f) // 75% chance in cluster
                {
                    isOre = true;
                    break;
                }
            }
        }
        
        // Scattered ore outside clusters
        if (!isOre && localRandom.NextDouble() > 0.88f)
        {
            isOre = true;
        }
        
        if (isOre)
        {
            // Better ore color with variation
            float oreNoise = (float)(localRandom.NextDouble() * 0.25 - 0.125);
            color = AddNoise(oreColor, oreNoise);
            
            // Ore vein highlights (shiny spots)
            if ((x + y * 2) % 4 == 0 && localRandom.NextDouble() > 0.7)
            {
                color = Lighten(color, 0.2f);
            }
            
            // Enhanced shine for precious ores
            if (oreType == TileType.GoldOre || oreType == TileType.DiamondOre)
            {
                // More frequent and brighter shine
                if (localRandom.NextDouble() > 0.75f)
                {
                    color = Lighten(color, 0.35f);
                }
                // Very bright highlights
                if ((x * 3 + y * 2) % 5 == 0 && localRandom.NextDouble() > 0.85f)
                {
                    color = Lighten(color, 0.5f);
                }
            }
            else if (oreType == TileType.CopperOre || oreType == TileType.IronOre)
            {
                // Subtle shine for common ores
                if (localRandom.NextDouble() > 0.85f)
                {
                    color = Lighten(color, 0.15f);
                }
            }
            
            // Ore edge definition (darker edges for depth)
            float edgeDist = Math.Min(
                Math.Min(x, TileSize - x),
                Math.Min(y, TileSize - y)
            );
            if (edgeDist < 1.5f)
            {
                color = Darken(color, 0.1f);
            }
        }
        
        // 3D effect - lighter on top
        if (y < TileSize / 3)
        {
            color = Lighten(color, 0.08f);
        }
        // Darker on bottom
        else if (y > TileSize * 2 / 3)
        {
            color = Darken(color, 0.06f);
        }
        
        return color;
    }
    
    private static Color GenerateWaterPixel(int x, int y, Color baseColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 43 + tileY * 47) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Natural water color palette
        var waterDeep = new Color(20, 70, 180);      // Deep water
        var waterMedium = baseColor;                 // Base water (30, 90, 200)
        var waterLight = new Color(60, 130, 230);   // Light water
        var waterFoam = new Color(180, 200, 255);   // Foam/whitecaps
        
        // Multi-layered wave patterns for natural water
        float wave1 = (float)Math.Sin(x * 0.6f + variationSeed * 0.1f) * 0.12f;
        float wave2 = (float)Math.Sin(y * 0.5f + variationSeed * 0.15f) * 0.1f;
        float wave3 = (float)Math.Sin((x + y) * 0.4f + variationSeed * 0.2f) * 0.08f;
        float wave4 = (float)Math.Sin((x - y) * 0.35f + variationSeed * 0.25f) * 0.06f;
        float combinedWave = wave1 + wave2 + wave3 + wave4;
        
        var color = AddNoise(waterMedium, combinedWave);
        
        // Depth variation - deeper in center, lighter at edges
        float centerX = TileSize / 2f;
        float centerY = TileSize / 2f;
        float distFromCenter = (float)Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
        float depthFactor = distFromCenter / (TileSize / 2f);
        
        if (depthFactor < 0.3f)
        {
            // Deep water - darker
            color = Color.Lerp(color, waterDeep, 0.2f);
        }
        else if (depthFactor > 0.8f)
        {
            // Shallow water - lighter
            color = Color.Lerp(color, waterLight, 0.15f);
        }
        
        // Foam/whitecaps at edges
        float edgeDist = Math.Min(
            Math.Min(x, TileSize - x),
            Math.Min(y, TileSize - y)
        );
        if (edgeDist < 2f && localRandom.NextDouble() > 0.7f)
        {
            color = Color.Lerp(color, waterFoam, 0.4f);
        }
        
        // Ripples and small waves
        float ripple = (float)Math.Sin(x * 0.8f + y * 0.6f + variationSeed * 0.1f);
        if (ripple > 0.7f)
        {
            color = Lighten(color, 0.1f);
        }
        else if (ripple < -0.7f)
        {
            color = Darken(color, 0.08f);
        }
        
        // Highlights for surface reflection
        if ((x * 2 + y * 3) % 9 == 0 && localRandom.NextDouble() > 0.75f)
        {
            color = Lighten(color, 0.15f);
        }
        
        // Semi-transparent for water effect
        color.A = 200; // Slightly more opaque for better visibility
        
        return color;
    }
    
    private static Color GenerateLavaPixel(int x, int y, Color baseColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 53 + tileY * 59) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Natural lava color palette
        var lavaDark = new Color(180, 40, 10);       // Dark lava
        var lavaMedium = baseColor;                  // Base lava (255, 80, 20)
        var lavaBright = new Color(255, 150, 50);   // Bright lava
        var lavaGlow = new Color(255, 255, 150);    // Glowing hot spots
        var lavaCore = new Color(255, 200, 100);    // Core glow
        
        // Multi-layered glow patterns for natural lava
        float glow1 = (float)(Math.Sin(x * 0.5f + variationSeed * 0.1f) * Math.Cos(y * 0.4f + variationSeed * 0.15f) * 0.15f);
        float glow2 = (float)(Math.Sin(x * 0.8f + variationSeed * 0.2f) * Math.Cos(y * 0.6f + variationSeed * 0.25f) * 0.1f);
        float glow3 = (float)(Math.Sin((x + y) * 0.3f + variationSeed * 0.3f) * 0.08f);
        float combinedGlow = glow1 + glow2 + glow3;
        
        var color = AddNoise(lavaMedium, combinedGlow);
        
        // Hot spots - bright glowing areas
        float hotSpotNoise = (float)(localRandom.NextDouble());
        if (hotSpotNoise > 0.92f) // Very hot spots
        {
            color = lavaGlow;
        }
        else if (hotSpotNoise > 0.88f) // Hot spots
        {
            color = lavaCore;
        }
        else if (hotSpotNoise > 0.85f) // Bright lava
        {
            color = lavaBright;
        }
        else if (hotSpotNoise < 0.15f) // Cooler spots
        {
            color = Darken(lavaDark, 0.1f);
        }
        
        // Lava flow patterns
        float flowPattern = (float)Math.Sin(x * 0.4f + y * 0.5f + variationSeed * 0.1f);
        if (flowPattern > 0.6f)
        {
            color = Lighten(color, 0.1f);
        }
        else if (flowPattern < -0.6f)
        {
            color = Darken(color, 0.08f);
        }
        
        // Bubbling effect
        if ((x + y * 2) % 5 == 0 && localRandom.NextDouble() > 0.8f)
        {
            color = Lighten(color, 0.2f);
        }
        
        // Core glow in center
        float centerX = TileSize / 2f;
        float centerY = TileSize / 2f;
        float distFromCenter = (float)Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
        if (distFromCenter < 3f)
        {
            float glowIntensity = 1f - (distFromCenter / 3f);
            color = Color.Lerp(color, lavaCore, glowIntensity * 0.3f);
        }
        
        // Edge cooling
        float edgeDist = Math.Min(
            Math.Min(x, TileSize - x),
            Math.Min(y, TileSize - y)
        );
        if (edgeDist < 2f)
        {
            color = Darken(color, 0.15f);
        }
        
        return color;
    }
    
    private static Color GenerateTorchPixel(int x, int y)
    {
        int centerX = TileSize / 2;
        
        // Stick
        if (y > 6 && Math.Abs(x - centerX) < 2)
            return new Color(139, 90, 43);
        
        // Flame
        if (y <= 8 && y > 2)
        {
            float dist = Math.Abs(x - centerX) + (8 - y) * 0.5f;
            if (dist < 4)
            {
                if (dist < 2)
                    return new Color(255, 255, 200); // Hot center
                return new Color(255, 150, 50); // Orange flame
            }
        }
        
        // Flame tip
        if (y <= 2 && Math.Abs(x - centerX) < 2)
            return new Color(255, 100, 0);
        
        return Color.Transparent;
    }
    
    private static Color GenerateDefaultPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor, int tileX = 0, int tileY = 0)
    {
        // Use tile position for variation (deterministic but varied)
        int variationSeed = (tileX * 61 + tileY * 67) % 1000;
        var localRandom = new Random(variationSeed);
        
        // Natural texture with variation
        float texture1 = (float)Math.Sin(x * 0.4f + variationSeed * 0.1f) * 0.1f;
        float texture2 = (float)Math.Sin(y * 0.35f + variationSeed * 0.15f) * 0.08f;
        float texture3 = (float)Math.Sin((x + y) * 0.3f + variationSeed * 0.2f) * 0.05f;
        float combinedTexture = texture1 + texture2 + texture3;
        
        var color = AddNoise(baseColor, combinedTexture);
        
        // Subtle pattern for depth
        if ((x + y) % 4 == 0 && localRandom.NextDouble() > 0.6)
        {
            color = Darken(color, 0.1f);
        }
        if ((x * 2 + y) % 6 == 0 && localRandom.NextDouble() > 0.7)
        {
            color = Lighten(color, 0.08f);
        }
        
        // Enhanced 3D effect with gradients
        // Top and left edges - lighter (light source from top-left)
        float edgeFactor = 0f;
        if (x == 0) edgeFactor += 0.2f;
        if (y == 0) edgeFactor += 0.2f;
        if (x == 1) edgeFactor += 0.1f;
        if (y == 1) edgeFactor += 0.1f;
        
        if (edgeFactor > 0)
        {
            color = Lighten(color, edgeFactor);
        }
        
        // Bottom and right edges - darker (shadows)
        edgeFactor = 0f;
        if (x == TileSize - 1) edgeFactor += 0.2f;
        if (y == TileSize - 1) edgeFactor += 0.2f;
        if (x == TileSize - 2) edgeFactor += 0.1f;
        if (y == TileSize - 2) edgeFactor += 0.1f;
        
        if (edgeFactor > 0)
        {
            color = Darken(color, edgeFactor);
        }
        
        // Corner highlights
        if ((x == 0 && y == 0) || (x == 0 && y == TileSize - 1) || 
            (x == TileSize - 1 && y == 0))
        {
            color = Lighten(color, 0.15f);
        }
        
        // Corner shadows
        if (x == TileSize - 1 && y == TileSize - 1)
        {
            color = Darken(color, 0.2f);
        }
        
        return color;
    }
    
    private static Color GetTileBaseColor(TileType tile)
    {
        return tile switch
        {
            TileType.Dirt => new Color(151, 107, 75),
            TileType.Grass => new Color(86, 152, 23),
            TileType.Stone => new Color(128, 128, 128),
            TileType.Sand => new Color(219, 194, 148),
            TileType.Snow => new Color(235, 245, 255),
            TileType.Wood => new Color(140, 100, 70),  // Warmer, more natural wood tone
            TileType.Leaves => new Color(60, 140, 50),  // Brighter, more natural green
            TileType.CopperOre => new Color(205, 127, 50),
            TileType.IronOre => new Color(195, 195, 195),
            TileType.GoldOre => new Color(255, 215, 0),
            TileType.DiamondOre => new Color(185, 242, 255),
            TileType.Coal => new Color(45, 45, 45),
            TileType.Water => new Color(30, 90, 200),
            TileType.Lava => new Color(255, 80, 20),
            TileType.Bedrock => new Color(50, 50, 50),
            TileType.Obsidian => new Color(30, 20, 50),
            TileType.JungleGrass => new Color(80, 170, 50),
            TileType.Mud => new Color(92, 68, 52),
            TileType.Ash => new Color(80, 80, 80),
            TileType.Torch => new Color(255, 150, 50),
            TileType.Chest => new Color(168, 122, 81),
            TileType.Furnace => new Color(120, 120, 120),
            TileType.Anvil => new Color(100, 100, 110),
            TileType.Brick => new Color(180, 80, 70),
            TileType.WoodPlatform => new Color(150, 110, 70),
            TileType.Cobalt => new Color(50, 100, 200),
            TileType.Mythril => new Color(100, 200, 150),
            TileType.Adamantite => new Color(200, 50, 80),
            _ => new Color(200, 200, 200)
        };
    }
    
    private static Texture2D GeneratePlayerSprite()
    {
        // 20x42 player sprite with simple design
        int width = 20;
        int height = 42;
        var texture = new Texture2D(_graphicsDevice, width, height);
        Color[] data = new Color[width * height];
        
        // Hair color
        Color hair = new Color(139, 90, 43);
        // Skin color
        Color skin = new Color(255, 213, 170);
        // Shirt color  
        Color shirt = new Color(70, 130, 180);
        // Pants color
        Color pants = new Color(90, 75, 65);
        // Shoes
        Color shoes = new Color(60, 50, 40);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = Color.Transparent;
                int cx = width / 2;
                
                // Head (y 0-12)
                if (y >= 0 && y < 12)
                {
                    // Hair (top)
                    if (y < 5 && Math.Abs(x - cx) < 5)
                        pixel = hair;
                    // Face
                    else if (y >= 4 && y < 12 && Math.Abs(x - cx) < 4)
                    {
                        pixel = skin;
                        // Eyes
                        if (y == 7 && (x == cx - 2 || x == cx + 1))
                            pixel = Color.Black;
                    }
                }
                // Body (y 12-28)
                else if (y >= 12 && y < 28)
                {
                    if (Math.Abs(x - cx) < 5)
                        pixel = shirt;
                    // Arms
                    if (y >= 12 && y < 24)
                    {
                        if (x == cx - 6 || x == cx + 5)
                            pixel = shirt;
                        if (x == cx - 7 || x == cx + 6)
                            pixel = skin;
                    }
                }
                // Legs (y 28-38)
                else if (y >= 28 && y < 38)
                {
                    if (Math.Abs(x - cx) < 4)
                    {
                        // Left or right leg
                        if ((x < cx && x >= cx - 3) || (x >= cx && x < cx + 3))
                            pixel = pants;
                    }
                }
                // Feet (y 38-42)
                else if (y >= 38 && y < 42)
                {
                    if (Math.Abs(x - cx) < 5)
                        pixel = shoes;
                }
                
                data[y * width + x] = pixel;
            }
        }
        
        texture.SetData(data);
        return texture;
    }
    
    private static Texture2D GeneratePlayerSpriteSheet()
    {
        // Sprite sheet dimensions: 20x42 per frame
        // Layout: 6 animations x 6 frames max = 120x252 total
        const int frameWidth = 20;
        const int frameHeight = 42;
        const int framesPerRow = 6;
        const int totalRows = 6; // Idle, Walk, Run, Jump, Fall, Mining
        
        int sheetWidth = frameWidth * framesPerRow;
        int sheetHeight = frameHeight * totalRows;
        var texture = new Texture2D(_graphicsDevice, sheetWidth, sheetHeight);
        Color[] data = new Color[sheetWidth * sheetHeight];
        
        // Fill with transparent
        for (int i = 0; i < data.Length; i++)
            data[i] = Color.Transparent;
        
        // Colors
        Color hair = new Color(184, 115, 51); // Brown-orange hair
        Color skin = new Color(255, 213, 170); // Peach skin
        Color shirt = new Color(139, 90, 43); // Tan/beige shirt
        Color pants = new Color(25, 25, 112); // Dark blue pants
        Color shoes = new Color(60, 50, 40); // Dark shoes
        
        // Generate each animation row
        int row = 0;
        
        // Row 0: Idle (3 frames - subtle breathing)
        GenerateIdleAnimation(data, sheetWidth, 0, row * frameHeight, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
        
        // Row 1: Walk (6 frames - smooth walking cycle)
        GenerateWalkAnimation(data, sheetWidth, 0, (row + 1) * frameHeight, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
        
        // Row 2: Run (6 frames - faster movement)
        GenerateRunAnimation(data, sheetWidth, 0, (row + 2) * frameHeight, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
        
        // Row 3: Jump (2 frames - jump pose)
        GenerateJumpAnimation(data, sheetWidth, 0, (row + 3) * frameHeight, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
        
        // Row 4: Fall (1 frame - falling pose)
        GenerateFallAnimation(data, sheetWidth, 0, (row + 4) * frameHeight, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
        
        // Row 5: Mining (3 frames - mining swing)
        GenerateMiningAnimation(data, sheetWidth, 0, (row + 5) * frameHeight, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
        
        texture.SetData(data);
        return texture;
    }
    
    private static void GenerateIdleAnimation(Color[] data, int sheetWidth, int startX, int startY, int frameWidth, int frameHeight, 
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // 3 frames of idle animation (subtle breathing)
        for (int frame = 0; frame < 3; frame++)
        {
            int frameX = startX + frame * frameWidth;
            
            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int dataIndex = (startY + y) * sheetWidth + (frameX + x);
                    if (dataIndex < 0 || dataIndex >= data.Length) continue;
                    
                    Color pixel = GeneratePlayerPixel(x, y, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
                    data[dataIndex] = pixel;
                }
            }
        }
    }
    
    private static void GenerateWalkAnimation(Color[] data, int sheetWidth, int startX, int startY, int frameWidth, int frameHeight,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // 6 frames of walking animation
        for (int frame = 0; frame < 6; frame++)
        {
            int frameX = startX + frame * frameWidth;
            
            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int dataIndex = (startY + y) * sheetWidth + (frameX + x);
                    if (dataIndex < 0 || dataIndex >= data.Length) continue;
                    
                    Color pixel = GeneratePlayerPixelWalk(x, y, frameWidth, frameHeight, frame, hair, skin, shirt, pants, shoes);
                    data[dataIndex] = pixel;
                }
            }
        }
    }
    
    private static void GenerateRunAnimation(Color[] data, int sheetWidth, int startX, int startY, int frameWidth, int frameHeight,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // 6 frames of running animation (faster, more dynamic)
        for (int frame = 0; frame < 6; frame++)
        {
            int frameX = startX + frame * frameWidth;
            
            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int dataIndex = (startY + y) * sheetWidth + (frameX + x);
                    if (dataIndex < 0 || dataIndex >= data.Length) continue;
                    
                    Color pixel = GeneratePlayerPixelRun(x, y, frameWidth, frameHeight, frame, hair, skin, shirt, pants, shoes);
                    data[dataIndex] = pixel;
                }
            }
        }
    }
    
    private static void GenerateJumpAnimation(Color[] data, int sheetWidth, int startX, int startY, int frameWidth, int frameHeight,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // 2 frames: jump start and mid-air
        for (int frame = 0; frame < 2; frame++)
        {
            int frameX = startX + frame * frameWidth;
            
            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int dataIndex = (startY + y) * sheetWidth + (frameX + x);
                    if (dataIndex < 0 || dataIndex >= data.Length) continue;
                    
                    Color pixel = GeneratePlayerPixelJump(x, y, frameWidth, frameHeight, frame, hair, skin, shirt, pants, shoes);
                    data[dataIndex] = pixel;
                }
            }
        }
    }
    
    private static void GenerateFallAnimation(Color[] data, int sheetWidth, int startX, int startY, int frameWidth, int frameHeight,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // 1 frame: falling pose (arms and legs spread)
        int frameX = startX;
        
        for (int y = 0; y < frameHeight; y++)
        {
            for (int x = 0; x < frameWidth; x++)
            {
                int dataIndex = (startY + y) * sheetWidth + (frameX + x);
                if (dataIndex < 0 || dataIndex >= data.Length) continue;
                
                Color pixel = GeneratePlayerPixelFall(x, y, frameWidth, frameHeight, hair, skin, shirt, pants, shoes);
                data[dataIndex] = pixel;
            }
        }
    }
    
    private static void GenerateMiningAnimation(Color[] data, int sheetWidth, int startX, int startY, int frameWidth, int frameHeight,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // 3 frames: mining swing animation
        for (int frame = 0; frame < 3; frame++)
        {
            int frameX = startX + frame * frameWidth;
            
            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int dataIndex = (startY + y) * sheetWidth + (frameX + x);
                    if (dataIndex < 0 || dataIndex >= data.Length) continue;
                    
                    Color pixel = GeneratePlayerPixelMining(x, y, frameWidth, frameHeight, frame, hair, skin, shirt, pants, shoes);
                    data[dataIndex] = pixel;
                }
            }
        }
    }
    
    private static Color GeneratePlayerPixel(int x, int y, int width, int height, Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        Color pixel = Color.Transparent;
        int cx = width / 2;
        
        // Head (y 0-12)
        if (y >= 0 && y < 12)
        {
            // Hair (top, spiky)
            if (y < 5 && Math.Abs(x - cx) < 5)
            {
                // Spiky hair pattern
                if (y == 0 && (x == cx - 2 || x == cx + 2))
                    pixel = hair;
                else if (y == 1 && (x >= cx - 4 && x <= cx + 4))
                    pixel = hair;
                else if (y >= 2 && y < 5 && Math.Abs(x - cx) < 4)
                    pixel = hair;
            }
            // Face
            else if (y >= 4 && y < 12 && Math.Abs(x - cx) < 4)
            {
                pixel = skin;
                // Eyes
                if (y == 7 && (x == cx - 2 || x == cx + 1))
                    pixel = Color.Black;
            }
        }
        // Body (y 12-28)
        else if (y >= 12 && y < 28)
        {
            if (Math.Abs(x - cx) < 5)
                pixel = shirt;
            // Arms
            if (y >= 12 && y < 24)
            {
                if (x == cx - 6 || x == cx + 5)
                    pixel = shirt;
                if (x == cx - 7 || x == cx + 6)
                    pixel = skin;
            }
        }
        // Legs (y 28-38)
        else if (y >= 28 && y < 38)
        {
            if (Math.Abs(x - cx) < 4)
            {
                if ((x < cx && x >= cx - 3) || (x >= cx && x < cx + 3))
                    pixel = pants;
            }
        }
        // Feet (y 38-42)
        else if (y >= 38 && y < height)
        {
            if (Math.Abs(x - cx) < 5)
                pixel = shoes;
        }
        
        return pixel;
    }
    
    private static Color GeneratePlayerPixelWalk(int x, int y, int width, int height, int frame, 
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // Similar to GeneratePlayerPixel but with leg and arm movement
        Color pixel = GeneratePlayerPixel(x, y, width, height, hair, skin, shirt, pants, shoes);
        
        // Adjust legs based on frame
        if (y >= 28 && y < 38) // Leg area
        {
            int cx = width / 2;
            // Shift legs based on frame
            if (frame % 2 == 0) // Left leg forward
            {
                if (x < cx - 1 && x >= cx - 3)
                    pixel = pants;
            }
            else // Right leg forward
            {
                if (x >= cx + 1 && x < cx + 3)
                    pixel = pants;
            }
        }
        
        // Adjust arms based on frame
        if (y >= 12 && y < 24) // Arm area
        {
            int cx = width / 2;
            if (frame % 2 == 0) // Right arm forward
            {
                if (x == cx + 5 || x == cx + 6)
                    pixel = frame % 2 == 0 ? shirt : skin;
            }
            else // Left arm forward
            {
                if (x == cx - 6 || x == cx - 7)
                    pixel = frame % 2 == 0 ? skin : shirt;
            }
        }
        
        return pixel;
    }
    
    private static Color GeneratePlayerPixelRun(int x, int y, int width, int height, int frame,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        // Similar to walk but more dynamic
        Color pixel = GeneratePlayerPixelWalk(x, y, width, height, frame, hair, skin, shirt, pants, shoes);
        
        // Add body lean effect (slight forward tilt)
        // This is handled by the frame generation, not per-pixel
        
        return pixel;
    }
    
    private static Color GeneratePlayerPixelJump(int x, int y, int width, int height, int frame,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        Color pixel = GeneratePlayerPixel(x, y, width, height, hair, skin, shirt, pants, shoes);
        
        // Adjust arms (raised)
        if (y >= 10 && y < 20) // Higher arm position
        {
            int cx = width / 2;
            if (x == cx - 6 || x == cx + 5)
                pixel = shirt;
            if (x == cx - 7 || x == cx + 6)
                pixel = skin;
        }
        
        // Adjust legs (bent or extended)
        if (y >= 28 && y < 38)
        {
            // Legs closer together when jumping
            int cx = width / 2;
            if (Math.Abs(x - cx) < 3)
                pixel = pants;
        }
        
        return pixel;
    }
    
    private static Color GeneratePlayerPixelFall(int x, int y, int width, int height,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        Color pixel = GeneratePlayerPixel(x, y, width, height, hair, skin, shirt, pants, shoes);
        
        // Arms spread out
        if (y >= 12 && y < 24)
        {
            int cx = width / 2;
            if (x == cx - 7 || x == cx + 6)
                pixel = shirt;
            if (x == cx - 8 || x == cx + 7)
                pixel = skin;
        }
        
        // Legs spread slightly
        if (y >= 28 && y < 38)
        {
            int cx = width / 2;
            if (x < cx - 2 || x > cx + 2)
                pixel = pants;
        }
        
        return pixel;
    }
    
    private static Color GeneratePlayerPixelMining(int x, int y, int width, int height, int frame,
        Color hair, Color skin, Color shirt, Color pants, Color shoes)
    {
        Color pixel = GeneratePlayerPixel(x, y, width, height, hair, skin, shirt, pants, shoes);
        
        // Mining pose: one arm raised, one arm down
        if (y >= 12 && y < 24)
        {
            int cx = width / 2;
            // Right arm raised (mining tool)
            if (frame < 2)
            {
                if (x == cx + 5 || x == cx + 6)
                    pixel = y < 18 ? shirt : skin; // Arm raised
            }
            // Left arm down
            if (x == cx - 6 || x == cx - 7)
                pixel = shirt;
        }
        
        // Slight body lean
        // Legs slightly bent
        if (y >= 28 && y < 36)
        {
            int cx = width / 2;
            if (Math.Abs(x - cx) < 3)
                pixel = pants;
        }
        
        return pixel;
    }
    
    private static Texture2D GenerateParticleTexture()
    {
        int size = 8;
        var texture = new Texture2D(_graphicsDevice, size, size);
        Color[] data = new Color[size * size];
        
        float center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = (float)Math.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float alpha = Math.Max(0, 1 - dist / center);
                data[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        
        texture.SetData(data);
        return texture;
    }
    
    private static void LoadParallaxLayers()
    {
        _parallaxLayers.Clear();
        
        // Load 3 layers for good depth with better performance
        for (int i = 0; i < 3; i++)
        {
            string file = string.Format(ParallaxFileFormat, i);
            var tex = LoadOrGenerate(file, () => GenerateParallaxLayer(i));
            _parallaxLayers.Add(tex);
        }
    }
    
    private static Texture2D GenerateParallaxLayer(int index)
    {
        int width = 512;
        int height = 256;
        var texture = new Texture2D(_graphicsDevice, width, height);
        Color[] data = new Color[width * height];
        
        // depth-based palette
        var skyTop = new Color(120, 185, 235);
        var skyBottom = new Color(200, 225, 245);
        var ridgeDark = new Color(50, 70 + index * 10, 90 + index * 15);
        var ridgeLight = new Color(90, 110 + index * 10, 130 + index * 15);
        
        float freqA = 0.01f + index * 0.004f;
        float freqB = 0.035f + index * 0.008f;
        float amp = 35 + index * 18;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float t = y / (float)height;
                Color sky = Color.Lerp(skyTop, skyBottom, t);
                
                float ridgeHeight = height * (0.45f + index * 0.1f)
                                    + (float)Math.Sin(x * freqA) * amp
                                    + (float)Math.Sin(x * freqB) * (amp * 0.4f);
                
                if (y > ridgeHeight)
                {
                    float depth = Math.Clamp((y - ridgeHeight) / (height - ridgeHeight), 0f, 1f);
                    sky = Color.Lerp(ridgeLight, ridgeDark, depth);
                }
                
                data[y * width + x] = sky;
            }
        }
        
        texture.SetData(data);
        return texture;
    }
    
    private static Texture2D GenerateUISlot(bool selected = false)
    {
        int size = 48;
        var tex = new Texture2D(_graphicsDevice, size, size);
        Color[] data = new Color[size * size];
        
        Color bg = selected ? new Color(80, 70, 40, 230) : new Color(35, 35, 50, 220);
        Color border = selected ? new Color(255, 215, 80) : new Color(100, 110, 140);
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                data[y * size + x] = isBorder ? border : bg;
            }
        }
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateUIHotbar()
    {
        int width = 9 * 48 + 8 * 4 + 16;
        int height = 64;
        var tex = new Texture2D(_graphicsDevice, width, height);
        Color[] data = new Color[width * height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < 3 || y < 3 || x >= width - 3 || y >= height - 3;
                var color = isBorder ? new Color(90, 90, 120) : new Color(25, 25, 40, 200);
                data[y * width + x] = color;
            }
        }
        
        tex.SetData(data);
        return tex;
    }
    
    public static Rectangle GetTileRect(TileType tile, int frameIndex = 0)
    {
        if (!_tileRects.TryGetValue(tile, out var baseRect))
            return new Rectangle(0, 0, TileSize, TileSize);
        
        // For animated tiles, offset horizontally by frame index
        if (TileProperties.IsAnimated(tile) && frameIndex > 0)
        {
            return new Rectangle(baseRect.X + frameIndex * TileSize, baseRect.Y, TileSize, TileSize);
        }
        
        return baseRect;
    }
    
    private static Color Darken(Color color, float amount)
    {
        return new Color(
            (int)(color.R * (1 - amount)),
            (int)(color.G * (1 - amount)),
            (int)(color.B * (1 - amount)),
            color.A
        );
    }
    
    private static Color Lighten(Color color, float amount)
    {
        return new Color(
            Math.Min(255, (int)(color.R + (255 - color.R) * amount)),
            Math.Min(255, (int)(color.G + (255 - color.G) * amount)),
            Math.Min(255, (int)(color.B + (255 - color.B) * amount)),
            color.A
        );
    }
    
    private static Color AddNoise(Color color, float noise)
    {
        int r = Math.Clamp((int)(color.R + noise * 255), 0, 255);
        int g = Math.Clamp((int)(color.G + noise * 255), 0, 255);
        int b = Math.Clamp((int)(color.B + noise * 255), 0, 255);
        return new Color(r, g, b, color.A);
    }
    
    public static void Dispose()
    {
        TileAtlas?.Dispose();
        PlayerSprite?.Dispose();
        PlayerSpriteSheet?.Dispose();
        ParticleTexture?.Dispose();
        Pixel?.Dispose();
        foreach (var layer in _parallaxLayers)
            layer?.Dispose();
        UISlot?.Dispose();
        UISelectedSlot?.Dispose();
        UIHotbar?.Dispose();
        foreach (var icon in _itemIcons.Values)
            icon?.Dispose();
    }
    
    #region Item Icons (32x32)
    private static void GenerateItemIcons()
    {
        _itemIcons.Clear();
        Directory.CreateDirectory(Path.Combine(GeneratedDir, "Icons"));
        
        // Palette inspired by the provided screenshot
        var wood = new Color(171, 126, 80);
        var woodDark = new Color(120, 82, 52);
        var stone = new Color(132, 132, 132);
        var metal = new Color(180, 190, 200);
        var gold = new Color(234, 204, 72);
        var copper = new Color(198, 124, 76);
        var diamond = new Color(140, 220, 230);
        var leather = new Color(166, 118, 74);
        var cloth = new Color(210, 190, 150);
        var greenLeaf = new Color(54, 146, 84);
        var brownTrunk = new Color(150, 110, 70);
        var torchFlame = new Color(255, 170, 70);
        
        // Tools
        SaveIcon("wood_pickaxe", GeneratePickaxeIcon(wood, woodDark));
        SaveIcon("stone_pickaxe", GeneratePickaxeIcon(stone, woodDark));
        SaveIcon("metal_pickaxe", GeneratePickaxeIcon(metal, woodDark));
        SaveIcon("wood_axe", GenerateAxeIcon(wood, woodDark));
        SaveIcon("stone_axe", GenerateAxeIcon(stone, woodDark));
        SaveIcon("metal_axe", GenerateAxeIcon(metal, woodDark));
        
        // Armor sets
        SaveIcon("wood_helm", GenerateArmorIcon(wood, "helm"));
        SaveIcon("wood_chest", GenerateArmorIcon(wood, "chest"));
        SaveIcon("wood_boots", GenerateArmorIcon(wood, "boots"));
        SaveIcon("wood_belt", GenerateArmorIcon(wood, "belt"));
        
        SaveIcon("stone_helm", GenerateArmorIcon(stone, "helm"));
        SaveIcon("stone_chest", GenerateArmorIcon(stone, "chest"));
        SaveIcon("stone_boots", GenerateArmorIcon(stone, "boots"));
        SaveIcon("stone_belt", GenerateArmorIcon(stone, "belt"));
        
        SaveIcon("metal_helm", GenerateArmorIcon(metal, "helm"));
        SaveIcon("metal_chest", GenerateArmorIcon(metal, "chest"));
        SaveIcon("metal_boots", GenerateArmorIcon(metal, "boots"));
        SaveIcon("metal_belt", GenerateArmorIcon(metal, "belt"));
        
        // Furniture / placeables
        SaveIcon("chair", GenerateFurnitureIcon(wood, "chair"));
        SaveIcon("table", GenerateFurnitureIcon(wood, "table"));
        SaveIcon("chest", GenerateChestIcon(wood, woodDark, metal));
        SaveIcon("door", GenerateFurnitureIcon(wood, "door"));
        SaveIcon("shelf", GenerateFurnitureIcon(wood, "shelf"));
        SaveIcon("torch", GenerateTorchIcon(woodDark, torchFlame));
        SaveIcon("tree", GenerateTreeIcon(greenLeaf, brownTrunk));
        SaveIcon("bush", GenerateBushIcon(greenLeaf));
        SaveIcon("furnace", GenerateFurnaceIcon(stone, metal));
        SaveIcon("anvil", GenerateAnvilIcon(metal));
        SaveIcon("wood_log", GenerateLogIcon(wood, woodDark));
        
        // Resources
        SaveIcon("gold_ore", GenerateOreIcon(gold, stone));
        SaveIcon("copper_ore", GenerateOreIcon(copper, stone));
        SaveIcon("iron_ore", GenerateOreIcon(metal, stone));
        SaveIcon("diamond_ore", GenerateOreIcon(diamond, stone));
        
        // Items
        SaveIcon("bottle", GenerateBottleIcon(cloth));
        SaveIcon("potion", GeneratePotionIcon(new Color(220, 60, 120)));
        
        // Map to existing ItemTypes where possible
        MapIcon(ItemType.Torch, "torch");
        MapIcon(ItemType.Chest, "chest");
        MapIcon(ItemType.CraftingTable, "table");
        MapIcon(ItemType.Furnace, "furnace");
        MapIcon(ItemType.Anvil, "anvil");
        MapIcon(ItemType.Wood, "wood_log");
        MapIcon(ItemType.Stone, "stone_pickaxe"); // rough stone icon
        MapIcon(ItemType.GoldOre, "gold_ore");
        MapIcon(ItemType.CopperOre, "copper_ore");
        MapIcon(ItemType.IronOre, "iron_ore");
        MapIcon(ItemType.Diamond, "diamond_ore");
        MapIcon(ItemType.WoodPlatform, "shelf");
    }
    
    private static void MapIcon(ItemType type, string name)
    {
        var path = Path.Combine(GeneratedDir, "Icons", $"{name}.png");
        if (!File.Exists(path)) return;
        using var fs = File.OpenRead(path);
        var tex = Texture2D.FromStream(_graphicsDevice, fs);
        _itemIcons[type] = tex;
    }
    
    private static void SaveIcon(string name, Texture2D tex)
    {
        var path = Path.Combine(GeneratedDir, "Icons", $"{name}.png");
        SaveTexture(tex, path);
        tex.Dispose();
    }
    
    private static Texture2D CreateBlankIcon(Color? background = null)
    {
        int size = 32;
        var tex = new Texture2D(_graphicsDevice, size, size);
        Color[] data = Enumerable.Repeat(background ?? Color.Transparent, size * size).ToArray();
        tex.SetData(data);
        return tex;
    }
    
    private static void SetPixel(Color[] data, int size, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return;
        data[y * size + x] = color;
    }
    
    private static void FillRect(Color[] data, int size, int x, int y, int w, int h, Color color)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                SetPixel(data, size, xx, yy, color);
    }
    
    private static Texture2D GeneratePickaxeIcon(Color head, Color handle)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        // Handle (diagonal)
        for (int i = 8; i < 26; i++)
        {
            SetPixel(data, size, i, 26 - (i - 8) / 2, handle);
            SetPixel(data, size, i, 27 - (i - 8) / 2, handle);
        }
        
        // Head
        FillRect(data, size, 8, 8, 16, 3, head);
        FillRect(data, size, 12, 11, 8, 3, head);
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateAxeIcon(Color head, Color handle)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        // Handle
        for (int i = 6; i < 26; i++)
        {
            SetPixel(data, size, i, 24 - (i - 6) / 2, handle);
            SetPixel(data, size, i, 25 - (i - 6) / 2, handle);
        }
        
        // Head (curved)
        FillRect(data, size, 10, 8, 6, 10, head);
        FillRect(data, size, 16, 9, 3, 8, head);
        FillRect(data, size, 19, 10, 2, 6, head);
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateArmorIcon(Color color, string part)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        switch (part)
        {
            case "helm":
                FillRect(data, size, 8, 8, 16, 10, color);
                FillRect(data, size, 10, 18, 12, 4, color);
                break;
            case "chest":
                FillRect(data, size, 8, 10, 16, 14, color);
                FillRect(data, size, 6, 12, 4, 10, Color.Lerp(color, Color.Black, 0.2f));
                FillRect(data, size, 22, 12, 4, 10, Color.Lerp(color, Color.Black, 0.2f));
                break;
            case "boots":
                FillRect(data, size, 10, 20, 12, 6, color);
                FillRect(data, size, 8, 26, 16, 4, Color.Lerp(color, Color.Black, 0.15f));
                break;
            case "belt":
                FillRect(data, size, 6, 16, 20, 6, color);
                FillRect(data, size, 10, 18, 12, 2, Color.Lerp(color, Color.Black, 0.2f));
                break;
        }
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateFurnitureIcon(Color color, string kind)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        switch (kind)
        {
            case "chair":
                FillRect(data, size, 10, 16, 4, 12, color);
                FillRect(data, size, 18, 16, 4, 12, color);
                FillRect(data, size, 8, 12, 16, 4, color);
                FillRect(data, size, 12, 6, 8, 8, color);
                break;
            case "table":
                FillRect(data, size, 6, 12, 20, 4, color);
                FillRect(data, size, 8, 16, 4, 12, color);
                FillRect(data, size, 20, 16, 4, 12, color);
                break;
            case "door":
                FillRect(data, size, 10, 6, 12, 22, color);
                FillRect(data, size, 12, 10, 8, 2, Color.Lerp(color, Color.Black, 0.2f));
                FillRect(data, size, 12, 18, 8, 2, Color.Lerp(color, Color.Black, 0.2f));
                break;
            case "shelf":
                FillRect(data, size, 6, 12, 20, 4, color);
                FillRect(data, size, 6, 20, 20, 4, Color.Lerp(color, Color.Black, 0.2f));
                break;
        }
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateChestIcon(Color body, Color dark, Color metal)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        FillRect(data, size, 6, 10, 20, 14, body);
        FillRect(data, size, 6, 22, 20, 4, dark);
        FillRect(data, size, 6, 8, 20, 4, Color.Lerp(body, Color.White, 0.1f));
        FillRect(data, size, 14, 16, 4, 6, metal);
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateTorchIcon(Color stick, Color flame)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        FillRect(data, size, 14, 16, 4, 10, stick);
        FillRect(data, size, 12, 10, 8, 6, flame);
        FillRect(data, size, 13, 8, 6, 3, Color.Lerp(flame, Color.White, 0.3f));
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateTreeIcon(Color leaves, Color trunk)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        // Trunk
        FillRect(data, size, 14, 14, 4, 12, trunk);
        // Leaves blob
        FillRect(data, size, 8, 4, 16, 10, leaves);
        FillRect(data, size, 10, 2, 12, 4, Color.Lerp(leaves, Color.White, 0.1f));
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateBushIcon(Color leaves)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        FillRect(data, size, 6, 14, 20, 8, leaves);
        FillRect(data, size, 8, 10, 16, 6, Color.Lerp(leaves, Color.White, 0.1f));
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateOreIcon(Color ore, Color stoneBg)
    {
        int size = 32;
        var tex = CreateBlankIcon(stoneBg);
        var data = new Color[size * size];
        for (int i = 0; i < data.Length; i++) data[i] = stoneBg;
        
        // Cluster dots
        var rnd = new Random(42);
        for (int n = 0; n < 30; n++)
        {
            int x = rnd.Next(4, 28);
            int y = rnd.Next(4, 28);
            SetPixel(data, size, x, y, ore);
            if (rnd.NextDouble() > 0.6) SetPixel(data, size, x + 1, y, Color.Lerp(ore, Color.White, 0.2f));
        }
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateFurnaceIcon(Color stoneColor, Color metal)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        FillRect(data, size, 6, 8, 20, 18, stoneColor);
        FillRect(data, size, 8, 10, 16, 10, Color.Lerp(stoneColor, Color.Black, 0.2f));
        FillRect(data, size, 10, 20, 12, 4, metal);
        FillRect(data, size, 12, 14, 8, 4, Color.Lerp(metal, Color.Black, 0.3f));
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateAnvilIcon(Color metal)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        FillRect(data, size, 8, 18, 16, 6, metal);
        FillRect(data, size, 10, 12, 12, 6, Color.Lerp(metal, Color.Black, 0.15f));
        FillRect(data, size, 12, 8, 8, 4, Color.Lerp(metal, Color.White, 0.1f));
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateLogIcon(Color wood, Color dark)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        FillRect(data, size, 8, 8, 16, 16, wood);
        FillRect(data, size, 12, 10, 8, 12, Color.Lerp(wood, Color.White, 0.1f));
        FillRect(data, size, 8, 22, 16, 2, dark);
        FillRect(data, size, 8, 8, 2, 16, dark);
        FillRect(data, size, 22, 8, 2, 16, dark);
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GenerateBottleIcon(Color glass)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        FillRect(data, size, 12, 8, 8, 4, glass);
        FillRect(data, size, 10, 12, 12, 10, Color.Lerp(glass, Color.White, 0.2f));
        FillRect(data, size, 12, 22, 8, 4, glass);
        
        tex.SetData(data);
        return tex;
    }
    
    private static Texture2D GeneratePotionIcon(Color liquid)
    {
        int size = 32;
        var tex = CreateBlankIcon();
        var data = new Color[size * size];
        
        // Bottle
        var glass = new Color(200, 220, 230, 180);
        FillRect(data, size, 10, 10, 12, 12, glass);
        FillRect(data, size, 12, 8, 8, 4, glass);
        // Liquid
        FillRect(data, size, 12, 16, 8, 6, liquid);
        
        tex.SetData(data);
        return tex;
    }
    #endregion
}
