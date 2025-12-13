namespace TerraNova.Entities;

/// <summary>
/// Represents an item in the game
/// </summary>
public class Item
{
    public ItemType Type { get; }
    public int Count { get; set; }
    public int MaxStack => ItemProperties.GetMaxStack(Type);
    
    public Item(ItemType type, int count = 1)
    {
        Type = type;
        Count = Math.Min(count, MaxStack);
    }
    
    public Item Clone()
    {
        return new Item(Type, Count);
    }
    
    public bool CanStackWith(Item other)
    {
        return other.Type == Type && Count < MaxStack;
    }
}

/// <summary>
/// All item types in the game
/// </summary>
public enum ItemType
{
    None = 0,
    
    // Materials
    Dirt = 1,
    Stone = 2,
    Sand = 3,
    Snow = 4,
    Wood = 5,
    Mud = 6,
    Ash = 7,
    
    // Ores
    CopperOre = 10,
    IronOre = 11,
    GoldOre = 12,
    Coal = 13,
    Diamond = 14,
    CobaltOre = 15,
    MythrilOre = 16,
    AdamantiteOre = 17,
    
    // Bars
    CopperBar = 20,
    IronBar = 21,
    GoldBar = 22,
    CobaltBar = 23,
    MythrilBar = 24,
    AdamantiteBar = 25,
    
    // Tools - Pickaxes
    CopperPickaxe = 100,
    IronPickaxe = 101,
    GoldPickaxe = 102,
    DiamondPickaxe = 103,
    CobaltPickaxe = 104,
    MythrilPickaxe = 105,
    
    // Tools - Axes
    CopperAxe = 110,
    IronAxe = 111,
    GoldAxe = 112,
    DiamondAxe = 113,
    
    // Tools - Hammers
    CopperHammer = 120,
    IronHammer = 121,
    GoldHammer = 122,
    
    // Weapons - Swords
    CopperSword = 200,
    IronSword = 201,
    GoldSword = 202,
    DiamondSword = 203,
    
    // Weapons - Bows
    WoodenBow = 210,
    IronBow = 211,
    GoldBow = 212,
    
    // Ammo
    WoodenArrow = 220,
    IronArrow = 221,
    
    // Placeable
    Torch = 300,
    CraftingTable = 301,
    Furnace = 302,
    Anvil = 303,
    Chest = 304,
    WoodPlatform = 305,
    
    // Consumables
    LesserHealingPotion = 400,
    HealingPotion = 401,
    GreaterHealingPotion = 402,
    ManaPotion = 410,
    
    // Accessories
    HermesBoots = 500,
    CloudInABottle = 501,
    LuckyHorseshoe = 502,
    
    // Misc
    Gel = 600,
    Lens = 601,
    Coin = 602,
}

/// <summary>
/// Properties for each item type
/// </summary>
public static class ItemProperties
{
    private static readonly Dictionary<ItemType, ItemData> _data = new();
    
    static ItemProperties()
    {
        // Materials
        Register(ItemType.Dirt, "Dirt", "Block", 999, true);
        Register(ItemType.Stone, "Stone", "Block", 999, true);
        Register(ItemType.Sand, "Sand", "Block", 999, true);
        Register(ItemType.Snow, "Snow", "Block", 999, true);
        Register(ItemType.Wood, "Wood", "Block", 999, true);
        
        // Ores
        Register(ItemType.CopperOre, "Copper Ore", "Ore", 999, true);
        Register(ItemType.IronOre, "Iron Ore", "Ore", 999, true);
        Register(ItemType.GoldOre, "Gold Ore", "Ore", 999, true);
        Register(ItemType.Coal, "Coal", "Ore", 999, false);
        Register(ItemType.Diamond, "Diamond", "Gem", 999, false);
        
        // Bars
        Register(ItemType.CopperBar, "Copper Bar", "Bar", 99);
        Register(ItemType.IronBar, "Iron Bar", "Bar", 99);
        Register(ItemType.GoldBar, "Gold Bar", "Bar", 99);
        
        // Tools
        Register(ItemType.CopperPickaxe, "Copper Pickaxe", "Tool", 1, damage: 5, useTime: 20);
        Register(ItemType.IronPickaxe, "Iron Pickaxe", "Tool", 1, damage: 7, useTime: 18);
        Register(ItemType.GoldPickaxe, "Gold Pickaxe", "Tool", 1, damage: 9, useTime: 16);
        Register(ItemType.DiamondPickaxe, "Diamond Pickaxe", "Tool", 1, damage: 12, useTime: 14);
        
        Register(ItemType.CopperAxe, "Copper Axe", "Tool", 1, damage: 6, useTime: 22);
        Register(ItemType.IronAxe, "Iron Axe", "Tool", 1, damage: 8, useTime: 20);
        
        // Weapons
        Register(ItemType.CopperSword, "Copper Sword", "Melee", 1, damage: 10, useTime: 25);
        Register(ItemType.IronSword, "Iron Sword", "Melee", 1, damage: 15, useTime: 22);
        Register(ItemType.GoldSword, "Gold Sword", "Melee", 1, damage: 20, useTime: 20);
        Register(ItemType.DiamondSword, "Diamond Sword", "Melee", 1, damage: 30, useTime: 18);
        
        // Placeable
        Register(ItemType.Torch, "Torch", "Placeable", 999, true);
        Register(ItemType.CraftingTable, "Crafting Table", "Placeable", 99, true);
        Register(ItemType.Furnace, "Furnace", "Placeable", 99, true);
        Register(ItemType.Anvil, "Anvil", "Placeable", 99, true);
        Register(ItemType.Chest, "Chest", "Placeable", 99, true);
        Register(ItemType.WoodPlatform, "Wood Platform", "Placeable", 999, true);
        
        // Consumables
        Register(ItemType.LesserHealingPotion, "Lesser Healing Potion", "Potion", 30, healAmount: 50);
        Register(ItemType.HealingPotion, "Healing Potion", "Potion", 30, healAmount: 100);
        Register(ItemType.ManaPotion, "Mana Potion", "Potion", 30, manaAmount: 50);
    }
    
    private static void Register(ItemType type, string name, string category, int maxStack, 
        bool placeable = false, int damage = 0, int useTime = 0, int healAmount = 0, int manaAmount = 0)
    {
        _data[type] = new ItemData
        {
            Name = name,
            Category = category,
            MaxStack = maxStack,
            IsPlaceable = placeable,
            Damage = damage,
            UseTime = useTime,
            HealAmount = healAmount,
            ManaAmount = manaAmount
        };
    }
    
    public static ItemData Get(ItemType type)
    {
        return _data.TryGetValue(type, out var data) ? data : new ItemData { Name = "Unknown", MaxStack = 1 };
    }
    
    public static string GetName(ItemType type) => Get(type).Name;
    public static int GetMaxStack(ItemType type) => Get(type).MaxStack;
    public static bool IsPlaceable(ItemType type) => Get(type).IsPlaceable;
    public static int GetDamage(ItemType type) => Get(type).Damage;
}

public struct ItemData
{
    public string Name;
    public string Category;
    public int MaxStack;
    public bool IsPlaceable;
    public int Damage;
    public int UseTime;
    public int HealAmount;
    public int ManaAmount;
}

/// <summary>
/// Player inventory system
/// </summary>
public class Inventory
{
    private readonly Item?[] _slots;
    public int Size { get; }
    public int HotbarSize { get; } = 9;
    
    public Inventory(int size)
    {
        Size = size;
        _slots = new Item?[size];
    }
    
    public Item? GetItem(int slot)
    {
        if (slot < 0 || slot >= Size) return null;
        return _slots[slot];
    }
    
    public bool SetItem(int slot, Item? item)
    {
        if (slot < 0 || slot >= Size) return false;
        _slots[slot] = item;
        return true;
    }
    
    /// <summary>
    /// Add item to inventory, returns remaining count that couldn't be added
    /// </summary>
    public int AddItem(Item item)
    {
        int remaining = item.Count;
        
        // First, try to stack with existing items
        for (int i = 0; i < Size && remaining > 0; i++)
        {
            var slot = _slots[i];
            if (slot != null && slot.CanStackWith(item))
            {
                int canAdd = slot.MaxStack - slot.Count;
                int toAdd = Math.Min(canAdd, remaining);
                slot.Count += toAdd;
                remaining -= toAdd;
            }
        }
        
        // Then, find empty slots
        for (int i = 0; i < Size && remaining > 0; i++)
        {
            if (_slots[i] == null)
            {
                int toAdd = Math.Min(item.MaxStack, remaining);
                _slots[i] = new Item(item.Type, toAdd);
                remaining -= toAdd;
            }
        }
        
        return remaining;
    }
    
    /// <summary>
    /// Remove items from a specific slot
    /// </summary>
    public bool RemoveItem(int slot, int count = 1)
    {
        var item = GetItem(slot);
        if (item == null || item.Count < count) return false;
        
        item.Count -= count;
        if (item.Count <= 0)
        {
            _slots[slot] = null;
        }
        
        return true;
    }
    
    /// <summary>
    /// Count total items of a type
    /// </summary>
    public int CountItems(ItemType type)
    {
        int total = 0;
        foreach (var slot in _slots)
        {
            if (slot?.Type == type)
                total += slot.Count;
        }
        return total;
    }
    
    /// <summary>
    /// Check if inventory contains at least count of item type
    /// </summary>
    public bool HasItem(ItemType type, int count = 1)
    {
        return CountItems(type) >= count;
    }
    
    /// <summary>
    /// Remove items of a type from anywhere in inventory
    /// </summary>
    public bool ConsumeItem(ItemType type, int count = 1)
    {
        if (!HasItem(type, count)) return false;
        
        int remaining = count;
        for (int i = 0; i < Size && remaining > 0; i++)
        {
            var slot = _slots[i];
            if (slot?.Type == type)
            {
                int toRemove = Math.Min(slot.Count, remaining);
                slot.Count -= toRemove;
                remaining -= toRemove;
                
                if (slot.Count <= 0)
                    _slots[i] = null;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Swap items between two slots
    /// </summary>
    public void SwapSlots(int slot1, int slot2)
    {
        if (slot1 < 0 || slot1 >= Size || slot2 < 0 || slot2 >= Size) return;
        (_slots[slot1], _slots[slot2]) = (_slots[slot2], _slots[slot1]);
    }
    
    /// <summary>
    /// Get all non-empty slots
    /// </summary>
    public IEnumerable<(int slot, Item item)> GetAllItems()
    {
        for (int i = 0; i < Size; i++)
        {
            if (_slots[i] != null)
                yield return (i, _slots[i]!);
        }
    }
    
    /// <summary>
    /// Find first slot containing item type
    /// </summary>
    public int FindItem(ItemType type)
    {
        for (int i = 0; i < Size; i++)
        {
            if (_slots[i]?.Type == type)
                return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Find first empty slot
    /// </summary>
    public int FindEmptySlot()
    {
        for (int i = 0; i < Size; i++)
        {
            if (_slots[i] == null)
                return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Sort inventory by category and name
    /// </summary>
    public void Sort()
    {
        var items = _slots.Where(s => s != null).OrderBy(s => ItemProperties.Get(s!.Type).Category)
                          .ThenBy(s => ItemProperties.GetName(s!.Type)).ToArray();
        
        Array.Clear(_slots, 0, _slots.Length);
        for (int i = 0; i < items.Length; i++)
        {
            _slots[i] = items[i];
        }
    }
}
