using System;

namespace ModernTinyWall.TinyWall.DatabaseClasses;

internal static class CatalogueCommand
{
    internal static int? TryExecute(string[] arguments)
    {
        if (arguments.Length == 0 || !string.Equals(arguments[0], "/buildcatalogue", StringComparison.OrdinalIgnoreCase))
            return null;
        if (arguments.Length != 3)
        {
            Console.Error.WriteLine("Usage: TinyWall.exe /buildcatalogue <input JSON or SQLite> <output SQLite>");
            return 1;
        }
        try
        {
            SqliteApplicationCatalogue.Import(arguments[1]).Save(arguments[2]);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not build the application catalogue: {ex.Message}");
            return 1;
        }
    }
}
