using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.World;

namespace TerraNova.Systems;

/// <summary>
/// Particle system for visual effects (mining, explosions, etc.)
/// </summary>
public class ParticleSystem
{
    private readonly List<Particle> _particles = new();
    private readonly Queue<Particle> _particlePool = new();
    private const int PoolSize = 500;
    
    public int Count => _particles.Count;
    
    public ParticleSystem()
    {
        // Pre-allocate particle pool
        for (int i = 0; i < PoolSize; i++)
        {
            _particlePool.Enqueue(new Particle());
        }
    }
    
    public void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Update(deltaTime);
            
            if (!particle.IsAlive)
            {
                _particles.RemoveAt(i);
                _particlePool.Enqueue(particle);
            }
        }
    }
    
    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var particle in _particles)
        {
            particle.Draw(spriteBatch);
        }
    }
    
    private Particle GetParticle()
    {
        return _particlePool.Count > 0 ? _particlePool.Dequeue() : new Particle();
    }
    
    public void SpawnParticle(Vector2 position, Vector2 velocity, Color color, 
        float size = 4f, float lifetime = 1f, float gravity = 300f, ParticleType type = ParticleType.Default)
    {
        var particle = GetParticle();
        particle.Initialize(position, velocity, color, size, lifetime, gravity, type);
        _particles.Add(particle);
    }
    
    public void SpawnTileBreakParticle(Vector2 position, TileType tileType)
    {
        var color = GetTileColor(tileType);
        var velocity = new Vector2(
            (float)(Random.Shared.NextDouble() * 100 - 50),
            (float)(Random.Shared.NextDouble() * -80 - 20)
        );
        SpawnParticle(position, velocity, color, 3f, 0.5f);
    }
    
    public void SpawnTileBreakBurst(Vector2 position, TileType tileType, int count)
    {
        var color = GetTileColor(tileType);
        
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 100 + 50);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed - 50
            );
            
            float size = (float)(Random.Shared.NextDouble() * 3 + 2);
            SpawnParticle(position, velocity, color, size, 0.8f);
        }
    }
    
    // New particle types
    public void SpawnDustParticles(Vector2 position, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 40 + 20);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed
            );
            
            var dustColor = new Color(180, 160, 140, 200);
            float size = (float)(Random.Shared.NextDouble() * 2 + 1);
            SpawnParticle(position, velocity, dustColor, size, 1.5f, 50f, ParticleType.Dust); // Slow fall
        }
    }
    
    public void SpawnSparkParticles(Vector2 position, int count = 12)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 150 + 100);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed
            );
            
            var sparkColor = new Color(255, 220, 100);
            float size = (float)(Random.Shared.NextDouble() * 2 + 1);
            SpawnParticle(position, velocity, sparkColor, size, 0.3f, 200f, ParticleType.Spark);
        }
    }
    
    public void SpawnSmokeParticles(Vector2 position, int count = 6)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 30 + 10);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed - 20 // Upward drift
            );
            
            var smokeColor = new Color(80, 80, 80, 150);
            float size = (float)(Random.Shared.NextDouble() * 4 + 3);
            SpawnParticle(position, velocity, smokeColor, size, 2.0f, -50f, ParticleType.Smoke); // Upward float
        }
    }
    
    public void SpawnMagicParticles(Vector2 position, Color magicColor, int count = 15)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 80 + 40);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed
            );
            
            float size = (float)(Random.Shared.NextDouble() * 3 + 2);
            SpawnParticle(position, velocity, magicColor, size, 1.2f, 0f, ParticleType.Magic); // No gravity
        }
    }
    
    public void SpawnExplosionParticles(Vector2 position, int count = 30)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 200 + 100);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed
            );
            
            // Mix of fire colors
            var colors = new[] { Color.Orange, Color.Red, Color.Yellow, new Color(255, 100, 0) };
            var color = colors[Random.Shared.Next(colors.Length)];
            
            float size = (float)(Random.Shared.NextDouble() * 4 + 2);
            SpawnParticle(position, velocity, color, size, 0.8f, 100f);
        }
        
        // Add smoke
        SpawnSmokeParticles(position, 10);
    }
    
    public void SpawnMiningParticles(Vector2 position, TileType tileType, float miningProgress)
    {
        // Spawn particles based on mining progress
        if (miningProgress > 0.3f && miningProgress < 0.7f)
        {
            // Small particles during mining
            SpawnDustParticles(position, 2);
        }
        else if (miningProgress > 0.7f)
        {
            // More particles as it's about to break
            SpawnDustParticles(position, 4);
            if (Random.Shared.NextSingle() > 0.7f)
            {
                SpawnSparkParticles(position, 3);
            }
        }
    }
    
    public void SpawnHeatParticles(Vector2 position, TileType sourceType, int count = 5)
    {
        // Spawn heat particles for fire sources (Furnace, Lava)
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 20 + 10);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed * 0.3f, // Mostly upward
                (float)Math.Sin(angle) * speed - 30 // Strong upward movement
            );
            
            // Warm colors: red, orange, yellow gradient
            var heatColors = new[] { 
                new Color(255, 100, 50),   // Hot red
                new Color(255, 150, 70),   // Orange
                new Color(255, 200, 100)   // Warm yellow
            };
            var heatColor = heatColors[Random.Shared.Next(heatColors.Length)];
            
            float size = (float)(Random.Shared.NextDouble() * 2 + 1);
            float lifetime = (float)(Random.Shared.NextDouble() * 0.8 + 0.5);
            SpawnParticle(position, velocity, heatColor, size, lifetime, -50f, ParticleType.Smoke); // Negative gravity = upward
        }
    }
    
    public void SpawnAtmosphericDust(Vector2 position, int count = 3, float brightness = 0.5f)
    {
        // Spawn atmospheric dust particles for cozy feeling
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 15 + 5);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed * 0.5f,
                (float)Math.Sin(angle) * speed * 0.3f
            );
            
            // Warm, soft dust colors
            var dustColors = new[] {
                new Color((byte)220, (byte)200, (byte)180, (byte)(180 * brightness)), // Warm beige
                new Color((byte)240, (byte)220, (byte)200, (byte)(160 * brightness)), // Light warm
                new Color((byte)200, (byte)180, (byte)160, (byte)(200 * brightness))  // Soft brown
            };
            var dustColor = dustColors[Random.Shared.Next(dustColors.Length)];
            
            float size = (float)(Random.Shared.NextDouble() * 1.5 + 0.5);
            float lifetime = (float)(Random.Shared.NextDouble() * 3 + 2); // Longer lifetime for gentle movement
            SpawnParticle(position, velocity, dustColor, size, lifetime, 20f, ParticleType.Dust); // Slow fall
        }
    }
    
    public void SpawnPollen(Vector2 position)
    {
        // Spawn pollen particles during day - yellow/golden, floating upward
        for (int i = 0; i < 2; i++)
        {
            float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            float speed = (float)(Random.Shared.NextDouble() * 10 + 5);
            var velocity = new Vector2(
                (float)Math.Cos(angle) * speed * 0.3f,
                (float)Math.Sin(angle) * speed - 15 // Upward drift
            );
            
            // Pollen colors: yellow, golden, light yellow
            var pollenColors = new[] {
                new Color(255, 240, 150, 200), // Bright yellow
                new Color(255, 220, 120, 180),  // Golden
                new Color(250, 250, 180, 220)   // Light yellow
            };
            var pollenColor = pollenColors[Random.Shared.Next(pollenColors.Length)];
            
            float size = (float)(Random.Shared.NextDouble() * 1.5 + 1);
            float lifetime = (float)(Random.Shared.NextDouble() * 4 + 3); // Long lifetime
            SpawnParticle(position, velocity, pollenColor, size, lifetime, -30f, ParticleType.Dust); // Negative gravity = upward float
        }
    }
    
    public void SpawnWindParticle(Vector2 position)
    {
        // Spawn wind particles - subtle, transparent, horizontal movement
        float windSpeed = (float)(Random.Shared.NextDouble() * 20 + 10);
        var velocity = new Vector2(
            windSpeed * (Random.Shared.NextDouble() > 0.5 ? 1 : -1), // Random horizontal direction
            (float)(Random.Shared.NextDouble() * 5 - 2.5f) // Slight vertical variation
        );
        
        // Very subtle, transparent wind color
        var windColor = new Color(200, 220, 240, 80); // Light blue-gray, very transparent
        
        float size = (float)(Random.Shared.NextDouble() * 2 + 1);
        float lifetime = (float)(Random.Shared.NextDouble() * 2 + 1.5f);
        SpawnParticle(position, velocity, windColor, size, lifetime, 0f, ParticleType.Dust); // No gravity, just drift
    }
    
    public void SpawnDamageParticles(Vector2 position, int damage)
    {
        for (int i = 0; i < Math.Min(damage / 5, 20); i++)
        {
            var velocity = new Vector2(
                (float)(Random.Shared.NextDouble() * 80 - 40),
                (float)(Random.Shared.NextDouble() * -100 - 30)
            );
            SpawnParticle(position, velocity, Color.Red, 4f, 0.6f);
        }
    }
    
    public void SpawnHealParticles(Vector2 position, int amount)
    {
        for (int i = 0; i < Math.Min(amount / 10, 15); i++)
        {
            var velocity = new Vector2(
                (float)(Random.Shared.NextDouble() * 40 - 20),
                (float)(Random.Shared.NextDouble() * -60 - 20)
            );
            SpawnParticle(position, velocity, Color.LimeGreen, 3f, 0.8f, 50f);
        }
    }
    
    private static Color GetTileColor(TileType type)
    {
        return type switch
        {
            TileType.Dirt => new Color(139, 90, 43),
            TileType.Grass => new Color(34, 139, 34),
            TileType.Stone => new Color(105, 105, 105),
            TileType.Sand => new Color(244, 208, 63),
            TileType.Snow => new Color(240, 240, 255),
            TileType.Wood => new Color(139, 69, 19),
            TileType.CopperOre => new Color(184, 115, 51),
            TileType.IronOre => new Color(161, 157, 148),
            TileType.GoldOre => new Color(255, 215, 0),
            TileType.DiamondOre => new Color(0, 255, 255),
            TileType.Coal => new Color(44, 44, 44),
            _ => Color.Gray
        };
    }
    
    public void Clear()
    {
        foreach (var particle in _particles)
        {
            _particlePool.Enqueue(particle);
        }
        _particles.Clear();
    }
}

public class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color Color;
    public float Size;
    public float MaxSize;
    public float Lifetime;
    public float MaxLifetime;
    public float Gravity;
    public float Rotation;
    public float RotationSpeed;
    public float Friction = 0.98f; // Air resistance
    public ParticleType Type = ParticleType.Default;
    
    public bool IsAlive => Lifetime > 0;
    public float Alpha => Math.Clamp(Lifetime / MaxLifetime, 0, 1);
    
    public void Initialize(Vector2 position, Vector2 velocity, Color color, 
        float size, float lifetime, float gravity = 300f, ParticleType type = ParticleType.Default)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Size = size;
        MaxSize = size;
        Lifetime = lifetime;
        MaxLifetime = lifetime;
        Gravity = gravity;
        Rotation = (float)(Random.Shared.NextDouble() * Math.PI * 2);
        RotationSpeed = (float)(Random.Shared.NextDouble() * 10 - 5);
        Type = type;
        
        // Type-specific properties
        switch (type)
        {
            case ParticleType.Smoke:
                Friction = 0.95f; // Slower decay
                break;
            case ParticleType.Spark:
                Friction = 0.99f; // Fast decay
                break;
            case ParticleType.Magic:
                Friction = 1.0f; // No friction
                break;
        }
    }
    
    public void Update(float deltaTime)
    {
        Lifetime -= deltaTime;
        if (!IsAlive) return;
        
        // Apply gravity
        Velocity.Y += Gravity * deltaTime;
        
        // Apply friction
        Velocity *= Friction;
        
        // Update position
        Position += Velocity * deltaTime;
        Rotation += RotationSpeed * deltaTime;
        
        // Size variation based on type
        switch (Type)
        {
            case ParticleType.Smoke:
                // Smoke grows over time
                Size = MaxSize * (1f + (1f - Alpha) * 0.5f);
                break;
            case ParticleType.Spark:
                // Sparks shrink quickly
                Size = MaxSize * Alpha;
                break;
        }
    }
    
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsAlive) return;
        
        var drawColor = Color * Alpha;
        var rect = new Rectangle(
            (int)(Position.X - Size / 2),
            (int)(Position.Y - Size / 2),
            (int)Size,
            (int)Size
        );
        
        spriteBatch.Draw(TextureManager.Pixel, rect, drawColor);
    }
}

public enum ParticleType
{
    Default,
    Dust,
    Spark,
    Smoke,
    Magic,
    Explosion
}
