using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.Entities;
using TerraNova.Systems;
using TerraNova.World;
using FontStashSharp;

namespace TerraNova.UI;

/// <summary>
/// Manages all UI rendering - HUD, inventory, menus
/// </summary>
public class UIManager
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Player _player;
    private readonly GameWorld _world;
    
    // UI State
    private bool _inventoryOpen = false;
    private bool _craftingOpen = false;
    private bool _brewingOpen = false;
    
    // Public properties to check menu state
    public bool IsInventoryOpen => _inventoryOpen;
    public bool IsCraftingOpen => _craftingOpen;
    public bool IsBrewingOpen => _brewingOpen;
    public bool IsAnyMenuOpen => _inventoryOpen || _craftingOpen || _brewingOpen;
    private int _hoveredSlot = -1;
    private int _hoveredRecipe = -1;
    private int _hoveredBrewingRecipe = -1;
    private List<CraftingRecipe> _availableRecipes = new();
    private List<BrewingRecipe> _availableBrewingRecipes = new();
    
    // UI Dimensions
    private const int HotbarSlotSize = 44;
    private const int HotbarPadding = 4;
    private const int HealthBarWidth = 200;
    private const int HealthBarHeight = 20;
    
    // Colors
    private static readonly Color HealthBarBg = new(60, 60, 60);
    private static readonly Color HealthBarFg = new(220, 50, 50);
    private static readonly Color ManaBarFg = new(50, 100, 220);
    private static readonly Color SlotBg = new(40, 40, 60, 200);
    private static readonly Color SlotBorder = new(100, 100, 140);
    private static readonly Color SlotSelected = new(255, 215, 0);
    
    public UIManager(GraphicsDevice graphicsDevice, Player player, GameWorld world)
    {
        _graphicsDevice = graphicsDevice;
        _player = player;
        _world = world;
    }
    
    public void Update(GameTime gameTime, InputManager input)
    {
        // Toggle inventory
        if (input.IsInventoryPressed)
        {
            _inventoryOpen = !_inventoryOpen;
            if (_inventoryOpen)
            {
                UpdateAvailableRecipes();
            }
            else
            {
                // Reset input state when closing menu to prevent stuck input
                input.ResetState();
            }
        }
        
        // Toggle crafting (C key)
        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.C))
        {
            _craftingOpen = !_craftingOpen;
            if (_craftingOpen)
            {
                UpdateAvailableRecipes();
            }
            else
            {
                // Reset input state when closing menu to prevent stuck input
                input.ResetState();
            }
        }
        
        // Update hovered slot
        if (_inventoryOpen)
        {
            UpdateInventoryHover(input.MousePosition);
        }
        
        // Update hovered recipe
        if (_craftingOpen)
        {
            UpdateCraftingHover(input.MousePosition);
            
            // Craft on click
            if (input.IsMouseButtonPressed(MouseButton.Left) && _hoveredRecipe >= 0)
            {
                TryCraftRecipe(_hoveredRecipe);
            }
        }
        
        // Update hovered brewing recipe
        if (_brewingOpen)
        {
            UpdateBrewingHover(input.MousePosition);
            
            // Brew on click
            if (input.IsMouseButtonPressed(MouseButton.Left) && _hoveredBrewingRecipe >= 0)
            {
                TryBrewRecipe(_hoveredBrewingRecipe);
            }
        }
    }
    
    private void UpdateAvailableRecipes()
    {
        var (hasCraftingTable, hasFurnace, hasAnvil) = CheckNearbyStations();
        _availableRecipes = CraftingSystem.GetAvailableRecipes(
            _player.Inventory,
            hasCraftingTable,
            hasFurnace,
            hasAnvil
        ).ToList();
    }
    
    private void UpdateAvailableBrewingRecipes()
    {
        _availableBrewingRecipes = BrewingSystem.GetAvailableRecipes(_player.Inventory).ToList();
    }
    
    private (bool craftingTable, bool furnace, bool anvil) CheckNearbyStations()
    {
        int playerTileX = (int)(_player.Center.X / GameConfig.TileSize);
        int playerTileY = (int)(_player.Center.Y / GameConfig.TileSize);
        int range = 5; // Check 5 tiles in each direction
        
        bool hasCraftingTable = false;
        bool hasFurnace = false;
        bool hasAnvil = false;
        
        for (int y = playerTileY - range; y <= playerTileY + range; y++)
        {
            for (int x = playerTileX - range; x <= playerTileX + range; x++)
            {
                var tile = _world.GetTile(x, y);
                if (tile == TileType.CraftingTable) hasCraftingTable = true;
                if (tile == TileType.Furnace) hasFurnace = true;
                if (tile == TileType.Anvil) hasAnvil = true;
            }
        }
        
        return (hasCraftingTable, hasFurnace, hasAnvil);
    }
    
    private void UpdateCraftingHover(Vector2 mousePos)
    {
        // Calculate crafting panel position
        int panelWidth = 400;
        int panelHeight = 500;
        int panelX = (_graphicsDevice.Viewport.Width - panelWidth) / 2;
        int panelY = (_graphicsDevice.Viewport.Height - panelHeight) / 2;
        
        int recipeHeight = 60;
        int startY = panelY + 60; // After title
        
        _hoveredRecipe = -1;
        
        for (int i = 0; i < _availableRecipes.Count; i++)
        {
            int recipeY = startY + i * recipeHeight;
            if (mousePos.X >= panelX && mousePos.X < panelX + panelWidth &&
                mousePos.Y >= recipeY && mousePos.Y < recipeY + recipeHeight)
            {
                _hoveredRecipe = i;
                return;
            }
        }
    }
    
    private void UpdateBrewingHover(Vector2 mousePos)
    {
        // Calculate brewing panel position
        int panelWidth = 400;
        int panelHeight = 500;
        int panelX = (_graphicsDevice.Viewport.Width - panelWidth) / 2;
        int panelY = (_graphicsDevice.Viewport.Height - panelHeight) / 2;
        
        int recipeHeight = 60;
        int startY = panelY + 60; // After title
        
        _hoveredBrewingRecipe = -1;
        
        for (int i = 0; i < _availableBrewingRecipes.Count; i++)
        {
            int recipeY = startY + i * recipeHeight;
            if (mousePos.X >= panelX && mousePos.X < panelX + panelWidth &&
                mousePos.Y >= recipeY && mousePos.Y < recipeY + recipeHeight)
            {
                _hoveredBrewingRecipe = i;
                return;
            }
        }
    }
    
    private void TryCraftRecipe(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= _availableRecipes.Count) return;
        
        var recipe = _availableRecipes[recipeIndex];
        var (hasCraftingTable, hasFurnace, hasAnvil) = CheckNearbyStations();
        
        if (CraftingSystem.TryCraft(recipe.Result, _player.Inventory, hasCraftingTable, hasFurnace, hasAnvil))
        {
            UpdateAvailableRecipes(); // Refresh after crafting
        }
    }
    
    private void TryBrewRecipe(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= _availableBrewingRecipes.Count) return;
        
        var recipe = _availableBrewingRecipes[recipeIndex];
        
        if (BrewingSystem.TryBrew(recipe.Result, _player.Inventory))
        {
            UpdateAvailableBrewingRecipes(); // Refresh after brewing
        }
    }
    
    private void UpdateInventoryHover(Vector2 mousePos)
    {
        // Calculate inventory position
        int invWidth = 10 * (HotbarSlotSize + HotbarPadding);
        int invHeight = 4 * (HotbarSlotSize + HotbarPadding);
        int invX = (_graphicsDevice.Viewport.Width - invWidth) / 2;
        int invY = (_graphicsDevice.Viewport.Height - invHeight) / 2;
        
        _hoveredSlot = -1;
        
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                int slotX = invX + col * (HotbarSlotSize + HotbarPadding);
                int slotY = invY + row * (HotbarSlotSize + HotbarPadding);
                
                if (mousePos.X >= slotX && mousePos.X < slotX + HotbarSlotSize &&
                    mousePos.Y >= slotY && mousePos.Y < slotY + HotbarSlotSize)
                {
                    _hoveredSlot = row * 10 + col;
                    return;
                }
            }
        }
    }
    
    public void Draw(SpriteBatch spriteBatch, float dayTime)
    {
        // Draw HUD
        DrawHealthBar(spriteBatch);
        DrawManaBar(spriteBatch);
        DrawHotbar(spriteBatch);
        DrawTimeDisplay(spriteBatch, dayTime);
        
        // Draw inventory if open
        if (_inventoryOpen)
        {
            DrawInventory(spriteBatch);
        }
        
        // Draw crafting if open
        if (_craftingOpen)
        {
            DrawCrafting(spriteBatch);
        }
        
        if (_brewingOpen)
        {
            DrawBrewingMenu(spriteBatch);
        }
    }
    
    private void DrawHealthBar(SpriteBatch spriteBatch)
    {
        int x = 20;
        int y = 20;
        
        // Background
        DrawRect(spriteBatch, x, y, HealthBarWidth, HealthBarHeight, HealthBarBg);
        
        // Health fill
        float healthPercent = (float)_player.Health / _player.MaxHealth;
        int fillWidth = (int)(HealthBarWidth * healthPercent);
        DrawRect(spriteBatch, x, y, fillWidth, HealthBarHeight, HealthBarFg);
        
        // Border
        DrawRectOutline(spriteBatch, x, y, HealthBarWidth, HealthBarHeight, Color.White, 2);
        
        // Text
        string healthText = $"{_player.Health}/{_player.MaxHealth}";
        var textSize = FontManager.DebugFont.MeasureString(healthText);
        FontManager.DebugFont.DrawText(spriteBatch, healthText, 
            new Vector2(x + (HealthBarWidth - textSize.X) / 2, y + (HealthBarHeight - textSize.Y) / 2),
            Color.White);
    }
    
    private void DrawManaBar(SpriteBatch spriteBatch)
    {
        int x = 20;
        int y = 48;
        int width = 150;
        int height = 14;
        
        float manaPercent = (float)_player.Mana / _player.MaxMana;
        
        // Modern background with gradient
        var bgDark = new Color(20, 20, 30, 220);
        var bgLight = new Color(35, 35, 50, 220);
        DrawRectGradient(spriteBatch, x, y, width, height, bgDark, bgLight);
        
        // Mana fill with gradient (blue to bright blue)
        int fillWidth = (int)(width * manaPercent);
        if (fillWidth > 0)
        {
            var manaDark = new Color(30, 80, 180);
            var manaLight = new Color(80, 150, 255);
            DrawRectGradient(spriteBatch, x, y, fillWidth, height, manaDark, manaLight);
            
            // Glow effect on mana bar
            var glowColor = new Color(100, 150, 255, 100);
            DrawRect(spriteBatch, x, y, fillWidth, height / 2, glowColor);
        }
        
        // Modern border with highlight
        DrawRectOutline(spriteBatch, x, y, width, height, new Color(150, 150, 150), 1);
        DrawRectOutline(spriteBatch, x + 1, y + 1, width - 2, height - 2, new Color(80, 80, 80), 1);
    }
    
    private void DrawHotbar(SpriteBatch spriteBatch)
    {
        int totalWidth = 9 * HotbarSlotSize + 8 * HotbarPadding;
        int startX = (_graphicsDevice.Viewport.Width - totalWidth) / 2;
        int y = _graphicsDevice.Viewport.Height - HotbarSlotSize - 20;
        
        // Draw modern backing panel with gradient
        var panelBgDark = new Color(20, 20, 30, 240);
        var panelBgLight = new Color(40, 40, 60, 240);
        DrawRectGradient(spriteBatch, startX - 8, y - 6, totalWidth + 16, HotbarSlotSize + 12, panelBgDark, panelBgLight);
        
        // Draw panel border with highlight
        DrawRectOutline(spriteBatch, startX - 8, y - 6, totalWidth + 16, HotbarSlotSize + 12, new Color(100, 100, 120), 2);
        DrawRectOutline(spriteBatch, startX - 7, y - 5, totalWidth + 14, HotbarSlotSize + 10, new Color(60, 60, 80), 1);
        
        // Also draw texture if available for extra detail
        if (TextureManager.UIHotbar != null)
        {
            var panelRect = new Rectangle(startX - 8, y - 6, totalWidth + 16, HotbarSlotSize + 12);
            spriteBatch.Draw(TextureManager.UIHotbar, panelRect, new Color(255, 255, 255, 180));
        }
        
        for (int i = 0; i < 9; i++)
        {
            int x = startX + i * (HotbarSlotSize + HotbarPadding);
            bool selected = i == _player.SelectedSlot;
            
            DrawInventorySlot(spriteBatch, x, y, i, selected);
        }
    }
    
    private void DrawInventory(SpriteBatch spriteBatch)
    {
        // Darken background
        DrawRect(spriteBatch, 0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, 
            new Color(0, 0, 0, 150));
        
        // Calculate inventory position
        int cols = 10;
        int rows = 4;
        int invWidth = cols * (HotbarSlotSize + HotbarPadding);
        int invHeight = rows * (HotbarSlotSize + HotbarPadding);
        int invX = (_graphicsDevice.Viewport.Width - invWidth) / 2;
        int invY = (_graphicsDevice.Viewport.Height - invHeight) / 2;
        
        // Draw title
        FontManager.DebugFont.DrawText(spriteBatch, "Inventory", new Vector2(invX, invY - 30), Color.White);
        
        // Draw slots
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int slot = row * cols + col;
                int x = invX + col * (HotbarSlotSize + HotbarPadding);
                int y = invY + row * (HotbarSlotSize + HotbarPadding);
                
                bool hovered = slot == _hoveredSlot;
                DrawInventorySlot(spriteBatch, x, y, slot, hovered);
            }
        }
        
        // Draw tooltip for hovered item
        if (_hoveredSlot >= 0)
        {
            var item = _player.Inventory.GetItem(_hoveredSlot);
            if (item != null)
            {
                string tooltip = ItemProperties.GetName(item.Type);
                // TODO: Draw tooltip near mouse
            }
        }
    }
    
    private void DrawInventorySlot(SpriteBatch spriteBatch, int x, int y, int slot, bool highlight)
    {
        // Background texture or fallback
        var slotTex = highlight ? TextureManager.UISelectedSlot : TextureManager.UISlot;
        if (slotTex != null)
        {
            spriteBatch.Draw(slotTex, new Rectangle(x, y, HotbarSlotSize, HotbarSlotSize), Color.White);
        }
        else
        {
            DrawRect(spriteBatch, x, y, HotbarSlotSize, HotbarSlotSize, SlotBg);
            var borderColor = highlight ? SlotSelected : SlotBorder;
            DrawRectOutline(spriteBatch, x, y, HotbarSlotSize, HotbarSlotSize, borderColor, highlight ? 3 : 2);
        }
        
        // Item
        var item = _player.Inventory.GetItem(slot);
        if (item != null)
        {
            int iconSize = HotbarSlotSize - 8;
            if (TextureManager.ItemIcons.TryGetValue(item.Type, out var icon))
            {
                spriteBatch.Draw(icon, new Rectangle(x + 4, y + 4, iconSize, iconSize), Color.White);
            }
            else
            {
                var itemColor = GetItemColor(item.Type);
                DrawRect(spriteBatch, x + 4, y + 4, iconSize, iconSize, itemColor);
            }
            
            // Draw count
            if (item.Count > 1)
            {
                string countText = item.Count.ToString();
                var countSize = FontManager.SmallFont.MeasureString(countText);
                FontManager.SmallFont.DrawText(spriteBatch, countText,
                    new Vector2(x + HotbarSlotSize - countSize.X - 4, y + HotbarSlotSize - countSize.Y - 2),
                    Color.White);
            }
        }
        
        // Slot number for hotbar
        if (slot < 9)
        {
            string slotNum = (slot + 1).ToString();
            FontManager.SmallFont.DrawText(spriteBatch, slotNum, new Vector2(x + 4, y + 2), Color.Gray);
        }
    }
    
    private void DrawTimeDisplay(SpriteBatch spriteBatch, float dayTime)
    {
        // Calculate time
        float hours = dayTime * 24f;
        int hour = (int)hours;
        int minute = (int)((hours - hour) * 60);
        
        string period = hour < 12 ? "AM" : "PM";
        int displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;
        string timeString = $"{displayHour:D2}:{minute:D2} {period}";
        
        // Determine time of day label and icon
        bool isDaytime = dayTime >= 0.25f && dayTime <= 0.75f;
        string timeOfDay = isDaytime ? "Day" : "Night";
        Color timeColor = isDaytime ? new Color(255, 255, 150) : new Color(150, 150, 255);
        
        // Draw time display in top-right corner
        var font = FontManager.DebugFont;
        if (font != null)
        {
            string displayText = $"{timeString} ({timeOfDay})";
            var textSize = font.MeasureString(displayText);
            int x = _graphicsDevice.Viewport.Width - (int)textSize.X - 20;
            int y = 20;
            
            // Draw background with time-based color tint
            var bgColor = new Color(0, 0, 0, 180);
            DrawRect(spriteBatch, x - 5, y - 2, (int)textSize.X + 10, (int)textSize.Y + 4, bgColor);
            
            // Draw border with time color
            DrawRectOutline(spriteBatch, x - 5, y - 2, (int)textSize.X + 10, (int)textSize.Y + 4, timeColor, 2);
            
            // Draw text with time color
            font.DrawText(spriteBatch, displayText, new Vector2(x, y), timeColor);
            
            // Draw sun/moon icon
            int iconSize = 20;
            int iconX = x - iconSize - 10;
            int iconY = y;
            
            if (isDaytime)
            {
                // Draw sun icon (circle)
                DrawRect(spriteBatch, iconX, iconY, iconSize, iconSize, timeColor);
            }
            else
            {
                // Draw moon icon (crescent - simplified as circle)
                DrawRect(spriteBatch, iconX, iconY, iconSize, iconSize, timeColor);
            }
        }
    }
    
    private void DrawRect(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color)
    {
        spriteBatch.Draw(TextureManager.Pixel, new Rectangle(x, y, width, height), color);
    }
    
    private void DrawRectGradient(SpriteBatch spriteBatch, int x, int y, int width, int height, Color topColor, Color bottomColor)
    {
        // Draw gradient by drawing horizontal lines with interpolated colors
        for (int i = 0; i < height; i++)
        {
            float t = i / (float)height;
            Color lineColor = Color.Lerp(topColor, bottomColor, t);
            spriteBatch.Draw(TextureManager.Pixel, new Rectangle(x, y + i, width, 1), lineColor);
        }
    }
    
    private void DrawRectOutline(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        // Top
        DrawRect(spriteBatch, x, y, width, thickness, color);
        // Bottom
        DrawRect(spriteBatch, x, y + height - thickness, width, thickness, color);
        // Left
        DrawRect(spriteBatch, x, y, thickness, height, color);
        // Right
        DrawRect(spriteBatch, x + width - thickness, y, thickness, height, color);
    }
    
    private static Color GetItemColor(ItemType type)
    {
        return type switch
        {
            // Materials
            ItemType.Dirt => new Color(139, 90, 43),
            ItemType.Stone => new Color(105, 105, 105),
            ItemType.Sand => new Color(244, 208, 63),
            ItemType.Wood => new Color(139, 69, 19),
            
            // Ores
            ItemType.CopperOre => new Color(184, 115, 51),
            ItemType.IronOre => new Color(161, 157, 148),
            ItemType.GoldOre => new Color(255, 215, 0),
            ItemType.Diamond => new Color(0, 255, 255),
            ItemType.Coal => new Color(44, 44, 44),
            
            // Tools
            ItemType.CopperPickaxe or ItemType.CopperAxe or ItemType.CopperSword => new Color(184, 115, 51),
            ItemType.IronPickaxe or ItemType.IronAxe or ItemType.IronSword => new Color(180, 180, 180),
            ItemType.GoldPickaxe or ItemType.GoldSword => new Color(255, 215, 0),
            
            // Placeable
            ItemType.Torch => new Color(255, 165, 0),
            ItemType.Chest => new Color(139, 90, 43),
            
            _ => Color.White
        };
    }
    
    private void DrawCrafting(SpriteBatch spriteBatch)
    {
        // Darken background
        DrawRect(spriteBatch, 0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, 
            new Color(0, 0, 0, 150));
        
        // Calculate panel position
        int panelWidth = 400;
        int panelHeight = 500;
        int panelX = (_graphicsDevice.Viewport.Width - panelWidth) / 2;
        int panelY = (_graphicsDevice.Viewport.Height - panelHeight) / 2;
        
        // Draw panel background
        DrawRect(spriteBatch, panelX, panelY, panelWidth, panelHeight, new Color(40, 40, 50, 240));
        DrawRectOutline(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.White, 2);
        
        // Draw title
        FontManager.LargeFont.DrawText(spriteBatch, "Crafting", 
            new Vector2(panelX + 20, panelY + 10), Color.White);
        
        // Draw station requirements
        var (hasCraftingTable, hasFurnace, hasAnvil) = CheckNearbyStations();
        int y = panelY + 40;
        if (hasCraftingTable)
            FontManager.SmallFont.DrawText(spriteBatch, "✓ Crafting Table", new Vector2(panelX + 20, y), Color.LimeGreen);
        else
            FontManager.SmallFont.DrawText(spriteBatch, "✗ Crafting Table", new Vector2(panelX + 20, y), Color.Red);
        
        y += 15;
        if (hasFurnace)
            FontManager.SmallFont.DrawText(spriteBatch, "✓ Furnace", new Vector2(panelX + 20, y), Color.LimeGreen);
        else
            FontManager.SmallFont.DrawText(spriteBatch, "✗ Furnace", new Vector2(panelX + 20, y), Color.Gray);
        
        y += 15;
        if (hasAnvil)
            FontManager.SmallFont.DrawText(spriteBatch, "✓ Anvil", new Vector2(panelX + 20, y), Color.LimeGreen);
        else
            FontManager.SmallFont.DrawText(spriteBatch, "✗ Anvil", new Vector2(panelX + 20, y), Color.Gray);
        
        // Draw recipes
        int recipeHeight = 60;
        int startY = panelY + 100;
        
        for (int i = 0; i < _availableRecipes.Count && i < 6; i++) // Max 6 visible
        {
            var recipe = _availableRecipes[i];
            int recipeY = startY + i * recipeHeight;
            bool hovered = i == _hoveredRecipe;
            
            // Recipe background
            var bgColor = hovered ? new Color(60, 60, 80, 255) : new Color(50, 50, 60, 200);
            DrawRect(spriteBatch, panelX + 10, recipeY, panelWidth - 20, recipeHeight - 5, bgColor);
            
            if (hovered)
            {
                DrawRectOutline(spriteBatch, panelX + 10, recipeY, panelWidth - 20, recipeHeight - 5, Color.Yellow, 2);
            }
            
            // Result item icon
            int iconSize = 40;
            int iconX = panelX + 20;
            int iconY = recipeY + 10;
            
            if (TextureManager.ItemIcons.TryGetValue(recipe.Result, out var icon))
            {
                spriteBatch.Draw(icon, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
            }
            else
            {
                var itemColor = GetItemColor(recipe.Result);
                DrawRect(spriteBatch, iconX, iconY, iconSize, iconSize, itemColor);
            }
            
            // Result name
            string resultName = ItemProperties.GetName(recipe.Result);
            if (recipe.Amount > 1)
                resultName += $" x{recipe.Amount}";
            FontManager.DebugFont.DrawText(spriteBatch, resultName, 
                new Vector2(iconX + iconSize + 10, iconY + 5), Color.White);
            
            // Ingredients
            int ingX = iconX + iconSize + 10;
            int ingY = iconY + 20;
            string ingredients = string.Join(", ", recipe.Ingredients.Select(ing => 
                $"{ItemProperties.GetName(ing.item)} x{ing.amount}"));
            FontManager.SmallFont.DrawText(spriteBatch, ingredients, 
                new Vector2(ingX, ingY), Color.Gray);
        }
        
        // Instructions
        FontManager.SmallFont.DrawText(spriteBatch, "Press C to close | Click recipe to craft", 
            new Vector2(panelX + 20, panelY + panelHeight - 25), Color.Gray);
    }
    
    private void DrawBrewingMenu(SpriteBatch spriteBatch)
    {
        // Darken background
        DrawRect(spriteBatch, 0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, 
            new Color(0, 0, 0, 150));
        
        // Calculate panel position
        int panelWidth = 400;
        int panelHeight = 500;
        int panelX = (_graphicsDevice.Viewport.Width - panelWidth) / 2;
        int panelY = (_graphicsDevice.Viewport.Height - panelHeight) / 2;
        
        // Draw panel background
        DrawRect(spriteBatch, panelX, panelY, panelWidth, panelHeight, new Color(50, 40, 60, 240));
        DrawRectOutline(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.Purple, 3);
        
        // Draw title
        FontManager.LargeFont.DrawText(spriteBatch, "Brewing (B to close)", 
            new Vector2(panelX + 20, panelY + 10), Color.Purple);
        
        // Draw recipes
        int recipeHeight = 60;
        int startY = panelY + 60;
        
        for (int i = 0; i < _availableBrewingRecipes.Count && i < 6; i++) // Max 6 visible
        {
            var recipe = _availableBrewingRecipes[i];
            int recipeY = startY + i * recipeHeight;
            bool hovered = i == _hoveredBrewingRecipe;
            
            // Recipe background
            var bgColor = hovered ? new Color(80, 60, 100, 255) : new Color(60, 50, 70, 200);
            DrawRect(spriteBatch, panelX + 10, recipeY, panelWidth - 20, recipeHeight - 5, bgColor);
            
            if (hovered)
            {
                DrawRectOutline(spriteBatch, panelX + 10, recipeY, panelWidth - 20, recipeHeight - 5, Color.Purple, 2);
            }
            
            // Result item icon
            int iconSize = 40;
            int iconX = panelX + 20;
            int iconY = recipeY + 10;
            
            if (TextureManager.ItemIcons.TryGetValue(recipe.Result, out var icon))
            {
                spriteBatch.Draw(icon, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
            }
            else
            {
                var itemColor = GetItemColor(recipe.Result);
                DrawRect(spriteBatch, iconX, iconY, iconSize, iconSize, itemColor);
            }
            
            // Result name
            string resultName = ItemProperties.GetName(recipe.Result);
            if (recipe.Amount > 1)
                resultName += $" x{recipe.Amount}";
            FontManager.DebugFont.DrawText(spriteBatch, resultName, 
                new Vector2(iconX + iconSize + 10, iconY + 5), Color.White);
            
            // Ingredients
            int ingX = iconX + iconSize + 10;
            int ingY = iconY + 20;
            string ingredients = string.Join(", ", recipe.Ingredients.Select(ing => 
                $"{ItemProperties.GetName(ing.item)} x{ing.amount}"));
            FontManager.SmallFont.DrawText(spriteBatch, ingredients, 
                new Vector2(ingX, ingY), Color.LightGray);
        }
        
        // Instructions
        FontManager.SmallFont.DrawText(spriteBatch, "Press B to close | Click recipe to brew", 
            new Vector2(panelX + 20, panelY + panelHeight - 25), Color.Gray);
    }
}
