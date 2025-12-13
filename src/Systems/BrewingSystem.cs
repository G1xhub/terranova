using TerraNova.Entities;

namespace TerraNova.Systems;

/// <summary>
/// Brewing/Alchemy system for crafting potions from crystals and mushrooms
/// </summary>
public static class BrewingSystem
{
    private static readonly List<BrewingRecipe> _recipes = new();
    
    static BrewingSystem()
    {
        InitializeRecipes();
    }
    
    private static void InitializeRecipes()
    {
        // Regeneration Potion - heals over time
        AddRecipe(new BrewingRecipe(
            ItemType.RegenerationPotion,
            new[] { (ItemType.Crystal, 1), (ItemType.Mushroom, 2) }
        ));
        
        // Speed Potion - increases movement speed
        AddRecipe(new BrewingRecipe(
            ItemType.SpeedPotion,
            new[] { (ItemType.BlueCrystal, 1), (ItemType.Mushroom, 1) }
        ));
        
        // Strength Potion - increases damage
        AddRecipe(new BrewingRecipe(
            ItemType.StrengthPotion,
            new[] { (ItemType.RedCrystal, 1), (ItemType.Mushroom, 2) }
        ));
        
        // Night Vision Potion - increases light vision
        AddRecipe(new BrewingRecipe(
            ItemType.NightVisionPotion,
            new[] { (ItemType.GlowingMushroom, 1), (ItemType.Crystal, 1) }
        ));
    }
    
    private static void AddRecipe(BrewingRecipe recipe)
    {
        _recipes.Add(recipe);
    }
    
    /// <summary>
    /// Get all recipes that can be brewed with current inventory
    /// </summary>
    public static IEnumerable<BrewingRecipe> GetAvailableRecipes(Inventory inventory)
    {
        return _recipes.Where(recipe => recipe.CanBrew(inventory));
    }
    
    /// <summary>
    /// Get all brewing recipes
    /// </summary>
    public static IEnumerable<BrewingRecipe> GetAllRecipes()
    {
        return _recipes;
    }
    
    /// <summary>
    /// Get recipe for a specific potion type
    /// </summary>
    public static BrewingRecipe? GetRecipe(ItemType result)
    {
        return _recipes.FirstOrDefault(r => r.Result == result);
    }
    
    /// <summary>
    /// Try to brew a potion
    /// </summary>
    public static bool TryBrew(ItemType result, Inventory inventory)
    {
        var recipe = GetRecipe(result);
        if (recipe == null) return false;
        
        if (!recipe.CanBrew(inventory))
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
/// A brewing recipe for potions
/// </summary>
public class BrewingRecipe
{
    public ItemType Result { get; }
    public IReadOnlyList<(ItemType item, int amount)> Ingredients { get; }
    public int Amount { get; }
    
    public BrewingRecipe(
        ItemType result,
        (ItemType item, int amount)[] ingredients,
        int amount = 1)
    {
        Result = result;
        Ingredients = ingredients;
        Amount = amount;
    }
    
    public bool CanBrew(Inventory inventory)
    {
        // Check ingredients
        foreach (var (ingredient, amount) in Ingredients)
        {
            if (!inventory.HasItem(ingredient, amount))
                return false;
        }
        
        return true;
    }
}

