using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using TerraNova.Core;

namespace TerraNova.Systems;

public class ParallaxManager : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ContentManager _content;
    
    private List<ParallaxLayer> _layers = new();
    
    public ParallaxManager(GraphicsDevice graphicsDevice, ContentManager content)
    {
        _graphicsDevice = graphicsDevice;
        _content = content;
    }
    
    public void Initialize()
    {
        // Add layers (Back to Front)
        
        // 1. Sky/gradient is usually drawn by clear color, but we can add a far backdrop
        // Layer 0: Furthest mountains/clouds
        AddLayer("Generated/parallax_0", 0.0f, 0.0f); // Static sky
        
        // Layer 1: Far mountains
        AddLayer("Generated/parallax_1", 0.1f, 0.05f);
        
        // Layer 2: Near mountains
        AddLayer("Generated/parallax_2", 0.2f, 0.1f);
        
        // Layer 3: Hills/Forest
        AddLayer("Generated/parallax_3", 0.4f, 0.15f);
        
        // Layer 4: Close details (trees)
        AddLayer("Generated/parallax_4", 0.6f, 0.2f);
    }
    
    private void AddLayer(string texturePath, float parallaxX, float parallaxY)
    {
        if (TryLoadTexture(texturePath, out Texture2D texture))
        {
            _layers.Add(new ParallaxLayer(texture, new Vector2(parallaxX, parallaxY)));
        }
    }
    
    // Helper to load raw PNGs similar to TextureManager
    private bool TryLoadTexture(string path, out Texture2D texture)
    {
        try
        {
            string fullPath = Path.Combine(_content.RootDirectory, path + ".png");
            if (File.Exists(fullPath))
            {
                using (var stream = TitleContainer.OpenStream(fullPath))
                {
                    texture = Texture2D.FromStream(_graphicsDevice, stream);
                    return true;
                }
            }
        }
        catch { }
        
        texture = null!;
        return false;
    }
    
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, float dayTime)
    {
        // Simple day/night tinting
        Color tint = Color.White;
        if (dayTime > 0.75f || dayTime < 0.25f)
        {
            // Night
            tint = new Color(50, 50, 80);
        }
        else if (dayTime < 0.3f || dayTime > 0.7f)
        {
             // Twilight
             tint = new Color(200, 150, 150);
        }

        Vector2 camPos = camera.Position;
        
        foreach (var layer in _layers)
        {
            DrawLayer(spriteBatch, layer, camPos, tint);
        }
    }
    
    private void DrawLayer(SpriteBatch spriteBatch, ParallaxLayer layer, Vector2 camPos, Color tint)
    {
        Texture2D texture = layer.Texture;
        if (texture.Width == 0) return; // Prevent divide by zero/infinite loop

        Vector2 parallax = layer.ParallaxFactor;
        
        // Calculate offset based on camera position
        float offsetX = (camPos.X * parallax.X) % texture.Width;
        float offsetY = (camPos.Y * parallax.Y);
        
        // We might want to clamp Y or scroll it differently depending on style
        // For Terraria style, Y parallax is often small or clamped so backgrounds don't disappear into sky
        // Let's keep it simple: Y moves slightly but is anchored effectively
        
        // Determine how many tiles we need to cover the screen
        // Viewport size in world units? No, we are drawing in screen space usually, 
        // OR world space but behind everything. 
        // Let's assume we are drawing in CreateRenderTarget space from TerraNovaGame?
        // Actually, Parallax is best drawn in Screen Space or with a special matrix.
        // If we use the camera matrix, the "position" is already accounted for, so we shouldn't use camPos AGAIN 
        // unless we are manually managing the scroll.
        //
        // Standard Parallax: Draw repeatedly to cover view, offsetting by (Cam * Parallax).
        // Since we will likely call this with Begin(SamplerState.LinearWrap) or manually tiling.
        
        // Let's do manual tiling calculation for robustness
        
        Viewport vp = _graphicsDevice.Viewport;
        
        // Effective rendering pos
        // Pos = -Offset 
        // We need to tile across the screen width
        
        float x = -offsetX;
        // Adjust for wrapping if negative
        while (x > 0) x -= texture.Width;
        while (x < -texture.Width) x += texture.Width;
        
        // Draw enough copies to fill width
        int count = (int)Math.Ceiling(vp.Width / (float)texture.Width) + 2;
        
        // Center Y somewhat? Or attach to bottom?
        // Let's say the image bottom aligns with some horizon. 
        // For now, simple vertical position:
        float y = -offsetY + vp.Height / 2f; // Center vertically-ish
        
        // Adjust vertically if needed
        // Assuming textures are 1080p high or similar, or small strips?
        // The files are likely large. If strips, we scale.
        // Let's assume the texture fills height or we scale it.
        float scale = (float)vp.Height / texture.Height;
        if (scale < 1f) scale = 1f; // Don't shrink too much
        
        // Actually, user said files are in Generated. Let's assume they are reasonably sized.
        
        for (int i = 0; i < count; i++)
        {
            spriteBatch.Draw(texture, new Vector2(x + i * texture.Width, y), null, tint, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }

    public void Dispose()
    {
        foreach (var layer in _layers)
        {
            layer.Texture.Dispose();
        }
        _layers.Clear();
    }
    
    private class ParallaxLayer
    {
        public Texture2D Texture { get; }
        public Vector2 ParallaxFactor { get; }
        
        public ParallaxLayer(Texture2D texture, Vector2 parallaxFactor)
        {
            Texture = texture;
            ParallaxFactor = parallaxFactor;
        }
    }
}
