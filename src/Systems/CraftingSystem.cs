using TerraNova.Entities;

namespace TerraNova.Systems;

/// <summary>
/// Crafting system with recipes
/// </summary>
public static class CraftingSystem
{
    private static readonly List<CraftingRecipe> _recipes = new();
    
    static CraftingSystem()
    {
        InitializeRecipes();
    }
    
    private static void InitializeRecipes()
    {
        // Basic tools - can craft anywhere
        AddRecipe(new CraftingRecipe(
            ItemType.CopperPickaxe,
            new[] { (ItemType.CopperBar, 12), (ItemType.Wood, 4) }
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.CopperAxe,
            new[] { (ItemType.CopperBar, 9), (ItemType.Wood, 3) }
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.CopperSword,
            new[] { (ItemType.CopperBar, 8) }
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.WoodenBow,
            new[] { (ItemType.Wood, 10) }
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.WoodenArrow,
            new[] { (ItemType.Wood, 1), (ItemType.Stone, 1) },
            amount: 5
        ));
        
        // Requires Crafting Table
        AddRecipe(new CraftingRecipe(
            ItemType.CraftingTable,
            new[] { (ItemType.Wood, 10) },
            requiresCraftingTable: false // Can craft first one by hand
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.Chest,
            new[] { (ItemType.Wood, 8), (ItemType.IronBar, 2) },
            requiresCraftingTable: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.Torch,
            new[] { (ItemType.Wood, 1), (ItemType.Coal, 1) },
            amount: 3,
            requiresCraftingTable: false
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.WoodPlatform,
            new[] { (ItemType.Wood, 1) },
            amount: 2,
            requiresCraftingTable: true
        ));
        
        // Requires Furnace
        AddRecipe(new CraftingRecipe(
            ItemType.Furnace,
            new[] { (ItemType.Stone, 20), (ItemType.Wood, 4), (ItemType.Coal, 3) },
            requiresCraftingTable: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.CopperBar,
            new[] { (ItemType.CopperOre, 3), (ItemType.Coal, 1) },
            requiresFurnace: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.IronBar,
            new[] { (ItemType.IronOre, 3), (ItemType.Coal, 1) },
            requiresFurnace: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.GoldBar,
            new[] { (ItemType.GoldOre, 3), (ItemType.Coal, 1) },
            requiresFurnace: true
        ));
        
        // Requires Anvil
        AddRecipe(new CraftingRecipe(
            ItemType.Anvil,
            new[] { (ItemType.IronBar, 5) },
            requiresCraftingTable: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.IronPickaxe,
            new[] { (ItemType.IronBar, 12), (ItemType.Wood, 4) },
            requiresAnvil: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.IronAxe,
            new[] { (ItemType.IronBar, 9), (ItemType.Wood, 3) },
            requiresAnvil: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.IronSword,
            new[] { (ItemType.IronBar, 8) },
            requiresAnvil: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.GoldPickaxe,
            new[] { (ItemType.GoldBar, 12), (ItemType.Wood, 4) },
            requiresAnvil: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.GoldSword,
            new[] { (ItemType.GoldBar, 8) },
            requiresAnvil: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.DiamondPickaxe,
            new[] { (ItemType.Diamond, 15), (ItemType.GoldBar, 12), (ItemType.Wood, 4) },
            requiresAnvil: true
        ));
        
        AddRecipe(new CraftingRecipe(
            ItemType.DiamondSword,
            new[] { (ItemType.Diamond, 10), (ItemType.GoldBar, 8) },
            requiresAnvil: true
        ));
    }
    
    private static void AddRecipe(CraftingRecipe recipe)
    {
        _recipes.Add(recipe);
    }
    
    /// <summary>
    /// Get all recipes that can be crafted with current inventory and nearby stations
    /// </summary>
    public static IEnumerable<CraftingRecipe> GetAvailableRecipes(Inventory inventory, bool hasCraftingTable, bool hasFurnace, bool hasAnvil)
    {
        return _recipes.Where(recipe => 
            recipe.CanCraft(inventory, hasCraftingTable, hasFurnace, hasAnvil)
        );
    }
    
    /// <summary>
    /// Get all recipes
    /// </summary>
    public static IEnumerable<CraftingRecipe> GetAllRecipes()
    {
        return _recipes;
    }
    
    /// <summary>
    /// Get recipe for a specific item type
    /// </summary>
    public static CraftingRecipe? GetRecipe(ItemType result)
    {
        return _recipes.FirstOrDefault(r => r.Result == result);
    }
    
    /// <summary>
    /// Try to craft an item
    /// </summary>
    public static bool TryCraft(ItemType result, Inventory inventory, bool hasCraftingTable, bool hasFurnace, bool hasAnvil)
    {
        var recipe = GetRecipe(result);
        if (recipe == null) return false;
        
        if (!recipe.CanCraft(inventory, hasCraftingTable, hasFurnace, hasAnvil))
            return false;
        
        // Consume ingredients
        foreach (var (ingredient, amount) in recipe.Ingredients)
        {
            inventory.ConsumeItem(ingredient, amount);
        }
        
        // Add result
        inventory.AddItem(new Item(result, recipe.Amount));
        
        return true;
    }
}

/// <summary>
/// A crafting recipe
/// </summary>
public class CraftingRecipe
{
    public ItemType Result { get; }
    public IReadOnlyList<(ItemType item, int amount)> Ingredients { get; }
    public int Amount { get; }
    public bool RequiresCraftingTable { get; }
    public bool RequiresFurnace { get; }
    public bool RequiresAnvil { get; }
    
    public CraftingRecipe(
        ItemType result,
        (ItemType item, int amount)[] ingredients,
        int amount = 1,
        bool requiresCraftingTable = false,
        bool requiresFurnace = false,
        bool requiresAnvil = false)
    {
        Result = result;
        Ingredients = ingredients;
        Amount = amount;
        RequiresCraftingTable = requiresCraftingTable;
        RequiresFurnace = requiresFurnace;
        RequiresAnvil = requiresAnvil;
    }
    
    public bool CanCraft(Inventory inventory, bool hasCraftingTable, bool hasFurnace, bool hasAnvil)
    {
        // Check station requirements
        if (RequiresCraftingTable && !hasCraftingTable) return false;
        if (RequiresFurnace && !hasFurnace) return false;
        if (RequiresAnvil && !hasAnvil) return false;
        
        // Check ingredients
        foreach (var (ingredient, amount) in Ingredients)
        {
            if (!inventory.HasItem(ingredient, amount))
                return false;
        }
        
        return true;
    }
}




