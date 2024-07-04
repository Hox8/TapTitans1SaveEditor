using System;

namespace TapTitansSaveEditor;

public static class Globals
{
    /// <summary>
    /// Friendly string representing the app's name and version.
    /// </summary>
    /// <remarks>
    /// Don't forget to change this in `app.manifest`!
    /// </remarks>
    public const string TitleString = "TapTitans Save Editor";

    /// <summary>
    /// Sequence of characters used for separating relevant sections of console output.
    /// </summary>
    public const string Separator = "=============================================";  // 45 long

    /// <summary>
    /// Prints the passed string to the console using the desired color.
    /// </summary>
    /// <remarks>
    /// - Uses Console.Write()<br/>
    /// - Reverts Console color to previous value on finish
    /// </remarks>
    public static void PrintColor(string content, ConsoleColor color)
    {
        var old = Console.ForegroundColor;

        Console.ForegroundColor = color;
        Console.Write(content);

        Console.ForegroundColor = old;
    }

    /// <summary>
    /// Prints information about this application to the console.
    /// </summary>
    public static void PrintInfo()
    {
        Console.WriteLine(Separator);
        PrintColor($"{TitleString}\n", ConsoleColor.Green);
        Console.WriteLine($"Copyright © 2024 Hox, GPL v3.0");
        Console.WriteLine($"{Separator}\n");
    }

    /// <summary>
    /// Clears the console.
    /// </summary>
    public static void ClearConsole()
    {
        Console.Clear();
#if UNIX
        Console.Write("\x1b[3J");
        Console.SetCursorPosition(0, 0);
#endif
    }

    /// <summary>
    /// Prints the 'Press any key...' dialog and awaits a key press.
    /// </summary>
    /// <remarks>
    /// This is not executed on Unix systems as the Terminal behaves differently.
    /// </remarks>
    public static void PrintExitString()
    {
#if !UNIX
        Console.WriteLine($"\nPress any key to close...");
#if !DEBUG
        Console.ReadKey();
#endif
#endif
    }
}
