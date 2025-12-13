using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TerraNova.Core;
using TerraNova.World;
using TerraNova.Entities;
using TerraNova.Systems;
using TerraNova.UI;
using FontStashSharp;
using System.Text.Json;

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
    private UIManager _ui = null!;
    
    // Game State
    private GameState _currentState = GameState.Playing;
    private float _dayTime = 0.25f; // 0-1, 0.25 = 6:00 AM
    private const float DayDuration = 1200f; // Seconds per full day
    
    // Configuration
    public static GameConfig Config { get; private set; } = null!;
    
    // Debug
    private bool _showDebug = false;
    private FrameCounter _frameCounter = null!;
    
    // #region agent log helper
    private static readonly object _agentLogLock = new();
    private const string AgentLogPath = ".cursor/debug.log";
    private const string AgentSession = "debug-session";
    private const string AgentRun = "run-pre-fix";
    
    internal static void AgentLog(string location, string message, object data, string hypothesisId)
    {
        try
        {
            var payload = new
            {
                sessionId = AgentSession,
                runId = AgentRun,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            var line = JsonSerializer.Serialize(payload) + "\n";
            lock (_agentLogLock)
            {
                File.AppendAllText(AgentLogPath, line);
            }
        }
        catch
        {
            // swallow
        }
    }
    // #endregion

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
        AgentLog("TerraNovaGame.LoadContent", "world-generated", new
        {
            Config.WorldWidth,
            Config.WorldHeight,
            worldSeed,
            sampleCenterTile = _world.GetTile(Config.WorldWidth / 2, Config.SurfaceLevel)
        }, "H1-world-empty");
        
        // Initialize lighting system
        _lighting = new LightingSystem(_world, GraphicsDevice);
        
        // Initialize particle system
        _particles = new ParticleSystem();
        
        // Find spawn point and create player
        var spawnPoint = _world.FindSpawnPoint();
        _player = new Player(spawnPoint);
        AgentLog("TerraNovaGame.LoadContent", "player-spawn", new
        {
            spawnPointX = spawnPoint.X,
            spawnPointY = spawnPoint.Y,
            spawnChunkX = (int)(spawnPoint.X / GameConfig.TileSize / Chunk.Size),
            spawnChunkY = (int)(spawnPoint.Y / GameConfig.TileSize / Chunk.Size)
        }, "H2-player-offscreen");
        
        // Initialize camera with render target size (not screen viewport)
        var renderTargetViewport = new Viewport(0, 0, Config.GameWidth, Config.GameHeight);
        _camera = new Camera2D(renderTargetViewport);
        _camera.Follow(_player);
        
        // Initialize UI
        _ui = new UIManager(GraphicsDevice, _player, _world);
    }

    protected override void UnloadContent()
    {
        _gameRenderTarget?.Dispose();
        _lighting?.Dispose();
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
            
            // Update camera
            _camera.Follow(_player);
            _camera.Update(gameTime);
            _camera.ClampToWorld(_world.PixelWidth, _world.PixelHeight);
            
            // Update particles
            _particles.Update(gameTime);
            
            // Update lighting (only when needed)
            if (_world.LightingDirty)
            {
                _lighting.Update(_dayTime);
                _world.LightingDirty = false;
            }
            
            // Handle mining/building
            HandleWorldInteraction();
        }
        
        // Update UI
        _ui.Update(gameTime, _input);
        
        // Update frame counter
        _frameCounter.Update(deltaTime);
        
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Render game to render target for pixel-perfect scaling
        GraphicsDevice.SetRenderTarget(_gameRenderTarget);
        GraphicsDevice.Clear(GetSkyColor());
        
        // #region agent log
        TerraNovaGame.AgentLog("TerraNovaGame.Draw", "render-target-setup", new { 
            renderTargetWidth = _gameRenderTarget.Width, 
            renderTargetHeight = _gameRenderTarget.Height,
            viewportWidth = GraphicsDevice.Viewport.Width,
            viewportHeight = GraphicsDevice.Viewport.Height,
            cameraPositionX = _camera.Position.X,
            cameraPositionY = _camera.Position.Y,
            cameraZoom = _camera.Zoom,
            transformM11 = _camera.TransformMatrix.M11,
            transformM12 = _camera.TransformMatrix.M12,
            transformM21 = _camera.TransformMatrix.M21,
            transformM22 = _camera.TransformMatrix.M22,
            transformM41 = _camera.TransformMatrix.M41,
            transformM42 = _camera.TransformMatrix.M42
        }, "H1-render-target-mismatch");
        // #endregion
        
        // Begin sprite batch with camera transform
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp, // Pixel-perfect rendering
            null,
            null,
            null,
            _camera.TransformMatrix
        );
        
        // Draw background (parallax layers)
        DrawBackground();
        
        // Draw world tiles
        _world.Draw(_spriteBatch, _camera);
        
        // Lighting overlay disabled - lighting applied per-tile in Chunk.Draw
        // _lighting.Draw(_spriteBatch, _camera);
        
        // Draw entities
        // #region agent log
        TerraNovaGame.AgentLog("TerraNovaGame.Draw", "before-player-draw", new { playerPositionX = _player.Position.X, playerPositionY = _player.Position.Y, cameraPositionX = _camera.Position.X, cameraPositionY = _camera.Position.Y, visibleArea = _camera.VisibleArea }, "H3-player-offscreen");
        // #endregion
        _player.Draw(_spriteBatch);
        // #region agent log
        TerraNovaGame.AgentLog("TerraNovaGame.Draw", "after-player-draw", new { }, "H3-player-offscreen");
        // #endregion
        
        // Draw particles
        _particles.Draw(_spriteBatch);
        
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
        var mouseWorldPos = _camera.ScreenToWorld(_input.MousePosition);
        int tileX = (int)(mouseWorldPos.X / GameConfig.TileSize);
        int tileY = (int)(mouseWorldPos.Y / GameConfig.TileSize);
        
        // Check if tile is in reach
        float distance = Vector2.Distance(_player.Center, new Vector2(
            tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
            tileY * GameConfig.TileSize + GameConfig.TileSize / 2
        ));
        
        bool inReach = distance <= Config.PlayerReach * GameConfig.TileSize;
        
        // #region agent log
        if (_input.IsMouseButtonDown(MouseButton.Left))
        {
            AgentLog("TerraNovaGame.HandleWorldInteraction", "mining-attempt", new {
                mouseScreenX = _input.MousePosition.X,
                mouseScreenY = _input.MousePosition.Y,
                mouseWorldX = mouseWorldPos.X,
                mouseWorldY = mouseWorldPos.Y,
                tileX,
                tileY,
                distance,
                playerReach = Config.PlayerReach * GameConfig.TileSize,
                inReach,
                tileType = _world.GetTile(tileX, tileY).ToString()
            }, "H1-mining-not-working");
        }
        // #endregion
        
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

    private void DrawBackground()
    {
        var visible = _camera.VisibleArea;
        var layers = TextureManager.ParallaxLayers;
        if (layers.Count == 0) return;
        
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            float factor = 0.18f + i * 0.12f;
            float alpha = 0.35f + (layers.Count - i) * 0.12f;
            
            int bgWidth = layer.Width;
            int bgHeight = layer.Height;
            
            int offsetX = (int)(-_camera.Position.X * factor) % bgWidth;
            int offsetY = (int)(visible.Top * factor * 0.25f);
            
            for (int x = offsetX - bgWidth; x < visible.Width + bgWidth; x += bgWidth)
            {
                _spriteBatch.Draw(
                    layer,
                    new Rectangle(visible.Left + x, visible.Top + offsetY, bgWidth, bgHeight),
                    Color.White * alpha
                );
            }
        }
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
        // Handle window resize - camera should use render target viewport, not screen viewport
        if (Config.GameWidth > 0 && Config.GameHeight > 0)
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
