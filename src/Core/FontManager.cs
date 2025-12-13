using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TerraNova.Core;

/// <summary>
/// Manages fonts using FontStashSharp - no content pipeline needed
/// </summary>
public static class FontManager
{
    private static FontSystem? _fontSystem;
    private static GraphicsDevice? _graphicsDevice;
    
    public static DynamicSpriteFont DebugFont { get; private set; } = null!;
    public static DynamicSpriteFont SmallFont { get; private set; } = null!;
    public static DynamicSpriteFont LargeFont { get; private set; } = null!;
    
    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        
        var settings = new FontSystemSettings
        {
            FontResolutionFactor = 2,
            KernelWidth = 2,
            KernelHeight = 2
        };
        
        _fontSystem = new FontSystem(settings);
        
        // Try to load a system font
        string? fontPath = FindSystemFont();
        
        if (fontPath != null && File.Exists(fontPath))
        {
            try
            {
                _fontSystem.AddFont(File.ReadAllBytes(fontPath));
                
                // Create different sizes
                SmallFont = _fontSystem.GetFont(12);
                DebugFont = _fontSystem.GetFont(14);
                LargeFont = _fontSystem.GetFont(24);
                
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load font: {ex.Message}");
            }
        }
        
        // Fallback: Use default font system (will show empty if no fonts)
        Console.WriteLine("Warning: No system fonts found. Text will be limited.");
        
        // Try to load any available font
        var fontPaths = GetAllPossibleFontPaths();
        foreach (var path in fontPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    _fontSystem.AddFont(File.ReadAllBytes(path));
                    SmallFont = _fontSystem.GetFont(12);
                    DebugFont = _fontSystem.GetFont(14);
                    LargeFont = _fontSystem.GetFont(24);
                    Console.WriteLine($"Loaded font from: {path}");
                    return;
                }
                catch { }
            }
        }
        
        // Last resort - create minimal fonts that just won't crash
        SmallFont = _fontSystem.GetFont(12);
        DebugFont = _fontSystem.GetFont(14);
        LargeFont = _fontSystem.GetFont(24);
    }
    
    private static string? FindSystemFont()
    {
        // Common system font paths - prioritize monospace/simple fonts
        var possiblePaths = new[]
        {
            // Windows
            @"C:\Windows\Fonts\consola.ttf",
            @"C:\Windows\Fonts\arial.ttf",
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\tahoma.ttf",
            @"C:\Windows\Fonts\verdana.ttf",
            // Linux
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/ubuntu/Ubuntu-R.ttf",
            // macOS
            "/System/Library/Fonts/SFNSMono.ttf",
            "/System/Library/Fonts/Menlo.ttc",
            "/Library/Fonts/Arial.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf",
        };
        
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }
        
        return null;
    }
    
    private static IEnumerable<string> GetAllPossibleFontPaths()
    {
        var paths = new List<string>();
        
        // Windows font directory
        if (OperatingSystem.IsWindows())
        {
            var winFonts = @"C:\Windows\Fonts";
            if (Directory.Exists(winFonts))
            {
                try
                {
                    paths.AddRange(Directory.GetFiles(winFonts, "*.ttf"));
                }
                catch { }
            }
        }
        
        // Linux font directories
        if (OperatingSystem.IsLinux())
        {
            var linuxFontDirs = new[]
            {
                "/usr/share/fonts",
                "/usr/local/share/fonts",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts")
            };
            
            foreach (var dir in linuxFontDirs)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        paths.AddRange(Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories));
                    }
                    catch { }
                }
            }
        }
        
        // macOS font directories
        if (OperatingSystem.IsMacOS())
        {
            var macFontDirs = new[]
            {
                "/System/Library/Fonts",
                "/Library/Fonts",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Fonts")
            };
            
            foreach (var dir in macFontDirs)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        paths.AddRange(Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories));
                    }
                    catch { }
                }
            }
        }
        
        return paths;
    }
    
    public static void Dispose()
    {
        _fontSystem?.Dispose();
        _fontSystem = null;
    }
}
