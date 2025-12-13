using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Entities;

namespace TerraNova.Core;

/// <summary>
/// 2D Camera with smooth following, zoom, and shake effects
/// </summary>
public class Camera2D
{
    private Vector2 _position;
    private Vector2 _targetPosition;
    private float _zoom = 1f;
    private float _targetZoom = 1f;
    private float _rotation;
    private Viewport _viewport;
    
    // Shake effect
    private float _shakeIntensity;
    private float _shakeDuration;
    private float _shakeTimer;
    private Vector2 _shakeOffset;
    
    // Following
    private Entity? _target;
    private float _followSpeed = 0.1f;
    private Vector2 _followOffset;
    
    // Bounds
    private Rectangle? _bounds;
    
    // Configuration
    public float MinZoom { get; set; } = 0.5f;
    public float MaxZoom { get; set; } = 3f;
    public float ZoomSpeed { get; set; } = 0.1f;
    public float DeadZoneWidth { get; set; } = 50f;
    public float DeadZoneHeight { get; set; } = 30f;
    
    // Properties
    public Vector2 Position
    {
        get => _position;
        set
        {
            _position = value;
            _targetPosition = value;
        }
    }
    
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = MathHelper.Clamp(value, MinZoom, MaxZoom);
            _targetZoom = _zoom;
        }
    }
    
    public float Rotation
    {
        get => _rotation;
        set => _rotation = value;
    }
    
    public Vector2 Center => _position; // Position already represents the center
    
    public Rectangle VisibleArea
    {
        get
        {
            float width = _viewport.Width / _zoom;
            float height = _viewport.Height / _zoom;
            // Position is the center, so offset by half the visible size
            return new Rectangle(
                (int)(_position.X - width / 2),
                (int)(_position.Y - height / 2),
                (int)width,
                (int)height
            );
        }
    }
    
    public Matrix TransformMatrix
    {
        get
        {
            var offset = _shakeOffset;
            
            // Position represents the center of the view
            return Matrix.CreateTranslation(new Vector3(-_position.X - offset.X, -_position.Y - offset.Y, 0)) *
                   Matrix.CreateRotationZ(_rotation) *
                   Matrix.CreateScale(_zoom, _zoom, 1) *
                   Matrix.CreateTranslation(new Vector3(_viewport.Width / 2f, _viewport.Height / 2f, 0));
        }
    }
    
    public Camera2D(Viewport viewport)
    {
        _viewport = viewport;
        _position = Vector2.Zero;
        _targetPosition = Vector2.Zero;
    }
    
    public void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Update shake
        UpdateShake(deltaTime);
        
        // Smooth zoom
        if (Math.Abs(_zoom - _targetZoom) > 0.001f)
        {
            _zoom = MathHelper.Lerp(_zoom, _targetZoom, ZoomSpeed);
        }
        
        // Smooth follow
        if (_target != null)
        {
            // Calculate target center
            var targetCenter = _target.Center + _followOffset;
            
            // Calculate desired camera position (center on target)
            // Position represents the center of the camera view in world space
            var desiredPos = targetCenter;
            
            // Apply dead zone
            var diff = desiredPos - _targetPosition;
            if (Math.Abs(diff.X) > DeadZoneWidth)
                _targetPosition.X += (diff.X - Math.Sign(diff.X) * DeadZoneWidth);
            if (Math.Abs(diff.Y) > DeadZoneHeight)
                _targetPosition.Y += (diff.Y - Math.Sign(diff.Y) * DeadZoneHeight);
        }
        
        // Smooth position
        _position = Vector2.Lerp(_position, _targetPosition, _followSpeed);
        
        // Apply bounds
        ApplyBounds();
    }
    
    public void Follow(Entity target, Vector2? offset = null)
    {
        _target = target;
        _followOffset = offset ?? Vector2.Zero;
        
        // Immediately center on target
        if (target != null)
        {
            var targetCenter = target.Center + _followOffset;
            // Position represents the center of the camera view in world space
            _position = targetCenter;
            _targetPosition = _position;
        }
    }
    
    public void SetBounds(Rectangle bounds)
    {
        _bounds = bounds;
    }
    
    public void ClampToWorld(int worldWidth, int worldHeight)
    {
        _bounds = new Rectangle(0, 0, worldWidth, worldHeight);
        ApplyBounds();
    }
    
    private void ApplyBounds()
    {
        if (_bounds == null) return;
        
        float viewWidth = _viewport.Width / _zoom;
        float viewHeight = _viewport.Height / _zoom;
        
        // Clamp position to bounds (position is center, so account for half view size)
        float halfWidth = viewWidth / 2f;
        float halfHeight = viewHeight / 2f;
        _position.X = MathHelper.Clamp(_position.X, _bounds.Value.Left + halfWidth, _bounds.Value.Right - halfWidth);
        _position.Y = MathHelper.Clamp(_position.Y, _bounds.Value.Top + halfHeight, _bounds.Value.Bottom - halfHeight);
        
        // Also clamp target position
        _targetPosition.X = MathHelper.Clamp(_targetPosition.X, _bounds.Value.Left + halfWidth, _bounds.Value.Right - halfWidth);
        _targetPosition.Y = MathHelper.Clamp(_targetPosition.Y, _bounds.Value.Top + halfHeight, _bounds.Value.Bottom - halfHeight);
    }
    
    public void Shake(float intensity, float duration)
    {
        _shakeIntensity = intensity;
        _shakeDuration = duration;
        _shakeTimer = duration;
    }
    
    private void UpdateShake(float deltaTime)
    {
        if (_shakeTimer > 0)
        {
            _shakeTimer -= deltaTime;
            
            // Calculate shake with decay
            float progress = _shakeTimer / _shakeDuration;
            float currentIntensity = _shakeIntensity * progress;
            
            _shakeOffset = new Vector2(
                (float)(Random.Shared.NextDouble() * 2 - 1) * currentIntensity,
                (float)(Random.Shared.NextDouble() * 2 - 1) * currentIntensity
            );
        }
        else
        {
            _shakeOffset = Vector2.Zero;
        }
    }
    
    public void ZoomTo(float targetZoom)
    {
        _targetZoom = MathHelper.Clamp(targetZoom, MinZoom, MaxZoom);
    }
    
    public void ZoomIn(float amount = 0.1f)
    {
        ZoomTo(_targetZoom + amount);
    }
    
    public void ZoomOut(float amount = 0.1f)
    {
        ZoomTo(_targetZoom - amount);
    }
    
    public void UpdateViewport(Viewport viewport)
    {
        _viewport = viewport;
    }
    
    /// <summary>
    /// Convert screen coordinates to world coordinates
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        return Vector2.Transform(screenPosition, Matrix.Invert(TransformMatrix));
    }
    
    /// <summary>
    /// Convert world coordinates to screen coordinates
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPosition)
    {
        return Vector2.Transform(worldPosition, TransformMatrix);
    }
    
    /// <summary>
    /// Check if a rectangle is visible in the camera view
    /// </summary>
    public bool IsVisible(Rectangle bounds)
    {
        return VisibleArea.Intersects(bounds);
    }
    
    /// <summary>
    /// Check if a point is visible in the camera view
    /// </summary>
    public bool IsVisible(Vector2 point)
    {
        return VisibleArea.Contains(point);
    }
}
