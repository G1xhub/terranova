using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.World;

namespace TerraNova.Entities;

/// <summary>
/// Base class for all game entities (player, enemies, NPCs, projectiles, etc.)
/// </summary>
public abstract class Entity
{
    // Position and dimensions
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    
    // Derived properties
    public Vector2 Center => Position + new Vector2(Width / 2f, Height / 2f);
    public Rectangle Bounds => new((int)Position.X, (int)Position.Y, Width, Height);
    public Rectangle CollisionBounds => new(
        (int)Position.X + CollisionOffsetX,
        (int)Position.Y + CollisionOffsetY,
        CollisionWidth,
        CollisionHeight
    );
    
    // Collision box offsets (for sprites larger than hitbox)
    protected int CollisionOffsetX { get; set; } = 0;
    protected int CollisionOffsetY { get; set; } = 0;
    protected int CollisionWidth { get; set; }
    protected int CollisionHeight { get; set; }
    
    // Physics
    public bool IsOnGround { get; protected set; }
    public bool IsInWater { get; protected set; }
    public bool AffectedByGravity { get; protected set; } = true;
    public float GravityMultiplier { get; protected set; } = 1f;
    
    // State
    public bool IsActive { get; set; } = true;
    public bool FacingRight { get; protected set; } = true;
    
    // Stats
    public int Health { get; protected set; }
    public int MaxHealth { get; protected set; }
    public bool IsDead => Health <= 0;
    
    // Timers
    protected float InvulnerabilityTimer { get; set; }
    public bool IsInvulnerable => InvulnerabilityTimer > 0;
    
    protected Entity(Vector2 position, int width, int height)
    {
        Position = position;
        Width = width;
        Height = height;
        CollisionWidth = width;
        CollisionHeight = height;
    }
    
    public virtual void Update(GameTime gameTime, GameWorld world)
    {
        if (!IsActive) return;
        
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Update timers
        if (InvulnerabilityTimer > 0)
            InvulnerabilityTimer -= deltaTime;
        
        // Apply gravity
        if (AffectedByGravity && !IsInWater)
        {
            Velocity = new Vector2(
                Velocity.X,
                Math.Min(Velocity.Y + TerraNovaGame.Config.Gravity * GravityMultiplier, 
                         TerraNovaGame.Config.MaxFallSpeed)
            );
        }
        
        // Check if in water
        CheckWater(world);
        
        // Apply water physics
        if (IsInWater)
        {
            Velocity *= 0.95f; // Water drag
        }
        
        // Move with collision
        MoveWithCollision(world);
    }
    
    protected virtual void MoveWithCollision(GameWorld world)
    {
        // Horizontal movement
        float newX = Position.X + Velocity.X;
        var horizontalBounds = new Rectangle(
            (int)newX + CollisionOffsetX,
            (int)Position.Y + CollisionOffsetY,
            CollisionWidth,
            CollisionHeight
        );
        
        if (!CheckCollision(world, horizontalBounds))
        {
            Position = new Vector2(newX, Position.Y);
        }
        else
        {
            // Slide along wall
            Velocity = new Vector2(0, Velocity.Y);
            
            // Snap to tile edge
            if (Velocity.X > 0)
            {
                int tileX = (horizontalBounds.Right) / GameConfig.TileSize;
                Position = new Vector2(tileX * GameConfig.TileSize - CollisionWidth - CollisionOffsetX - 0.01f, Position.Y);
            }
            else if (Velocity.X < 0)
            {
                int tileX = (horizontalBounds.Left) / GameConfig.TileSize;
                Position = new Vector2((tileX + 1) * GameConfig.TileSize - CollisionOffsetX + 0.01f, Position.Y);
            }
        }
        
        // Vertical movement
        float newY = Position.Y + Velocity.Y;
        var verticalBounds = new Rectangle(
            (int)Position.X + CollisionOffsetX,
            (int)newY + CollisionOffsetY,
            CollisionWidth,
            CollisionHeight
        );
        
        IsOnGround = false;
        
        if (!CheckCollision(world, verticalBounds))
        {
            Position = new Vector2(Position.X, newY);
        }
        else
        {
            // Hit ceiling or floor
            if (Velocity.Y > 0)
            {
                IsOnGround = true;
                int tileY = (verticalBounds.Bottom) / GameConfig.TileSize;
                Position = new Vector2(Position.X, tileY * GameConfig.TileSize - CollisionHeight - CollisionOffsetY);
            }
            else if (Velocity.Y < 0)
            {
                int tileY = (verticalBounds.Top) / GameConfig.TileSize;
                Position = new Vector2(Position.X, (tileY + 1) * GameConfig.TileSize - CollisionOffsetY + 0.01f);
            }
            
            Velocity = new Vector2(Velocity.X, 0);
        }
    }
    
    protected bool CheckCollision(GameWorld world, Rectangle bounds)
    {
        int startX = bounds.Left / GameConfig.TileSize;
        int startY = bounds.Top / GameConfig.TileSize;
        int endX = bounds.Right / GameConfig.TileSize;
        int endY = bounds.Bottom / GameConfig.TileSize;
        
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (world.IsSolid(x, y))
                {
                    var tileBounds = new Rectangle(
                        x * GameConfig.TileSize,
                        y * GameConfig.TileSize,
                        GameConfig.TileSize,
                        GameConfig.TileSize
                    );
                    
                    if (bounds.Intersects(tileBounds))
                        return true;
                }
            }
        }
        
        return false;
    }
    
    protected void CheckWater(GameWorld world)
    {
        int centerX = (int)Center.X / GameConfig.TileSize;
        int centerY = (int)Center.Y / GameConfig.TileSize;
        
        IsInWater = world.IsLiquid(centerX, centerY);
    }
    
    public virtual void TakeDamage(int damage, Vector2? knockback = null)
    {
        if (IsInvulnerable || IsDead) return;
        
        Health -= damage;
        InvulnerabilityTimer = 0.5f; // Half second of invulnerability
        
        if (knockback.HasValue)
        {
            Velocity += knockback.Value;
        }
        
        if (Health <= 0)
        {
            Health = 0;
            OnDeath();
        }
    }
    
    public virtual void Heal(int amount)
    {
        Health = Math.Min(Health + amount, MaxHealth);
    }
    
    protected virtual void OnDeath()
    {
        IsActive = false;
    }
    
    public abstract void Draw(SpriteBatch spriteBatch);
    
    /// <summary>
    /// Check if this entity intersects with another
    /// </summary>
    public bool Intersects(Entity other)
    {
        return CollisionBounds.Intersects(other.CollisionBounds);
    }
    
    /// <summary>
    /// Get the distance to another entity
    /// </summary>
    public float DistanceTo(Entity other)
    {
        return Vector2.Distance(Center, other.Center);
    }
    
    /// <summary>
    /// Get the direction to another entity (normalized)
    /// </summary>
    public Vector2 DirectionTo(Entity other)
    {
        var dir = other.Center - Center;
        if (dir.LengthSquared() > 0)
            dir.Normalize();
        return dir;
    }
}
