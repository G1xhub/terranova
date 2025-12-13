namespace TerraNova.World;

/// <summary>
/// All tile types in the game
/// The numeric value corresponds to the position in the tile atlas
/// </summary>
public enum TileType : byte
{
    // Basic
    Air = 0,
    Dirt = 1,
    Grass = 2,
    Stone = 3,
    Sand = 4,
    Snow = 5,
    
    // Natural
    Wood = 6,
    Leaves = 7,
    
    // Ores
    CopperOre = 8,
    IronOre = 9,
    GoldOre = 10,
    DiamondOre = 11,
    
    // Liquids
    Water = 12,
    Lava = 13,
    
    // Underground
    Coal = 14,
    Bedrock = 15,
    
    // Placed
    Torch = 16,
    CraftingTable = 17,
    Chest = 18,
    Furnace = 19,
    Anvil = 20,
    
    // Decorative
    Brick = 21,
    WoodPlatform = 22,
    
    // Biome specific
    Mud = 23,
    JungleGrass = 24,
    Ash = 25,
    Obsidian = 26,
    
    // Hardmode
    Cobalt = 27,
    Mythril = 28,
    Adamantite = 29,
    
    // Crystals and Mushrooms
    Crystal = 30,
    BlueCrystal = 31,
    RedCrystal = 32,
    Mushroom = 33,
    GlowingMushroom = 34,
    
    // Count for iteration
    Count = 35
}

/// <summary>
/// Properties for each tile type
/// </summary>
public static class TileProperties
{
    private static readonly TileData[] _data = new TileData[(int)TileType.Count];
    
    static TileProperties()
    {
        // Initialize all tile data
        Set(TileType.Air, false, 0f, TileType.Air, 0, "Air");
        Set(TileType.Dirt, true, 1f, TileType.Dirt, 0, "Dirt");
        Set(TileType.Grass, true, 1f, TileType.Dirt, 0, "Grass", animated: true);
        Set(TileType.Stone, true, 2f, TileType.Stone, 0, "Stone");
        Set(TileType.Sand, true, 0.5f, TileType.Sand, 0, "Sand", gravity: true);
        Set(TileType.Snow, true, 0.5f, TileType.Snow, 0, "Snow");
        
        Set(TileType.Wood, true, 1.5f, TileType.Wood, 0, "Wood");
        Set(TileType.Leaves, false, 0.3f, TileType.Air, 0, "Leaves", animated: true);
        
        Set(TileType.CopperOre, true, 3f, TileType.CopperOre, 0, "Copper Ore", reflectivity: 0.3f);
        Set(TileType.IronOre, true, 3.5f, TileType.IronOre, 0, "Iron Ore", reflectivity: 0.4f);
        Set(TileType.GoldOre, true, 4f, TileType.GoldOre, 0, "Gold Ore", reflectivity: 0.6f);
        Set(TileType.DiamondOre, true, 5f, TileType.DiamondOre, 0, "Diamond Ore", reflectivity: 0.7f);
        
        Set(TileType.Water, false, 0f, TileType.Air, 0, "Water", liquid: true, reflectivity: 0.5f, animated: true);
        Set(TileType.Lava, false, 0f, TileType.Air, 8, "Lava", liquid: true, animated: true);
        
        Set(TileType.Coal, true, 2f, TileType.Coal, 0, "Coal");
        Set(TileType.Bedrock, true, float.MaxValue, TileType.Air, 0, "Bedrock");
        
        Set(TileType.Torch, false, 0f, TileType.Torch, 15, "Torch"); // Increased light level for better range
        Set(TileType.CraftingTable, true, 1.5f, TileType.CraftingTable, 0, "Crafting Table");
        Set(TileType.Chest, true, 1f, TileType.Chest, 0, "Chest");
        Set(TileType.Furnace, true, 2f, TileType.Furnace, 8, "Furnace", reflectivity: 0.2f);
        Set(TileType.Anvil, true, 3f, TileType.Anvil, 0, "Anvil", reflectivity: 0.5f);
        
        Set(TileType.Brick, true, 2f, TileType.Brick, 0, "Brick", reflectivity: 0.15f);
        Set(TileType.WoodPlatform, false, 0.5f, TileType.WoodPlatform, 0, "Wood Platform", platform: true);
        
        Set(TileType.Mud, true, 1f, TileType.Mud, 0, "Mud");
        Set(TileType.JungleGrass, true, 1f, TileType.Mud, 0, "Jungle Grass");
        Set(TileType.Ash, true, 1f, TileType.Ash, 0, "Ash");
        Set(TileType.Obsidian, true, 6f, TileType.Obsidian, 0, "Obsidian");
        
        Set(TileType.Cobalt, true, 4f, TileType.Cobalt, 0, "Cobalt Ore", reflectivity: 0.5f);
        Set(TileType.Mythril, true, 4.5f, TileType.Mythril, 0, "Mythril Ore", reflectivity: 0.6f);
        Set(TileType.Adamantite, true, 5f, TileType.Adamantite, 0, "Adamantite Ore", reflectivity: 0.7f);
        
        // Crystals - emit light and can be harvested (drop themselves as items)
        Set(TileType.Crystal, false, 0.5f, TileType.Crystal, 5, "Crystal"); // Emits light
        Set(TileType.BlueCrystal, false, 0.5f, TileType.BlueCrystal, 6, "Blue Crystal", reflectivity: 0.3f); // Emits blue light
        Set(TileType.RedCrystal, false, 0.5f, TileType.RedCrystal, 6, "Red Crystal", reflectivity: 0.3f); // Emits red light
        
        // Mushrooms - can be harvested for brewing (drop themselves as items)
        Set(TileType.Mushroom, false, 0.3f, TileType.Mushroom, 0, "Mushroom");
        Set(TileType.GlowingMushroom, false, 0.3f, TileType.GlowingMushroom, 4, "Glowing Mushroom"); // Emits soft light
    }
    
    private static void Set(TileType type, bool solid, float hardness, TileType drops, 
        int lightLevel, string name, bool liquid = false, bool platform = false, bool gravity = false, float reflectivity = 0f, bool animated = false)
    {
        _data[(int)type] = new TileData
        {
            IsSolid = solid,
            Hardness = hardness,
            Drops = drops,
            LightLevel = lightLevel,
            Name = name,
            IsLiquid = liquid,
            IsPlatform = platform,
            HasGravity = gravity,
            Reflectivity = reflectivity,
            IsAnimated = animated
        };
    }
    
    public static TileData Get(TileType type)
    {
        int index = (int)type;
        if (index >= 0 && index < _data.Length)
            return _data[index];
        return _data[0]; // Return Air data as fallback
    }
    
    public static bool IsSolid(TileType type) => Get(type).IsSolid;
    public static bool IsLiquid(TileType type) => Get(type).IsLiquid;
    public static bool IsPlatform(TileType type) => Get(type).IsPlatform;
    public static bool HasGravity(TileType type) => Get(type).HasGravity;
    public static float GetHardness(TileType type) => Get(type).Hardness;
    public static int GetLightLevel(TileType type) => Get(type).LightLevel;
    public static TileType GetDrop(TileType type) => Get(type).Drops;
    public static string GetName(TileType type) => Get(type).Name;
    public static float GetReflectivity(TileType type) => Get(type).Reflectivity;
    public static bool IsAnimated(TileType type) => Get(type).IsAnimated;
}

public struct TileData
{
    public bool IsSolid;
    public bool IsLiquid;
    public bool IsPlatform;
    public bool HasGravity;
    public float Hardness;
    public TileType Drops;
    public int LightLevel;
    public string Name;
    public float Reflectivity; // 0.0 = no reflection, 1.0 = perfect mirror
    public bool IsAnimated; // Whether this tile has animation frames
}
