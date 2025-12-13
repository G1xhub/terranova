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
    private RenderTarget2D _postProcessRenderTarget = null!; // For post-processing
    
    // Core Systems
    private GameWorld _world = null!;
    private Player _player = null!;
    private Camera2D _camera = null!;
    private InputManager _input = null!;
    private ParticleSystem _particles = null!;
    private LightingSystem _lighting = null!;
    private WeatherSystem _weather = null!;
    private EventSystem _events = null!;
    private UIManager _ui = null!;
    
    // Game State
    private GameState _currentState = GameState.Playing;
    private float _dayTime = 0.25f; // 0-1, 0.25 = 6:00 AM
    private const float DayDuration = 1200f; // Seconds per full day
    
    // Heat-based color filters
    private Dictionary<(int x, int y), float> _heatInfluenceMap = new();
    private float _heatUpdateTimer = 0f;
    private const float HeatUpdateInterval = 0.1f; // Update every 0.1 seconds instead of every frame
    
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
        _world.SetLightingSystem(_lighting);
        
        // Initialize particle system
        _particles = new ParticleSystem();
        
        // Initialize weather system
        _weather = new WeatherSystem(_world, GraphicsDevice);
        
        // Initialize event system
        _events = new EventSystem(GraphicsDevice, Config.GameWidth, Config.GameHeight);
        
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
        _postProcessRenderTarget?.Dispose();
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
            
            // Update weather
            if (_weather != null)
            {
                var playerBiome = _world.GetBiomeAt(
                    (int)(_player.Position.X / GameConfig.TileSize),
                    (int)(_player.Position.Y / GameConfig.TileSize)
                );
                _weather.Update(gameTime, _camera.Center, playerBiome);
            }
            
            // Update events
            _events?.Update(gameTime);
            
            // Update heat-based color filters (warm tones near fire sources) - throttled
            _heatUpdateTimer += deltaTime;
            if (_heatUpdateTimer >= HeatUpdateInterval)
            {
                UpdateHeatColorFilters(_heatUpdateTimer, _camera.VisibleArea);
                _world.SetHeatInfluenceMap(_heatInfluenceMap);
                _heatUpdateTimer = 0f;
            }
            
            // Update lighting (only when needed)
            if (_world.LightingDirty)
            {
                _lighting.Update(_dayTime, deltaTime, _camera.VisibleArea);
                _world.LightingDirty = false;
            }
            else
            {
                // Still update for dynamic effects even if lighting isn't dirty
                _lighting.Update(_dayTime, deltaTime, _camera.VisibleArea);
            }
            
            // Handle mining/building (only if no menu is open)
            if (!_ui.IsAnyMenuOpen)
            {
                HandleWorldInteraction();
            }
            
            // Spawn atmospheric dust particles in well-lit areas for cozy feeling
            SpawnAtmosphericParticles();
        }
        
        // Update UI
        _ui.Update(gameTime, _input);
        
        // Update frame counter
        _frameCounter.Update(deltaTime);
        
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        try
    {
        // Render game to render target for pixel-perfect scaling
        GraphicsDevice.SetRenderTarget(_gameRenderTarget);
            
            // Determine background color - dark for underground, sky color for surface
            // Underground background will be drawn in DrawBackground()
        var visible = _camera.VisibleArea;
        int tileSize = GameConfig.TileSize;
        int visibleBottomTile = Math.Min(_world.Height - 1, (visible.Bottom + tileSize - 1) / tileSize);
        int surfaceLevel = Config.SurfaceLevel;
        bool isUnderground = visibleBottomTile > surfaceLevel;
        
        Color clearColor = isUnderground 
            ? new Color(20, 20, 30)  // Dark for underground
            : GetSkyColor();          // Sky color for surface
        GraphicsDevice.Clear(clearColor);
        
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
        
        // Draw sun/moon based on time of day
        DrawCelestialBodies();
        
        // Draw world tiles (with particle system for heat effects)
        _world.Draw(_spriteBatch, _camera, gameTime, _particles);
        
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
        
        // Draw weather (after particles, before UI)
        _weather?.Draw(_spriteBatch, _camera);
        
        // Draw event overlays (after everything, before UI)
        _events?.Draw(_spriteBatch, _gameRenderTarget);
        
        _spriteBatch.End();
        
        // Post-processing: Apply color grading
        GraphicsDevice.SetRenderTarget(_postProcessRenderTarget);
        GraphicsDevice.Clear(Color.Black);
        
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            null,
            null
        );
        
        // Apply color grading based on time of day
        Color colorGrade = GetColorGrading(_dayTime);
        _spriteBatch.Draw(_gameRenderTarget, Vector2.Zero, colorGrade);
        
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
        
        // Draw scaled game (with post-processing applied)
        DrawScaledGamePostProcessed();
        
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
        catch (Exception ex)
        {
            AgentLog("TerraNovaGame.Draw", "exception", new { 
                message = ex.Message, 
                stackTrace = ex.StackTrace,
                innerException = ex.InnerException?.Message 
            }, "H1-freeze-debug");
            throw;
        }
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
        // Convert mouse position from screen to render target coordinates
        Vector2 mouseScreenPos = _input.MousePosition;
        Vector2 mouseRenderTargetPos = ScreenToRenderTarget(mouseScreenPos);
        
        // Convert to world coordinates
        var mouseWorldPos = _camera.ScreenToWorld(mouseRenderTargetPos);
        int tileX = (int)(mouseWorldPos.X / GameConfig.TileSize);
        int tileY = (int)(mouseWorldPos.Y / GameConfig.TileSize);
        
        // Check if tile is in reach
        float distance = Vector2.Distance(_player.Center, new Vector2(
            tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
            tileY * GameConfig.TileSize + GameConfig.TileSize / 2
        ));
        
        bool inReach = distance <= Config.PlayerReach * GameConfig.TileSize;
        
        // #region agent log
        if (_input.IsMouseButtonDown(MouseButton.Left) || _input.IsMouseButtonPressed(MouseButton.Right))
        {
            AgentLog("TerraNovaGame.HandleWorldInteraction", "interaction-attempt", new {
                mouseScreenX = mouseScreenPos.X,
                mouseScreenY = mouseScreenPos.Y,
                mouseRenderTargetX = mouseRenderTargetPos.X,
                mouseRenderTargetY = mouseRenderTargetPos.Y,
                mouseWorldX = mouseWorldPos.X,
                mouseWorldY = mouseWorldPos.Y,
                tileX,
                tileY,
                distance,
                playerReach = Config.PlayerReach * GameConfig.TileSize,
                inReach,
                tileType = _world.GetTile(tileX, tileY).ToString(),
                playerCenterX = _player.Center.X,
                playerCenterY = _player.Center.Y
            }, "H1-mining-not-working");
        }
        // #endregion
        
        // Mining
        if (_input.IsMouseButtonDown(MouseButton.Left) && inReach)
        {
            var selectedItem = _player.Inventory.GetItem(_player.SelectedSlot);
            float miningPower = _player.GetMiningPower();
            
            AgentLog("TerraNovaGame.HandleWorldInteraction", "mining-action", new {
                tileX,
                tileY,
                selectedSlot = _player.SelectedSlot,
                selectedItemType = selectedItem?.Type.ToString() ?? "None",
                miningPower,
                inReach
            }, "H2-mining-details");
            
            _player.Mine(_world, tileX, tileY, _particles);
        }
        else
        {
            _player.ResetMining();
        }
        
        // Placing
        if (_input.IsMouseButtonPressed(MouseButton.Right) && inReach)
        {
            var selectedItem = _player.Inventory.GetItem(_player.SelectedSlot);
            
            AgentLog("TerraNovaGame.HandleWorldInteraction", "placement-attempt", new {
                tileX,
                tileY,
                selectedSlot = _player.SelectedSlot,
                selectedItemType = selectedItem?.Type.ToString() ?? "None",
                selectedItemCount = selectedItem?.Count ?? 0,
                tileAtPosition = _world.GetTile(tileX, tileY).ToString(),
                inReach
            }, "H3-placement-details");
            
            _player.PlaceBlock(_world, tileX, tileY);
        }
    }

    private void UpdateHeatColorFilters(float deltaTime, Rectangle visibleArea)
    {
        // Only update heat influence for visible area (with padding for off-screen effects)
        int padding = 10; // Tiles of padding
        int startTileX = Math.Max(0, (visibleArea.Left / GameConfig.TileSize) - padding);
        int startTileY = Math.Max(0, (visibleArea.Top / GameConfig.TileSize) - padding);
        int endTileX = Math.Min(_world.Width - 1, (visibleArea.Right / GameConfig.TileSize) + padding);
        int endTileY = Math.Min(_world.Height - 1, (visibleArea.Bottom / GameConfig.TileSize) + padding);
        
        // Remove heat influence outside visible area
        var keysToRemove = new List<(int x, int y)>();
        foreach (var key in _heatInfluenceMap.Keys)
        {
            if (key.x < startTileX || key.x > endTileX || key.y < startTileY || key.y > endTileY)
            {
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove)
        {
            _heatInfluenceMap.Remove(key);
        }
        
        // Find fire sources and calculate heat influence
        for (int y = startTileY; y <= endTileY; y++)
        {
            for (int x = startTileX; x <= endTileX; x++)
            {
                var tile = _world.GetTile(x, y);
                if (tile == TileType.Furnace || tile == TileType.Lava || tile == TileType.Torch)
                {
                    // Calculate heat influence radius
                    int heatRadius = tile == TileType.Lava ? 8 : (tile == TileType.Furnace ? 6 : 4);
                    
                    // Apply heat influence to surrounding tiles (clamped to visible area)
                    int radius = (int)heatRadius;
                    int influenceStartX = Math.Max(startTileX, x - radius);
                    int influenceStartY = Math.Max(startTileY, y - radius);
                    int influenceEndX = Math.Min(endTileX, x + radius);
                    int influenceEndY = Math.Min(endTileY, y + radius);
                    
                    for (int ty = influenceStartY; ty <= influenceEndY; ty++)
                    {
                        for (int tx = influenceStartX; tx <= influenceEndX; tx++)
                        {
                            int dx = tx - x;
                            int dy = ty - y;
                            float distance = MathF.Sqrt(dx * dx + dy * dy);
                            if (distance > heatRadius) continue;
                            
                            float influence = 1f - (distance / heatRadius);
                            influence = MathF.Pow(influence, 1.5f); // Softer falloff
                            
                            if (_heatInfluenceMap.TryGetValue((tx, ty), out var existing))
                            {
                                _heatInfluenceMap[(tx, ty)] = Math.Max(existing, influence);
                            }
                            else
                            {
                                _heatInfluenceMap[(tx, ty)] = influence;
                            }
                        }
                    }
                }
            }
        }
    }
    
    private void SpawnAtmosphericParticles()
    {
        // Spawn atmospheric particles (dust, pollen, etc.) for better atmosphere
        if (_particles == null) return;
        
        var visible = _camera.VisibleArea;
        
        // Spawn dust particles (more frequent)
        if (Random.Shared.NextSingle() < 0.03f) // 3% chance per frame
        {
            int centerX = visible.Left + visible.Width / 2;
            int centerY = visible.Top + visible.Height / 2;
            
            // Check light level at center
            int tileX = centerX / GameConfig.TileSize;
            int tileY = centerY / GameConfig.TileSize;
            
            if (tileX >= 0 && tileX < _world.Width && tileY >= 0 && tileY < _world.Height)
            {
                var lightColor = _lighting.GetLightColor(tileX, tileY);
                float brightness = (lightColor.R + lightColor.G + lightColor.B) / (3f * 255f);
                
                // Spawn in well-lit areas (brightness > 0.25)
                if (brightness > 0.25f)
                {
                    var spawnPos = new Vector2(
                        centerX + (float)(Random.Shared.NextDouble() * visible.Width - visible.Width / 2),
                        centerY + (float)(Random.Shared.NextDouble() * visible.Height - visible.Height / 2)
                    );
                    _particles.SpawnAtmosphericDust(spawnPos, 3, brightness); // Increased count from 2 to 3
                }
            }
        }
        
        // Spawn pollen particles during day (less frequent, more visible)
        if (IsDaytime() && Random.Shared.NextSingle() < 0.01f) // 1% chance per frame during day
        {
            var spawnPos = new Vector2(
                visible.Left + (float)(Random.Shared.NextDouble() * visible.Width),
                visible.Top + (float)(Random.Shared.NextDouble() * visible.Height)
            );
            _particles.SpawnPollen(spawnPos);
        }
        
        // Spawn wind particles for leaves/grass (subtle movement)
        if (Random.Shared.NextSingle() < 0.015f) // 1.5% chance per frame
        {
            var spawnPos = new Vector2(
                visible.Left + (float)(Random.Shared.NextDouble() * visible.Width),
                visible.Top + (float)(Random.Shared.NextDouble() * visible.Height * 0.3f) // Upper part of screen
            );
            _particles.SpawnWindParticle(spawnPos);
        }
    }

    private void UpdateDayNightCycle(float deltaTime)
    {
        // Time speed multiplier (can be adjusted for faster/slower cycles)
        float timeSpeed = 1.0f; // 1.0 = normal speed, 2.0 = 2x faster, etc.
        
        _dayTime += (deltaTime / DayDuration) * timeSpeed;
        if (_dayTime >= 1f) _dayTime -= 1f;
    }
    
    public bool IsDaytime()
    {
        // Daytime: 0.25 (6:00 AM) to 0.75 (6:00 PM)
        return _dayTime >= 0.25f && _dayTime <= 0.75f;
    }
    
    public bool IsNighttime()
    {
        return !IsDaytime();
    }
    
    public string GetTimeOfDayString()
    {
        float hours = _dayTime * 24f;
        int hour = (int)hours;
        int minute = (int)((hours - hour) * 60);
        
        string period = hour < 12 ? "AM" : "PM";
        int displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;
        
        return $"{displayHour:D2}:{minute:D2} {period}";
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
        try
        {
            var visible = _camera.VisibleArea;
            
            // Safety checks
            if (_world == null || visible.Width <= 0 || visible.Height <= 0)
                return;
            
            int tileSize = GameConfig.TileSize;
            if (tileSize <= 0) return;
            
            // Calculate visible area in tile coordinates
            int visibleTopTile = Math.Max(0, visible.Top / tileSize);
            int visibleBottomTile = Math.Min(_world.Height - 1, (visible.Bottom + tileSize - 1) / tileSize);
            int surfaceLevel = Config.SurfaceLevel;
        
        // Check if any part of visible area is underground (Y increases downward, so underground = Y > surfaceLevel)
        bool hasUndergroundArea = visibleBottomTile > surfaceLevel;
        
        if (hasUndergroundArea)
        {
            // Draw sky for surface area
            if (visibleTopTile < surfaceLevel)
            {
                int skyHeight = (surfaceLevel - visibleTopTile) * tileSize;
                if (skyHeight > 0)
                {
                    DrawSkyBackground(new Rectangle(visible.Left, visible.Top, visible.Width, skyHeight));
                }
            }
            
            // Draw underground background with earth tiles for depth
            int undergroundTop = Math.Max(visibleTopTile, surfaceLevel);
            int undergroundTopPixel = undergroundTop * tileSize;
            int undergroundHeight = visible.Bottom - undergroundTopPixel;
            
            if (undergroundHeight > 0 && undergroundTop < _world.Height)
            {
                Rectangle undergroundArea = new Rectangle(
                    visible.Left, 
                    undergroundTopPixel, 
                    visible.Width, 
                    undergroundHeight
                );
                
                int depthBelowSurface = undergroundTop - surfaceLevel;
                float depthFactor = MathHelper.Clamp(depthBelowSurface / 100f, 0f, 1f);
                
                DrawFullCaveBackground(undergroundArea, depthFactor, undergroundTop);
            }
        }
        else
        {
            // Draw normal sky background
            DrawSkyBackground(visible);
        }
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            System.Diagnostics.Debug.WriteLine($"Error in DrawBackground: {ex.Message}");
        }
    }
    
    private void DrawSkyBackground(Rectangle visible)
    {
        var layers = TextureManager.ParallaxLayers;
        if (layers.Count == 0) return;
        
        // Adjust background brightness based on time of day
        float brightnessTransition = GetTimeBrightness();
        
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            // Different parallax speeds for each layer (more variation)
            float factor = 0.1f + i * 0.08f; // Slower, more varied speeds
            float alpha = (0.4f - i * 0.08f) * brightnessTransition; // Fade with distance
            alpha = MathHelper.Clamp(alpha, 0.1f, 1f);
            
            // Atmospheric perspective: lighter, less saturated for distant layers
            float distanceFactor = i / (float)layers.Count;
            float saturation = 1f - distanceFactor * 0.4f; // Less saturated
            float lightness = 1f + distanceFactor * 0.2f; // Lighter
            
            int bgWidth = layer.Width;
            int bgHeight = layer.Height;
            
            int offsetX = (int)(-_camera.Position.X * factor) % bgWidth;
            int offsetY = (int)(visible.Top * factor * 0.25f);
            
            // Apply atmospheric perspective color tint
            Color layerColor = new Color(
                (byte)(255 * lightness * saturation),
                (byte)(255 * lightness * saturation),
                (byte)(255 * lightness),
                (byte)(255 * alpha)
            );
            
            for (int x = offsetX - bgWidth; x < visible.Width + bgWidth; x += bgWidth)
            {
                _spriteBatch.Draw(
                    layer,
                    new Rectangle(visible.Left + x, visible.Top + offsetY, bgWidth, bgHeight),
                    layerColor
                );
            }
        }
    }
    
    private void DrawFullCaveBackground(Rectangle visible, float depthFactor, int startTileY)
    {
        // Draw full cave background with multiple layers - always draws tiles (even if foreground tiles exist)
        // Creates a complete cave wall effect instead of just showing blue background
        
        // Safety checks
        if (_world == null || TextureManager.TileAtlas == null)
            return;
        
        int tileSize = GameConfig.TileSize;
        if (tileSize <= 0) return;
        
        // Clamp tile coordinates to world bounds
        int startTileX = Math.Max(0, visible.Left / tileSize);
        int endTileX = Math.Min(_world.Width - 1, (visible.Right + tileSize - 1) / tileSize);
        int endTileY = Math.Min(_world.Height - 1, (visible.Bottom + tileSize - 1) / tileSize);
        
        // Ensure startTileY is valid
        startTileY = Math.Max(0, Math.Min(startTileY, _world.Height - 1));
        
        if (startTileY > endTileY || startTileX > endTileX)
            return;
        
        // Define 4 layers with different parallax speeds and opacities
        var layers = new[]
        {
            (parallaxSpeed: 0.05f, opacity: 0.5f, darkness: 0.7f, tileStep: 1),  // Layer 1: Nearest, most visible
            (parallaxSpeed: 0.03f, opacity: 0.3f, darkness: 0.8f, tileStep: 2),  // Layer 2: Medium
            (parallaxSpeed: 0.015f, opacity: 0.15f, darkness: 0.85f, tileStep: 3), // Layer 3: Far
            (parallaxSpeed: 0.01f, opacity: 0.08f, darkness: 0.9f, tileStep: 4)   // Layer 4: Farthest, least visible
        };
        
        // Draw each layer
        for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            var layer = layers[layerIndex];
            float parallaxOffsetX = -_camera.Position.X * layer.parallaxSpeed;
            
            // Draw tiles for this layer
            for (int tileY = startTileY; tileY <= endTileY; tileY += layer.tileStep)
            {
                int depthBelowSurface = tileY - Config.SurfaceLevel;
                if (depthBelowSurface < 0) continue; // Only draw underground
                
                float localDepthFactor = MathHelper.Clamp(depthBelowSurface / 150f, 0f, 1f);
                
                // Determine tile type based on depth (dirt near surface, stone deeper, bedrock very deep)
                TileType bgTileType;
                if (localDepthFactor < 0.2f)
                    bgTileType = TileType.Dirt;
                else if (localDepthFactor < 0.8f)
                    bgTileType = TileType.Stone;
                else
                    bgTileType = TileType.Bedrock;
                
                // Calculate opacity and darkness for this layer and depth
                float layerOpacity = layer.opacity * (1f - localDepthFactor * 0.3f); // Slightly fade with depth
                float layerDarkness = layer.darkness + localDepthFactor * 0.1f; // Darker with depth
                layerDarkness = MathHelper.Clamp(layerDarkness, 0f, 1f);
                
                for (int tileX = startTileX; tileX <= endTileX; tileX += layer.tileStep)
                {
                    // ALWAYS draw tiles (even if foreground tiles exist) - this creates the full cave wall
                    // Calculate world position with parallax
                    int worldX = (int)(tileX * tileSize + parallaxOffsetX);
                    int worldY = tileY * tileSize;
                    
                    // Only draw if in visible area
                    if (worldX + tileSize < visible.Left || worldX > visible.Right ||
                        worldY + tileSize < visible.Top || worldY > visible.Bottom)
                        continue;
                    
                    try
                    {
                        var sourceRect = TextureManager.GetTileRect(bgTileType);
                        var destRect = new Rectangle(worldX, worldY, tileSize, tileSize);
                        
                        // Apply layer opacity, darkness, and depth for full cave wall effect
                        Color tileColor = Color.White * layerOpacity * layerDarkness;
                        
                        _spriteBatch.Draw(TextureManager.TileAtlas, destRect, sourceRect, tileColor);
                    }
                    catch
                    {
                        // Skip this tile if there's an error
                        continue;
                    }
                }
            }
        }
    }
    
    private void DrawUndergroundBackground(Rectangle visible, float depthFactor)
    {
        // Legacy method - now replaced by DrawFullCaveBackground
        // Keep for compatibility but redirect
        int startTileY = visible.Top / GameConfig.TileSize;
        DrawFullCaveBackground(visible, depthFactor, startTileY);
    }
    
    private void DrawCelestialBodies()
    {
        var visible = _camera.VisibleArea;
        
        // Calculate sun/moon position based on time of day
        // Sun rises at 0.25 (6 AM), sets at 0.75 (6 PM)
        // Moon is opposite (rises at 0.75, sets at 0.25)
        
        float skyWidth = visible.Width;
        float skyHeight = visible.Height * 0.3f; // Top 30% of screen
        
        // Sun position (0.25 to 0.75 = day)
        if (_dayTime >= 0.25f && _dayTime <= 0.75f)
        {
            float sunProgress = (_dayTime - 0.25f) / 0.5f; // 0 to 1 during day
            float sunX = visible.Left + sunProgress * skyWidth;
            float sunY = visible.Top + skyHeight * 0.3f + MathF.Sin(sunProgress * MathF.PI) * skyHeight * 0.4f;
            
            // Draw sun
            int sunSize = 40;
            var sunColor = GetSunColor();
            var sunRect = new Rectangle((int)(sunX - sunSize / 2), (int)(sunY - sunSize / 2), sunSize, sunSize);
            _spriteBatch.Draw(TextureManager.Pixel, sunRect, sunColor);
            
            // Draw sun glow
            int glowSize = sunSize + 20;
            var glowColor = sunColor * 0.3f;
            var glowRect = new Rectangle((int)(sunX - glowSize / 2), (int)(sunY - glowSize / 2), glowSize, glowSize);
            _spriteBatch.Draw(TextureManager.Pixel, glowRect, glowColor);
        }
        
        // Moon position (night: 0.75 to 1.0 and 0.0 to 0.25)
        if (_dayTime < 0.25f || _dayTime > 0.75f)
        {
            float moonProgress;
            if (_dayTime < 0.25f)
                moonProgress = (_dayTime + 0.25f) / 0.5f; // 0.0 to 0.25 -> 0.0 to 1.0
            else
                moonProgress = (_dayTime - 0.75f) / 0.25f; // 0.75 to 1.0 -> 0.0 to 1.0
            
            float moonX = visible.Left + moonProgress * skyWidth;
            float moonY = visible.Top + skyHeight * 0.3f + MathF.Sin(moonProgress * MathF.PI) * skyHeight * 0.4f;
            
            // Draw moon
            int moonSize = 35;
            var moonColor = new Color(220, 220, 240, 200);
            var moonRect = new Rectangle((int)(moonX - moonSize / 2), (int)(moonY - moonSize / 2), moonSize, moonSize);
            _spriteBatch.Draw(TextureManager.Pixel, moonRect, moonColor);
            
            // Draw stars (only at night)
            if (_dayTime < 0.2f || _dayTime > 0.8f)
            {
                DrawStars(visible, skyWidth, skyHeight);
            }
        }
    }
    
    private void DrawStars(Rectangle visible, float skyWidth, float skyHeight)
    {
        // Draw random stars (using seeded random based on position for consistency)
        int starCount = 30;
        for (int i = 0; i < starCount; i++)
        {
            // Use position-based seed for consistent star positions
            int seed = (int)(visible.Left / 100) + i * 1000;
            var rng = new Random(seed);
            
            float starX = visible.Left + rng.NextSingle() * skyWidth;
            float starY = visible.Top + rng.NextSingle() * skyHeight * 0.6f;
            
            // Twinkle effect based on time
            float twinkle = (MathF.Sin(_dayTime * 20f + i) + 1f) * 0.5f;
            float starAlpha = (0.3f + twinkle * 0.7f) * (1f - GetTimeBrightness());
            
            if (starAlpha > 0.1f)
            {
                int starSize = rng.Next(1, 3);
                byte alpha = (byte)(255 * starAlpha);
                var starColor = new Color((byte)255, (byte)255, (byte)255, alpha);
                var starRect = new Rectangle((int)starX, (int)starY, starSize, starSize);
                _spriteBatch.Draw(TextureManager.Pixel, starRect, starColor);
            }
        }
    }
    
    private Color GetSunColor()
    {
        // Sun color changes throughout the day
        if (_dayTime >= 0.25f && _dayTime <= 0.75f)
        {
            float t = (_dayTime - 0.25f) / 0.5f;
            if (t < 0.2f) // Sunrise
                return Color.Lerp(new Color(255, 150, 100), new Color(255, 220, 150), t / 0.2f);
            else if (t > 0.8f) // Sunset
                return Color.Lerp(new Color(255, 220, 150), new Color(255, 100, 50), (t - 0.8f) / 0.2f);
            else // Midday
                return new Color(255, 255, 200);
        }
        return new Color(255, 255, 200);
    }
    
    private float GetTimeBrightness()
    {
        // Brightness factor: 1.0 at noon, 0.3 at midnight
        if (_dayTime >= 0.25f && _dayTime <= 0.75f)
        {
            // Daytime: bright
            float t = (_dayTime - 0.25f) / 0.5f;
            return 0.7f + 0.3f * MathF.Sin(t * MathF.PI);
        }
        else
        {
            // Nighttime: dark
            return 0.3f;
        }
    }

    private Vector2 ScreenToRenderTarget(Vector2 screenPos)
    {
        // Calculate scaling (same as in DrawScaledGame)
        float scaleX = (float)GraphicsDevice.Viewport.Width / _gameRenderTarget.Width;
        float scaleY = (float)GraphicsDevice.Viewport.Height / _gameRenderTarget.Height;
        float scale = Math.Min(scaleX, scaleY);
        
        int scaledWidth = (int)(_gameRenderTarget.Width * scale);
        int scaledHeight = (int)(_gameRenderTarget.Height * scale);
        int offsetX = (GraphicsDevice.Viewport.Width - scaledWidth) / 2;
        int offsetY = (GraphicsDevice.Viewport.Height - scaledHeight) / 2;
        
        // Convert screen coordinates to render target coordinates
        float renderTargetX = (screenPos.X - offsetX) / scale;
        float renderTargetY = (screenPos.Y - offsetY) / scale;
        
        // Clamp to render target bounds
        renderTargetX = MathHelper.Clamp(renderTargetX, 0, _gameRenderTarget.Width);
        renderTargetY = MathHelper.Clamp(renderTargetY, 0, _gameRenderTarget.Height);
        
        return new Vector2(renderTargetX, renderTargetY);
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
    
    private void DrawScaledGamePostProcessed()
    {
        // Calculate scaling to fit window while maintaining aspect ratio
        float scaleX = (float)GraphicsDevice.Viewport.Width / _postProcessRenderTarget.Width;
        float scaleY = (float)GraphicsDevice.Viewport.Height / _postProcessRenderTarget.Height;
        float scale = Math.Min(scaleX, scaleY);
        
        int scaledWidth = (int)(_postProcessRenderTarget.Width * scale);
        int scaledHeight = (int)(_postProcessRenderTarget.Height * scale);
        int x = (GraphicsDevice.Viewport.Width - scaledWidth) / 2;
        int y = (GraphicsDevice.Viewport.Height - scaledHeight) / 2;
        
        _spriteBatch.Draw(
            _postProcessRenderTarget,
            new Rectangle(x, y, scaledWidth, scaledHeight),
            Color.White
        );
    }
    
    private Color GetColorGrading(float dayTime)
    {
        // Color grading based on time of day
        // dayTime: 0.0 = midnight, 0.25 = 6 AM, 0.5 = noon, 0.75 = 6 PM, 1.0 = midnight
        
        // Normalize dayTime to 0-1 cycle
        float normalizedTime = dayTime % 1f;
        
        // Calculate brightness and color temperature
        float brightness = 1f;
        float saturation = 1f;
        Color colorTint = Color.White;
        
        if (normalizedTime < 0.25f) // Night (midnight to 6 AM)
        {
            float nightFactor = normalizedTime / 0.25f; // 0 at midnight, 1 at 6 AM
            brightness = 0.4f + nightFactor * 0.3f; // 0.4 to 0.7
            saturation = 0.7f + nightFactor * 0.2f; // 0.7 to 0.9
            colorTint = Color.Lerp(
                new Color(100, 120, 180), // Cool blue at midnight
                new Color(150, 160, 200), // Warmer blue at dawn
                nightFactor
            );
        }
        else if (normalizedTime < 0.5f) // Morning (6 AM to noon)
        {
            float morningFactor = (normalizedTime - 0.25f) / 0.25f; // 0 at 6 AM, 1 at noon
            brightness = 0.7f + morningFactor * 0.3f; // 0.7 to 1.0
            saturation = 0.9f + morningFactor * 0.1f; // 0.9 to 1.0
            colorTint = Color.Lerp(
                new Color(200, 210, 240), // Warm blue at dawn
                new Color(255, 255, 255), // Pure white at noon
                morningFactor
            );
        }
        else if (normalizedTime < 0.75f) // Afternoon (noon to 6 PM)
        {
            float afternoonFactor = (normalizedTime - 0.5f) / 0.25f; // 0 at noon, 1 at 6 PM
            brightness = 1.0f - afternoonFactor * 0.2f; // 1.0 to 0.8
            saturation = 1.0f - afternoonFactor * 0.1f; // 1.0 to 0.9
            colorTint = Color.Lerp(
                new Color(255, 255, 255), // Pure white at noon
                new Color(255, 240, 220), // Warm orange at sunset
                afternoonFactor
            );
        }
        else // Evening/Night (6 PM to midnight)
        {
            float eveningFactor = (normalizedTime - 0.75f) / 0.25f; // 0 at 6 PM, 1 at midnight
            brightness = 0.8f - eveningFactor * 0.4f; // 0.8 to 0.4
            saturation = 0.9f - eveningFactor * 0.2f; // 0.9 to 0.7
            colorTint = Color.Lerp(
                new Color(255, 200, 150), // Warm orange-red at sunset
                new Color(100, 120, 180), // Cool blue at midnight
                eveningFactor
            );
        }
        
        // Apply brightness and saturation
        float r = MathHelper.Clamp(colorTint.R * brightness, 0, 255);
        float g = MathHelper.Clamp(colorTint.G * brightness, 0, 255);
        float b = MathHelper.Clamp(colorTint.B * brightness, 0, 255);
        
        // Apply saturation
        float gray = (r + g + b) / 3f;
        r = MathHelper.Lerp(gray, r, saturation);
        g = MathHelper.Lerp(gray, g, saturation);
        b = MathHelper.Lerp(gray, b, saturation);
        
        return new Color((int)r, (int)g, (int)b, 255);
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
        
        // Create post-processing render target
        _postProcessRenderTarget?.Dispose();
        _postProcessRenderTarget = new RenderTarget2D(
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
