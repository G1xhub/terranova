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
    private const string ParticleFile = "particle.png";
    private const string ParallaxFileFormat = "parallax_{0}.png";
    private const string UISlotFile = "ui_slot.png";
    private const string UISelectedSlotFile = "ui_slot_selected.png";
    private const string UIHotbarFile = "ui_hotbar.png";
    
    // Main textures
    public static Texture2D TileAtlas { get; private set; } = null!;
    public static Texture2D PlayerSprite { get; private set; } = null!;
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
                        pixelColor = GenerateDirtPixel(x, y, baseColor, darkColor, lightColor);
                        break;
                    case TileType.Stone:
                        pixelColor = GenerateStonePixel(x, y, baseColor, darkColor, lightColor);
                        break;
                    case TileType.Sand:
                        pixelColor = GenerateSandPixel(x, y, baseColor, darkColor, lightColor);
                        break;
                    case TileType.Snow:
                        pixelColor = GenerateSnowPixel(x, y, baseColor, darkColor, highlightColor);
                        break;
                    case TileType.Wood:
                        pixelColor = GenerateWoodPixel(x, y, baseColor, darkColor, lightColor);
                        break;
                    case TileType.Leaves:
                        pixelColor = GenerateLeavesPixel(x, y, baseColor, darkColor, lightColor);
                        break;
                    case TileType.CopperOre:
                    case TileType.IronOre:
                    case TileType.GoldOre:
                    case TileType.DiamondOre:
                    case TileType.Coal:
                        pixelColor = GenerateOrePixel(x, y, tile, baseColor);
                        break;
                    case TileType.Water:
                        pixelColor = GenerateWaterPixel(x, y, baseColor);
                        break;
                    case TileType.Lava:
                        pixelColor = GenerateLavaPixel(x, y, baseColor);
                        break;
                    case TileType.Torch:
                        pixelColor = GenerateTorchPixel(x, y);
                        break;
                    default:
                        pixelColor = GenerateDefaultPixel(x, y, baseColor, darkColor, lightColor);
                        break;
                }
                
                data[(startY + y) * atlasSize + (startX + x)] = pixelColor;
            }
        }
    }
    
    private static Color GenerateGrassPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor)
    {
        // Top part is grass, bottom is dirt
        if (y < 4)
        {
            // Grass blades at top - clearer definition
            if (y == 0)
            {
                // Individual grass blades with better contrast
                if ((x + y * 2) % 3 == 0 && _random.NextDouble() > 0.4)
                    return Lighten(baseColor, 0.4f);
                if ((x * 2 + y) % 4 == 0 && _random.NextDouble() > 0.6)
                    return Darken(baseColor, 0.2f);
            }
            if (y < 3)
            {
                // More structured variation
                float noise = (float)(_random.NextDouble() * 0.4 - 0.2);
                var color = AddNoise(baseColor, noise);
                // Add subtle highlights
                if ((x + y) % 5 == 0 && _random.NextDouble() > 0.7)
                    color = Lighten(color, 0.15f);
                return color;
            }
            return baseColor;
        }
        else
        {
            // Dirt underneath - clearer transition
            var dirtColor = new Color(139, 90, 43);
            float noise = (float)(_random.NextDouble() * 0.3 - 0.15);
            var color = AddNoise(dirtColor, noise);
            // Add pebbles for texture
            if (_random.NextDouble() > 0.9)
                color = Darken(color, 0.15f);
            return color;
        }
    }
    
    private static Color GenerateDirtPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor)
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
    
    private static Color GenerateStonePixel(int x, int y, Color baseColor, Color darkColor, Color lightColor)
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
    
    private static Color GenerateSandPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor)
    {
        float noise = (float)(_random.NextDouble() * 0.2 - 0.1);
        var color = AddNoise(baseColor, noise);
        
        // More defined sandy specks with better contrast
        if (_random.NextDouble() > 0.85)
            color = Lighten(color, 0.25f);
        if (_random.NextDouble() > 0.92)
            color = Darken(color, 0.15f);
        
        // Subtle wave pattern
        if ((x + y * 2) % 6 == 0 && _random.NextDouble() > 0.7)
            color = Lighten(color, 0.1f);
            
        return color;
    }
    
    private static Color GenerateSnowPixel(int x, int y, Color baseColor, Color darkColor, Color highlightColor)
    {
        float noise = (float)(_random.NextDouble() * 0.15 - 0.075);
        var color = AddNoise(baseColor, noise);
        
        // Enhanced sparkle effect with better contrast
        if (_random.NextDouble() > 0.92)
            color = Color.White;
        if (_random.NextDouble() > 0.96)
            color = new Color(240, 250, 255); // Slight blue tint
        
        // Subtle shadows for depth
        if ((x + y) % 8 == 0 && _random.NextDouble() > 0.8)
            color = Darken(color, 0.05f);
            
        return color;
    }
    
    private static Color GenerateWoodPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor)
    {
        // Wood grain pattern (vertical lines) - clearer definition
        float grain = (float)Math.Sin(x * 0.8 + _random.NextDouble() * 0.5) * 0.2f;
        var color = AddNoise(baseColor, grain);
        
        // More defined wood rings
        if (x % 4 == 0)
            color = Darken(color, 0.15f);
        if (x % 8 == 0 && _random.NextDouble() > 0.6)
            color = Darken(color, 0.1f);
            
        // Clearer knots
        if (_random.NextDouble() > 0.95)
            color = Darken(baseColor, 0.4f);
        
        // Subtle highlights
        if ((x * 2 + y) % 7 == 0 && _random.NextDouble() > 0.75)
            color = Lighten(color, 0.1f);
            
        return color;
    }
    
    private static Color GenerateLeavesPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor)
    {
        // Leafy pattern with holes
        if (_random.NextDouble() > 0.85)
            return Color.Transparent;
            
        float noise = (float)(_random.NextDouble() * 0.3 - 0.15);
        return AddNoise(baseColor, noise);
    }
    
    private static Color GenerateOrePixel(int x, int y, TileType oreType, Color oreColor)
    {
        // Stone background with ore spots
        var stoneColor = new Color(105, 105, 105);
        float stoneNoise = (float)(_random.NextDouble() * 0.15 - 0.075);
        var color = AddNoise(stoneColor, stoneNoise);
        
        // Ore veins pattern
        bool isOre = false;
        float centerX = TileSize / 2f;
        float centerY = TileSize / 2f;
        float dist = (float)Math.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
        
        // Create clustered ore pattern
        if (dist < 5 + _random.NextDouble() * 3)
        {
            if (_random.NextDouble() > 0.3)
                isOre = true;
        }
        else if (_random.NextDouble() > 0.85)
        {
            isOre = true;
        }
        
        if (isOre)
        {
            float oreNoise = (float)(_random.NextDouble() * 0.2 - 0.1);
            color = AddNoise(oreColor, oreNoise);
            
            // Add shine for precious ores
            if (oreType == TileType.GoldOre || oreType == TileType.DiamondOre)
            {
                if (_random.NextDouble() > 0.8)
                    color = Lighten(color, 0.3f);
            }
        }
        
        return color;
    }
    
    private static Color GenerateWaterPixel(int x, int y, Color baseColor)
    {
        // Animated wave-like pattern
        float wave = (float)Math.Sin(x * 0.5 + y * 0.3) * 0.1f;
        var color = AddNoise(baseColor, wave);
        color.A = 180; // Semi-transparent
        return color;
    }
    
    private static Color GenerateLavaPixel(int x, int y, Color baseColor)
    {
        // Glowing lava pattern
        float glow = (float)(Math.Sin(x * 0.4) * Math.Cos(y * 0.4) * 0.2);
        var color = AddNoise(baseColor, glow);
        
        // Hot spots
        if (_random.NextDouble() > 0.9)
            color = new Color(255, 255, 100);
            
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
    
    private static Color GenerateDefaultPixel(int x, int y, Color baseColor, Color darkColor, Color lightColor)
    {
        float noise = (float)(_random.NextDouble() * 0.15 - 0.075);
        var color = AddNoise(baseColor, noise);
        
        // Simple 3D effect
        if (x == 0 || y == 0)
            color = Lighten(color, 0.15f);
        if (x == TileSize - 1 || y == TileSize - 1)
            color = Darken(color, 0.15f);
            
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
            TileType.Wood => new Color(168, 122, 81),
            TileType.Leaves => new Color(50, 130, 50),
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
        
        // Load more layers for better depth (5 layers instead of 3)
        for (int i = 0; i < 5; i++)
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
    
    public static Rectangle GetTileRect(TileType tile)
    {
        return _tileRects.TryGetValue(tile, out var rect) ? rect : new Rectangle(0, 0, TileSize, TileSize);
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
