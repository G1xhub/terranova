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
    
    // Count for iteration
    Count = 30
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
        Set(TileType.Grass, true, 1f, TileType.Dirt, 0, "Grass");
        Set(TileType.Stone, true, 2f, TileType.Stone, 0, "Stone");
        Set(TileType.Sand, true, 0.5f, TileType.Sand, 0, "Sand", gravity: true);
        Set(TileType.Snow, true, 0.5f, TileType.Snow, 0, "Snow");
        
        Set(TileType.Wood, true, 1.5f, TileType.Wood, 0, "Wood");
        Set(TileType.Leaves, false, 0.3f, TileType.Air, 0, "Leaves");
        
        Set(TileType.CopperOre, true, 3f, TileType.CopperOre, 0, "Copper Ore");
        Set(TileType.IronOre, true, 3.5f, TileType.IronOre, 0, "Iron Ore");
        Set(TileType.GoldOre, true, 4f, TileType.GoldOre, 0, "Gold Ore");
        Set(TileType.DiamondOre, true, 5f, TileType.DiamondOre, 0, "Diamond Ore");
        
        Set(TileType.Water, false, 0f, TileType.Air, 0, "Water", liquid: true);
        Set(TileType.Lava, false, 0f, TileType.Air, 8, "Lava", liquid: true);
        
        Set(TileType.Coal, true, 2f, TileType.Coal, 0, "Coal");
        Set(TileType.Bedrock, true, float.MaxValue, TileType.Air, 0, "Bedrock");
        
        Set(TileType.Torch, false, 0f, TileType.Torch, 12, "Torch");
        Set(TileType.CraftingTable, true, 1.5f, TileType.CraftingTable, 0, "Crafting Table");
        Set(TileType.Chest, true, 1f, TileType.Chest, 0, "Chest");
        Set(TileType.Furnace, true, 2f, TileType.Furnace, 8, "Furnace");
        Set(TileType.Anvil, true, 3f, TileType.Anvil, 0, "Anvil");
        
        Set(TileType.Brick, true, 2f, TileType.Brick, 0, "Brick");
        Set(TileType.WoodPlatform, false, 0.5f, TileType.WoodPlatform, 0, "Wood Platform", platform: true);
        
        Set(TileType.Mud, true, 1f, TileType.Mud, 0, "Mud");
        Set(TileType.JungleGrass, true, 1f, TileType.Mud, 0, "Jungle Grass");
        Set(TileType.Ash, true, 1f, TileType.Ash, 0, "Ash");
        Set(TileType.Obsidian, true, 6f, TileType.Obsidian, 0, "Obsidian");
        
        Set(TileType.Cobalt, true, 4f, TileType.Cobalt, 0, "Cobalt Ore");
        Set(TileType.Mythril, true, 4.5f, TileType.Mythril, 0, "Mythril Ore");
        Set(TileType.Adamantite, true, 5f, TileType.Adamantite, 0, "Adamantite Ore");
    }
    
    private static void Set(TileType type, bool solid, float hardness, TileType drops, 
        int lightLevel, string name, bool liquid = false, bool platform = false, bool gravity = false)
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
            HasGravity = gravity
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
}
