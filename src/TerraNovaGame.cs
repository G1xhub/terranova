using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TerraNova.Core;
using TerraNova.World;
using TerraNova.Entities;
using TerraNova.Systems;
using TerraNova.UI;
using FontStashSharp;
using System;

namespace TerraNova;

/// <summary>
/// Main game class - handles initialization, update loop, and rendering
/// </summary>
public class TerraNovaGame : Game
{
    // Graphics
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _gameRenderTarget = null!;
    
    // Core Systems
    private GameWorld _world = null!;
    private Player _player = null!;
    private Camera2D _camera = null!;
    private InputManager _input = null!;
    private ParticleSystem _particles = null!;
    private LightingSystem _lighting = null!;
    private ParallaxManager _parallax = null!;
    private UIManager _ui = null!;
    private float _totalGameTime = 0f;
    
    // Game State
    private GameState _currentState = GameState.Playing;
    private float _dayTime = 0.25f; // 0-1, 0.25 = 6:00 AM
    private const float DayDuration = 1200f; // Seconds per full day
    
    // Configuration
    public static GameConfig Config { get; private set; } = null!;
    
    // Debug
    private bool _showDebug = false;
    private FrameCounter _frameCounter = null!;
    
    // LightMap BlendState
    private readonly BlendState _multiplyBlend = new BlendState
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.SourceColor,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.SourceAlpha
    };

    public TerraNovaGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1920,
            PreferredBackBufferHeight = 1080,
            IsFullScreen = false,
            SynchronizeWithVerticalRetrace = true,
            PreferMultiSampling = false,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0); // 60 FPS
        
        Window.Title = "TerraNova";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    protected override void Initialize()
    {
        // Load configuration
        Config = GameConfig.Load();
        
        // Apply graphics settings
        ApplyGraphicsSettings();
        
        // Initialize input manager
        _input = new InputManager();
        
        // Initialize frame counter
        _frameCounter = new FrameCounter();
        
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        
        // Create render target for pixel-perfect scaling
        CreateRenderTarget();
        
        // Load textures
        TextureManager.Initialize(GraphicsDevice, Content);
        
        // Load fonts
        FontManager.Initialize(GraphicsDevice);
        
        // Initialize world
        var worldSeed = (int)DateTime.Now.Ticks;
        _world = new GameWorld(Config.WorldWidth, Config.WorldHeight, worldSeed);
        _world.Generate();
        
        // Initialize lighting system
        _lighting = new LightingSystem(_world, GraphicsDevice);
        
        // Initialize Parallax
        _parallax = new ParallaxManager(GraphicsDevice, Content);
        _parallax.Initialize();

        // Initialize particle system
        _particles = new ParticleSystem();
        
        // Find spawn point and create player
        var spawnPoint = _world.FindSpawnPoint();
        _player = new Player(spawnPoint);
        
        // Initialize camera with render target viewport
        var renderTargetViewport = new Viewport(0, 0, Config.GameWidth, Config.GameHeight);
        _camera = new Camera2D(renderTargetViewport);
        _camera.Follow(_player);
        
        // Initialize UI
        _ui = new UIManager(GraphicsDevice, _player);
    }


    protected override void UnloadContent()
    {
        _gameRenderTarget?.Dispose();
        _lighting?.Dispose();
        _parallax?.Dispose();
        TextureManager.Dispose();
        FontManager.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Update input
        _input.Update();
        
        // Handle global input
        HandleGlobalInput();
        
        if (_currentState == GameState.Playing)
        {
            // Update day/night cycle
            UpdateDayNightCycle(deltaTime);
            
            // Update player
            _player.Update(gameTime, _input, _world, _particles);
            
            // Update camera (Follow is only called once during initialization)
            _camera.Update(gameTime);
            _camera.ClampToWorld(_world.PixelWidth, _world.PixelHeight);
            
            // Update particles
            _particles.SpawnAmbientParticles(_world, _camera.VisibleArea);
            _particles.Update(gameTime);
            
            // Update lighting
            _lighting.Update(_dayTime);
            
            // Handle mining/building
            HandleWorldInteraction();
        }
        
        // Update UI
        _ui.Update(gameTime, _input);
        
        // Update frame counter
        _frameCounter.Update(deltaTime);
        
        // Track total game time for effects
        _totalGameTime += deltaTime;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Render game to render target
        GraphicsDevice.SetRenderTarget(_gameRenderTarget);
        
        // Sky color background
        Color skyColor = GetSkyColor();
        GraphicsDevice.Clear(skyColor);
        
        // 1. Parallax Background (Screen Space / Manual Camera)
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
        _parallax.Draw(_spriteBatch, _camera, _dayTime);
        _spriteBatch.End();
        
        // 2. Diffuse Pass - Draw World & Player (Using Camera Matrix)
        // Draw normal sprites (AlphaBlend)
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp, 
            null,
            null,
            null,
            _camera.TransformMatrix
        );
        
        _world.Draw(_spriteBatch, _camera, gameTime, _particles);
        _player.Draw(_spriteBatch);
        _particles.Draw(_spriteBatch, false); // Non-glowing particles
        
        _spriteBatch.End();
        
        // 3. Lighting Overlay Pass (Multiply)
        // Draw the light texture over the entire world
        // Use LinearClamp for smooth lighting interpolation!
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            _multiplyBlend,
            SamplerState.LinearClamp, 
            null,
            null,
            null,
            _camera.TransformMatrix
        );
        
        // Draw lightmap scaled to world
        _spriteBatch.Draw(_lighting.LightMap, new Rectangle(0, 0, _world.PixelWidth, _world.PixelHeight), Color.White);
        
        _spriteBatch.End();
        
        // 4. Glow/Emissive Pass (Additive)
        // Draw glowing tiles and particles on top of the darkened world
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.PointClamp, 
            null,
            null,
            null,
            _camera.TransformMatrix
        );
        
        _world.DrawEmissive(_spriteBatch, _camera);
        _particles.Draw(_spriteBatch, true); // Glowing particles
        
        _spriteBatch.End();
        
        // Draw to screen
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            null,
            null
        );
        
        // Draw scaled game
        DrawScaledGame();
        
        _spriteBatch.End();
        
        // Draw UI (not scaled)
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp
        );
        
        _ui.Draw(_spriteBatch, _dayTime);
        
        // Draw debug info
        if (_showDebug)
        {
            DrawDebugInfo();
        }
        
        _spriteBatch.End();
        
        base.Draw(gameTime);
    }

    private void HandleGlobalInput()
    {
        // Exit game
        if (_input.IsKeyPressed(Keys.Escape))
        {
            if (_currentState == GameState.Paused)
                _currentState = GameState.Playing;
            else if (_currentState == GameState.Playing)
                _currentState = GameState.Paused;
        }
        
        // Toggle debug
        if (_input.IsKeyPressed(Keys.F3))
        {
            _showDebug = !_showDebug;
        }
        
        // Toggle fullscreen
        if (_input.IsKeyPressed(Keys.F11))
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.ApplyChanges();
        }
        
        // Quick save
        if (_input.IsKeyDown(Keys.LeftControl) && _input.IsKeyPressed(Keys.S))
        {
            SaveGame();
        }
    }

    private void HandleWorldInteraction()
    {
        // Convert mouse position from window coordinates to render target coordinates
        Vector2 mousePos = ConvertMouseToRenderTarget(_input.MousePosition);
        var mouseWorldPos = _camera.ScreenToWorld(mousePos);
        int tileX = (int)(mouseWorldPos.X / GameConfig.TileSize);
        int tileY = (int)(mouseWorldPos.Y / GameConfig.TileSize);
        
        // Check if tile is in reach
        float distance = Vector2.Distance(_player.Center, new Vector2(
            tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
            tileY * GameConfig.TileSize + GameConfig.TileSize / 2
        ));
        
        bool inReach = distance <= Config.PlayerReach * GameConfig.TileSize;
        
        // Mining
        if (_input.IsMouseButtonDown(MouseButton.Left) && inReach)
        {
            _player.Mine(_world, tileX, tileY, _particles);
        }
        else
        {
            _player.ResetMining();
        }
        
        // Placing
        if (_input.IsMouseButtonPressed(MouseButton.Right) && inReach)
        {
            _player.PlaceBlock(_world, tileX, tileY);
        }
    }

    private void UpdateDayNightCycle(float deltaTime)
    {
        _dayTime += deltaTime / DayDuration;
        if (_dayTime >= 1f) _dayTime -= 1f;
    }

    private Color GetSkyColor()
    {
        // Sky color gradient based on time of day
        var skyColors = new[]
        {
            (0.00f, new Color(10, 10, 40)),     // Midnight
            (0.20f, new Color(20, 20, 60)),     // Pre-dawn
            (0.25f, new Color(255, 150, 100)),  // Sunrise
            (0.30f, new Color(135, 206, 235)),  // Morning
            (0.50f, new Color(100, 180, 255)),  // Noon
            (0.70f, new Color(135, 206, 235)),  // Afternoon
            (0.75f, new Color(255, 100, 50)),   // Sunset
            (0.80f, new Color(40, 20, 60)),     // Dusk
            (1.00f, new Color(10, 10, 40))      // Back to midnight
        };
        
        for (int i = 0; i < skyColors.Length - 1; i++)
        {
            if (_dayTime >= skyColors[i].Item1 && _dayTime <= skyColors[i + 1].Item1)
            {
                float t = (_dayTime - skyColors[i].Item1) / (skyColors[i + 1].Item1 - skyColors[i].Item1);
                return Color.Lerp(skyColors[i].Item2, skyColors[i + 1].Item2, t);
            }
        }
        
        return skyColors[0].Item2;
    }

    private void DrawScaledGame()
    {
        // Calculate scaling to fit window while maintaining aspect ratio
        float scaleX = (float)GraphicsDevice.Viewport.Width / _gameRenderTarget.Width;
        float scaleY = (float)GraphicsDevice.Viewport.Height / _gameRenderTarget.Height;
        float scale = Math.Min(scaleX, scaleY);
        
        int scaledWidth = (int)(_gameRenderTarget.Width * scale);
        int scaledHeight = (int)(_gameRenderTarget.Height * scale);
        int x = (GraphicsDevice.Viewport.Width - scaledWidth) / 2;
        int y = (GraphicsDevice.Viewport.Height - scaledHeight) / 2;
        
        _spriteBatch.Draw(
            _gameRenderTarget,
            new Rectangle(x, y, scaledWidth, scaledHeight),
            Color.White
        );
    }
    
    private Vector2 ConvertMouseToRenderTarget(Vector2 windowMousePos)
    {
        // Calculate scaling (same as in DrawScaledGame)
        float scaleX = (float)GraphicsDevice.Viewport.Width / _gameRenderTarget.Width;
        float scaleY = (float)GraphicsDevice.Viewport.Height / _gameRenderTarget.Height;
        float scale = Math.Min(scaleX, scaleY);
        
        int scaledWidth = (int)(_gameRenderTarget.Width * scale);
        int scaledHeight = (int)(_gameRenderTarget.Height * scale);
        int offsetX = (GraphicsDevice.Viewport.Width - scaledWidth) / 2;
        int offsetY = (GraphicsDevice.Viewport.Height - scaledHeight) / 2;
        
        // Convert from window coordinates to render target coordinates
        float renderTargetX = (windowMousePos.X - offsetX) / scale;
        float renderTargetY = (windowMousePos.Y - offsetY) / scale;
        
        // Clamp to render target bounds
        renderTargetX = MathHelper.Clamp(renderTargetX, 0, _gameRenderTarget.Width);
        renderTargetY = MathHelper.Clamp(renderTargetY, 0, _gameRenderTarget.Height);
        
        return new Vector2(renderTargetX, renderTargetY);
    }

    private void DrawDebugInfo()
    {
        var debugLines = new[]
        {
            $"FPS: {_frameCounter.AverageFramesPerSecond:F1}",
            $"Position: {_player.Position.X / GameConfig.TileSize:F1}, {_player.Position.Y / GameConfig.TileSize:F1}",
            $"Velocity: {_player.Velocity.X:F2}, {_player.Velocity.Y:F2}",
            $"OnGround: {_player.IsOnGround}",
            $"Time: {(_dayTime * 24):F1}:00",
            $"Particles: {_particles.Count}",
            $"Chunks Loaded: {_world.LoadedChunkCount}"
        };
        
        for (int i = 0; i < debugLines.Length; i++)
        {
            FontManager.DebugFont.DrawText(
                _spriteBatch,
                debugLines[i],
                new Vector2(10, 10 + i * 20),
                Color.White
            );
        }
    }

    private void ApplyGraphicsSettings()
    {
        _graphics.PreferredBackBufferWidth = Config.ScreenWidth;
        _graphics.PreferredBackBufferHeight = Config.ScreenHeight;
        _graphics.IsFullScreen = Config.Fullscreen;
        _graphics.ApplyChanges();
    }

    private void CreateRenderTarget()
    {
        _gameRenderTarget?.Dispose();
        _gameRenderTarget = new RenderTarget2D(
            GraphicsDevice,
            Config.GameWidth,
            Config.GameHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None
        );
    }

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        // Handle window resize - camera should use render target viewport, not window viewport
        if (Config != null)
        {
            var renderTargetViewport = new Viewport(0, 0, Config.GameWidth, Config.GameHeight);
            _camera?.UpdateViewport(renderTargetViewport);
        }
    }

    private void SaveGame()
    {
        // TODO: Implement save system
        Console.WriteLine("Game saved!");
    }

    private void LoadGame()
    {
        // TODO: Implement load system
    }
}


public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    Inventory,
    GameOver
}
