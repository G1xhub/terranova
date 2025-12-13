using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;

namespace TerraNova.Systems;

/// <summary>
/// Event system for special game events (Blood Moon, Eclipse, Boss fights, etc.)
/// Manages color overlays and filters
/// </summary>
public class EventSystem
{
    private GameEvent? _currentEvent;
    private float _eventTimer = 0f;
    private float _eventDuration = 0f;
    
    private readonly GraphicsDevice _graphicsDevice;
    private RenderTarget2D? _overlayTexture;
    private Color[] _overlayData;
    private int _overlayWidth;
    private int _overlayHeight;
    
    public GameEvent? CurrentEvent => _currentEvent;
    public float EventProgress => _eventDuration > 0 ? _eventTimer / _eventDuration : 0f;
    
    public EventSystem(GraphicsDevice graphicsDevice, int width, int height)
    {
        _graphicsDevice = graphicsDevice;
        _overlayWidth = width;
        _overlayHeight = height;
        _overlayData = new Color[width * height];
        CreateOverlayTexture();
    }
    
    private void CreateOverlayTexture()
    {
        _overlayTexture?.Dispose();
        _overlayTexture = new RenderTarget2D(_graphicsDevice, _overlayWidth, _overlayHeight);
    }
    
    public void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        if (_currentEvent != null)
        {
            _eventTimer += deltaTime;
            
            if (_eventTimer >= _eventDuration)
            {
                // Event ended
                _currentEvent = null;
                _eventTimer = 0f;
                _eventDuration = 0f;
            }
        }
    }
    
    public void StartEvent(GameEvent gameEvent, float duration)
    {
        _currentEvent = gameEvent;
        _eventTimer = 0f;
        _eventDuration = duration;
    }
    
    public void EndEvent()
    {
        _currentEvent = null;
        _eventTimer = 0f;
        _eventDuration = 0f;
    }
    
    public void Draw(SpriteBatch spriteBatch, RenderTarget2D? renderTarget = null)
    {
        if (_currentEvent == null) return;
        
        // Get overlay color based on event
        Color overlayColor = GetEventOverlayColor(_currentEvent.Value);
        float intensity = GetEventIntensity();
        
        // Draw overlay
        var overlayRect = new Rectangle(0, 0, _overlayWidth, _overlayHeight);
        var colorWithAlpha = overlayColor * intensity;
        spriteBatch.Draw(TextureManager.Pixel, overlayRect, colorWithAlpha);
    }
    
    private Color GetEventOverlayColor(GameEvent gameEvent)
    {
        return gameEvent switch
        {
            GameEvent.BloodMoon => new Color(150, 0, 0, 100), // Dark red
            GameEvent.Eclipse => new Color(0, 0, 0, 150), // Dark
            GameEvent.BossFight => new Color(100, 0, 100, 80), // Purple
            GameEvent.SolarEclipse => new Color(50, 0, 50, 120), // Dark purple
            GameEvent.Rainbow => new Color(255, 255, 255, 50), // Light white (will be animated)
            _ => Color.Transparent
        };
    }
    
    private float GetEventIntensity()
    {
        if (_currentEvent == null) return 0f;
        
        float progress = EventProgress;
        
        // Fade in/out at start and end
        float fadeIn = Math.Min(progress * 5f, 1f); // Fade in over first 20%
        float fadeOut = Math.Min((1f - progress) * 5f, 1f); // Fade out over last 20%
        float intensity = Math.Min(fadeIn, fadeOut);
        
        // Add pulsing for some events
        if (_currentEvent == GameEvent.BloodMoon || _currentEvent == GameEvent.BossFight)
        {
            float pulse = (MathF.Sin(_eventTimer * 2f) + 1f) * 0.1f; // 10% pulse
            intensity = MathHelper.Clamp(intensity + pulse, 0f, 1f);
        }
        
        return intensity;
    }
    
    public void Resize(int width, int height)
    {
        if (_overlayWidth == width && _overlayHeight == height) return;
        
        _overlayWidth = width;
        _overlayHeight = height;
        _overlayData = new Color[width * height];
        CreateOverlayTexture();
    }
    
    public void Dispose()
    {
        _overlayTexture?.Dispose();
    }
}

public enum GameEvent
{
    None,
    BloodMoon,      // Red tint, increased enemy spawns
    Eclipse,       // Dark overlay
    BossFight,      // Purple tint, dramatic
    SolarEclipse,   // Very dark, special enemies
    Rainbow         // Colorful, rare event
}

