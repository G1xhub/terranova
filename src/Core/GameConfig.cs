using System.Text.Json;

namespace TerraNova.Core;

/// <summary>
/// Game configuration - loaded from JSON, provides default values
/// </summary>
public class GameConfig
{
    // Display
    public int ScreenWidth { get; set; } = 1920;
    public int ScreenHeight { get; set; } = 1080;
    public int GameWidth { get; set; } = 960;  // Internal resolution
    public int GameHeight { get; set; } = 540;
    public bool Fullscreen { get; set; } = false;
    public bool VSync { get; set; } = true;
    
    // World
    public int WorldWidth { get; set; } = 4200;   // Tiles (small = 4200, medium = 6400, large = 8400)
    public int WorldHeight { get; set; } = 1200;  // Tiles
    public int ChunkSize { get; set; } = 32;      // Tiles per chunk
    
    // Player
    public float PlayerSpeed { get; set; } = 4f;
    public float JumpForce { get; set; } = 11f;
    public float Gravity { get; set; } = 0.4f;
    public float MaxFallSpeed { get; set; } = 15f;
    public int PlayerReach { get; set; } = 5;     // Tiles
    public int MaxHealth { get; set; } = 100;
    public int MaxMana { get; set; } = 20;
    
    // World Generation
    public int SurfaceLevel { get; set; } = 350;
    public int UndergroundLevel { get; set; } = 500;
    public int CavernLevel { get; set; } = 800;
    public int UnderworldLevel { get; set; } = 1100;
    
    // Lighting
    public int LightRadius { get; set; } = 12;
    public float AmbientLight { get; set; } = 0.03f;
    
    // Audio
    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 0.7f;
    public float SFXVolume { get; set; } = 1.0f;
    
    // Constants (not configurable)
    public const int TileSize = 16;
    public const int PlayerWidth = 20;
    public const int PlayerHeight = 42;
    
    private static readonly string ConfigPath = "config.json";
    
    public static GameConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<GameConfig>(json) ?? new GameConfig();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load config: {ex.Message}");
        }
        
        var config = new GameConfig();
        config.Save();
        return config;
    }
    
    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}
