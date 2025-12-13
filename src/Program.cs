using System;

namespace TerraNova;

public static class Program
{
    [STAThread]
    static void Main()
    {
        using var game = new TerraNovaGame();
        game.Run();
    }
}
