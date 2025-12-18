using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;

namespace TerraNova.Systems;

/// <summary>
/// Renders stars in the night sky with twinkling effect
/// </summary>
public class StarRenderer
{
    private readonly List<Star> _stars;
    private Random _random;
    private Texture2D? _starTexture;
    private readonly GraphicsDevice _graphicsDevice;
    
    private struct Star
    {
        public Vector2 Position;
        public float Size;
        public float TwinkleSpeed;
        public float TwinkleOffset;
        public Color Color;
    }
    
    public StarRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _stars = new List<Star>();
        _random = new Random();
    }
    
    /// <summary>
    /// Initialize stars with a seed for consistent generation
    /// </summary>
    public void Initialize(int seed, int count = 150)
    {
        _random = new Random(seed);
        _stars.Clear();
        
        // Create star texture (small white dot)
        _starTexture = new Texture2D(_graphicsDevice, 3, 3);
        Color[] data = new Color[9];
        // Create a soft star shape
        data[0] = Color.Transparent;
        data[1] = new Color(255, 255, 255, 100);
        data[2] = Color.Transparent;
        data[3] = new Color(255, 255, 255, 100);
        data[4] = Color.White;
        data[5] = new Color(255, 255, 255, 100);
        data[6] = Color.Transparent;
        data[7] = new Color(255, 255, 255, 100);
        data[8] = Color.Transparent;
        _starTexture.SetData(data);
        
        // Star colors
        Color[] starColors = new[]
        {
            Color.White,
            new Color(255, 255, 230), // Warm white
            new Color(230, 240, 255), // Cool white
            new Color(255, 220, 180), // Yellow
            new Color(200, 220, 255), // Blue-white
        };
        
        for (int i = 0; i < count; i++)
        {
            _stars.Add(new Star
            {
                Position = new Vector2(
                    _random.Next(0, 3000),
                    _random.Next(0, 400)  // Only upper portion of sky
                ),
                Size = 0.5f + (float)_random.NextDouble() * 1.5f,
                TwinkleSpeed = 2f + (float)_random.NextDouble() * 4f,
                TwinkleOffset = (float)_random.NextDouble() * MathF.PI * 2,
                Color = starColors[_random.Next(starColors.Length)]
            });
        }
    }
    
    /// <summary>
    /// Draw stars in the sky
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, float dayTime, Camera2D camera, float gameTime)
    {
        if (_starTexture == null) return;
        
        // Calculate night visibility (0 = day, 1 = full night)
        float nightFactor = CalculateNightFactor(dayTime);
        if (nightFactor <= 0.01f) return;
        
        foreach (var star in _stars)
        {
            // Parallax effect - stars move slower than camera
            Vector2 screenPos = star.Position - camera.Position * 0.05f;
            
            // Wrap around horizontally
            screenPos.X = ((screenPos.X % 1500) + 1500) % 1500 - 200;
            
            // Skip if not visible on screen
            if (screenPos.X < -50 || screenPos.X > camera.VisibleArea.Width + 50)
                continue;
            if (screenPos.Y < -50 || screenPos.Y > 500)
                continue;
            
            // Twinkle effect
            float twinkle = 0.5f + 0.5f * MathF.Sin(gameTime * star.TwinkleSpeed + star.TwinkleOffset);
            
            // Final alpha based on night factor and twinkle
            byte alpha = (byte)(255 * nightFactor * twinkle);
            
            // Apply star color with alpha
            Color drawColor = new Color(star.Color.R, star.Color.G, star.Color.B, alpha);
            
            // Draw the star
            Vector2 origin = new Vector2(_starTexture.Width / 2f, _starTexture.Height / 2f);
            spriteBatch.Draw(
                _starTexture,
                screenPos,
                null,
                drawColor,
                0f,
                origin,
                star.Size,
                SpriteEffects.None,
                0f
            );
        }
    }
    
    /// <summary>
    /// Calculate how visible the night sky is (0 = day, 1 = full night)
    /// </summary>
    private float CalculateNightFactor(float dayTime)
    {
        // dayTime: 0 = midnight, 0.25 = 6am, 0.5 = noon, 0.75 = 6pm
        
        if (dayTime >= 0.3f && dayTime <= 0.7f)
        {
            // Full day - no stars
            return 0f;
        }
        else if (dayTime >= 0.2f && dayTime < 0.3f)
        {
            // Dawn - stars fading out
            return 1f - (dayTime - 0.2f) / 0.1f;
        }
        else if (dayTime > 0.7f && dayTime <= 0.8f)
        {
            // Dusk - stars fading in
            return (dayTime - 0.7f) / 0.1f;
        }
        else
        {
            // Night - full stars
            return 1f;
        }
    }
    
    public void Dispose()
    {
        _starTexture?.Dispose();
    }
}

