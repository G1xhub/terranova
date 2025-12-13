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
        float size = 4f, float lifetime = 1f, float gravity = 300f)
    {
        var particle = GetParticle();
        particle.Initialize(position, velocity, color, size, lifetime, gravity);
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
    public float Lifetime;
    public float MaxLifetime;
    public float Gravity;
    public float Rotation;
    public float RotationSpeed;
    
    public bool IsAlive => Lifetime > 0;
    public float Alpha => Math.Clamp(Lifetime / MaxLifetime, 0, 1);
    
    public void Initialize(Vector2 position, Vector2 velocity, Color color, 
        float size, float lifetime, float gravity = 300f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Size = size;
        Lifetime = lifetime;
        MaxLifetime = lifetime;
        Gravity = gravity;
        Rotation = (float)(Random.Shared.NextDouble() * Math.PI * 2);
        RotationSpeed = (float)(Random.Shared.NextDouble() * 10 - 5);
    }
    
    public void Update(float deltaTime)
    {
        Lifetime -= deltaTime;
        if (!IsAlive) return;
        
        Velocity.Y += Gravity * deltaTime;
        Position += Velocity * deltaTime;
        Rotation += RotationSpeed * deltaTime;
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
