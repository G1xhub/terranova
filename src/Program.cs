using System;
using System.IO;

namespace TerraNova;

public static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            using var game = new TerraNovaGame();
            game.Run();
        }
        catch (Exception ex)
        {
            // Log exception to file for debugging
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\nStack Trace:\n{ex.StackTrace}");
            }
            catch { }
            
            // Also show in console if available
            Console.WriteLine($"Fatal Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            throw;
        }
    }
}
