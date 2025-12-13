using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraNova.Core;
using TerraNova.World;

namespace TerraNova.Systems;

/// <summary>
/// Weather system for rain, snow, and sandstorms
/// Weather is biome-dependent and affects visibility
/// </summary>
public class WeatherSystem
{
    private readonly List<WeatherParticle> _particles = new();
    private readonly Queue<WeatherParticle> _particlePool = new();
    private const int PoolSize = 1000;
    
    private WeatherType _currentWeather = WeatherType.None;
    private float _weatherIntensity = 0f; // 0-1
    private BiomeType _currentBiome = BiomeType.Forest;
    private float _windSpeed = 0f;
    
    private readonly GameWorld _world;
    private readonly GraphicsDevice _graphicsDevice;
    
    public WeatherType CurrentWeather => _currentWeather;
    public float WeatherIntensity => _weatherIntensity;
    
    public WeatherSystem(GameWorld world, GraphicsDevice graphicsDevice)
    {
        _world = world;
        _graphicsDevice = graphicsDevice;
        
        // Pre-allocate particle pool
        for (int i = 0; i < PoolSize; i++)
        {
            _particlePool.Enqueue(new WeatherParticle());
        }
    }
    
    public void Update(GameTime gameTime, Vector2 cameraPosition, BiomeType biome)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        _currentBiome = biome;
        
        // Update weather based on biome
        UpdateWeatherForBiome();
        
        // Update wind
        _windSpeed = CalculateWindSpeed();
        
        // Update existing particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Update(deltaTime, _windSpeed);
            
            if (!particle.IsAlive)
            {
                _particles.RemoveAt(i);
                _particlePool.Enqueue(particle);
            }
        }
        
        // Spawn new particles based on weather
        if (_currentWeather != WeatherType.None && _weatherIntensity > 0.1f)
        {
            SpawnWeatherParticles(cameraPosition, deltaTime);
        }
    }
    
    private void UpdateWeatherForBiome()
    {
        // Weather probability based on biome
        float weatherChance = _currentBiome switch
        {
            BiomeType.Desert => 0.3f, // Sandstorms
            BiomeType.Snow => 0.4f,   // Snow
            BiomeType.Jungle => 0.5f, // Rain
            BiomeType.Forest => 0.3f, // Rain
            _ => 0.2f
        };
        
        // Randomly change weather
        if (Random.Shared.NextSingle() < weatherChance * 0.01f) // 1% chance per frame
        {
            _currentWeather = _currentBiome switch
            {
                BiomeType.Desert => Random.Shared.NextSingle() < 0.7f ? WeatherType.Sandstorm : WeatherType.None,
                BiomeType.Snow => Random.Shared.NextSingle() < 0.8f ? WeatherType.Snow : WeatherType.None,
                BiomeType.Jungle => Random.Shared.NextSingle() < 0.9f ? WeatherType.Rain : WeatherType.None,
                BiomeType.Forest => Random.Shared.NextSingle() < 0.6f ? WeatherType.Rain : WeatherType.None,
                _ => WeatherType.None
            };
            
            _weatherIntensity = Random.Shared.NextSingle() * 0.5f + 0.3f; // 0.3-0.8
        }
        
        // Gradually fade weather
        if (_currentWeather != WeatherType.None)
        {
            _weatherIntensity -= 0.001f; // Fade over time
            if (_weatherIntensity <= 0)
            {
                _currentWeather = WeatherType.None;
            }
        }
    }
    
    private float CalculateWindSpeed()
    {
        return _currentWeather switch
        {
            WeatherType.Rain => 20f + _weatherIntensity * 30f,
            WeatherType.Snow => 10f + _weatherIntensity * 20f,
            WeatherType.Sandstorm => 50f + _weatherIntensity * 80f,
            _ => 0f
        };
    }
    
    private void SpawnWeatherParticles(Vector2 cameraPosition, float deltaTime)
    {
        // Calculate spawn area (slightly larger than viewport)
        float spawnWidth = 1920; // Approximate viewport width
        float spawnHeight = 1080; // Approximate viewport height
        float spawnX = cameraPosition.X - spawnWidth / 2;
        float spawnY = cameraPosition.Y - spawnHeight / 2;
        
        int particlesPerSecond = (int)(_weatherIntensity * GetParticleRate());
        int particlesThisFrame = (int)(particlesPerSecond * deltaTime);
        
        for (int i = 0; i < particlesThisFrame && _particlePool.Count > 0; i++)
        {
            var particle = _particlePool.Dequeue();
            
            float x = spawnX + Random.Shared.NextSingle() * spawnWidth;
            float y = spawnY - 100; // Spawn above viewport
            
            particle.Initialize(x, y, _currentWeather, _weatherIntensity);
            _particles.Add(particle);
        }
    }
    
    private int GetParticleRate()
    {
        return _currentWeather switch
        {
            WeatherType.Rain => 200,
            WeatherType.Snow => 150,
            WeatherType.Sandstorm => 300,
            _ => 0
        };
    }
    
    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (_currentWeather == WeatherType.None) return;
        
        var visibleArea = camera.VisibleArea;
        
        foreach (var particle in _particles)
        {
            // Only draw particles in visible area
            if (visibleArea.Contains(particle.Position))
            {
                particle.Draw(spriteBatch);
            }
        }
    }
    
    public void SetWeather(WeatherType weather, float intensity = 0.5f)
    {
        _currentWeather = weather;
        _weatherIntensity = MathHelper.Clamp(intensity, 0f, 1f);
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

public enum WeatherType
{
    None,
    Rain,
    Snow,
    Sandstorm
}

public class WeatherParticle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color Color;
    public float Size;
    public float Lifetime;
    public float MaxLifetime;
    public WeatherType Type;
    
    public bool IsAlive => Lifetime > 0;
    public float Alpha => Math.Clamp(Lifetime / MaxLifetime, 0, 1);
    
    public void Initialize(float x, float y, WeatherType type, float intensity)
    {
        Position = new Vector2(x, y);
        Type = type;
        
        switch (type)
        {
            case WeatherType.Rain:
                Velocity = new Vector2(
                    Random.Shared.NextSingle() * 40f - 20f + intensity * 30f,
                    200f + intensity * 200f
                );
                Color = new Color(150, 180, 220, 180);
                Size = 2f + intensity * 2f;
                MaxLifetime = 2f;
                break;
                
            case WeatherType.Snow:
                Velocity = new Vector2(
                    Random.Shared.NextSingle() * 30f - 15f + intensity * 20f,
                    50f + intensity * 50f
                );
                Color = new Color(240, 250, 255, 200);
                Size = 3f + intensity * 2f;
                MaxLifetime = 5f;
                break;
                
            case WeatherType.Sandstorm:
                Velocity = new Vector2(
                    80f + intensity * 100f + Random.Shared.NextSingle() * 40f,
                    Random.Shared.NextSingle() * 40f - 20f
                );
                Color = new Color(200, 180, 140, 150);
                Size = 4f + intensity * 3f;
                MaxLifetime = 3f;
                break;
        }
        
        Lifetime = MaxLifetime;
    }
    
    public void Update(float deltaTime, float windSpeed)
    {
        Lifetime -= deltaTime;
        if (!IsAlive) return;
        
        // Apply wind
        Velocity.X += windSpeed * deltaTime * 0.1f;
        
        // Update position
        Position += Velocity * deltaTime;
        
        // Snow drifts
        if (Type == WeatherType.Snow)
        {
            Velocity.X += MathF.Sin(Position.X * 0.01f) * 10f * deltaTime;
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

