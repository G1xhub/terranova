using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.Entities;
using FontStashSharp;

namespace TerraNova.UI;

/// <summary>
/// Manages all UI rendering - HUD, inventory, menus
/// </summary>
public class UIManager
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Player _player;
    
    // UI State
    private bool _inventoryOpen = false;
    private int _hoveredSlot = -1;
    
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
    
    public UIManager(GraphicsDevice graphicsDevice, Player player)
    {
        _graphicsDevice = graphicsDevice;
        _player = player;
    }
    
    public void Update(GameTime gameTime, InputManager input)
    {
        // Toggle inventory
        if (input.IsInventoryPressed)
        {
            _inventoryOpen = !_inventoryOpen;
        }
        
        // Update hovered slot
        if (_inventoryOpen)
        {
            UpdateInventoryHover(input.MousePosition);
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
        
        // Background
        DrawRect(spriteBatch, x, y, width, height, HealthBarBg);
        
        // Mana fill
        float manaPercent = (float)_player.Mana / _player.MaxMana;
        int fillWidth = (int)(width * manaPercent);
        DrawRect(spriteBatch, x, y, fillWidth, height, ManaBarFg);
        
        // Border
        DrawRectOutline(spriteBatch, x, y, width, height, Color.White, 1);
    }
    
    private void DrawHotbar(SpriteBatch spriteBatch)
    {
        int totalWidth = 9 * HotbarSlotSize + 8 * HotbarPadding;
        int startX = (_graphicsDevice.Viewport.Width - totalWidth) / 2;
        int y = _graphicsDevice.Viewport.Height - HotbarSlotSize - 20;
        
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
        // Background
        DrawRect(spriteBatch, x, y, HotbarSlotSize, HotbarSlotSize, SlotBg);
        
        // Border
        var borderColor = highlight ? SlotSelected : SlotBorder;
        DrawRectOutline(spriteBatch, x, y, HotbarSlotSize, HotbarSlotSize, borderColor, highlight ? 3 : 2);
        
        // Item
        var item = _player.Inventory.GetItem(slot);
        if (item != null)
        {
            // Draw item icon (placeholder - just colored square)
            var itemColor = GetItemColor(item.Type);
            int iconSize = HotbarSlotSize - 8;
            DrawRect(spriteBatch, x + 4, y + 4, iconSize, iconSize, itemColor);
            
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
        // Convert dayTime to hours
        int hours = (int)(dayTime * 24);
        int minutes = (int)((dayTime * 24 - hours) * 60);
        
        string timeText = $"{hours:D2}:{minutes:D2}";
        string periodText = hours >= 12 ? "PM" : "AM";
        
        // Position in top right
        var timeSize = FontManager.DebugFont.MeasureString(timeText);
        int x = _graphicsDevice.Viewport.Width - (int)timeSize.X - 60;
        int y = 20;
        
        FontManager.DebugFont.DrawText(spriteBatch, timeText, new Vector2(x, y), Color.White);
        FontManager.DebugFont.DrawText(spriteBatch, periodText, new Vector2(x + timeSize.X + 5, y), Color.Gray);
    }
    
    private void DrawRect(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color)
    {
        spriteBatch.Draw(TextureManager.Pixel, new Rectangle(x, y, width, height), color);
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
}
