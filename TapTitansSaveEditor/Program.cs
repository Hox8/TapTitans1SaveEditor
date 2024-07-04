using System;
using System.IO;

namespace TapTitansSaveEditor;

internal static class Program
{
    const string InstructionsFile = "Drag a TapTitans 1 save file onto the executable to decompile it into a folder within the same directory.";
    const string InstructionsFolder = "Drag a TapTitans 1 save folder onto the executable to compile it back into a TapTitans save file.";
    const string InstructionsUnix = "Drag a TapTitans 1 save file or folder onto this window to begin:";
    const string ReadmeNotice = "See the included readme file for more information.";

    const string SavedOverwriteFile = "Saved TapTitans save file to:";
    const string SavedOverwriteFolder = "Saved TapTitans save folder to:";

    const string FailedOverwriteFile = "Failed to overwrite TapTitans save file";
    const string FailedOverwriteFolder = "Failed to overwrite TapTitans save folder";

    static void Main(string[] args)
    {
        Console.Title = Globals.TitleString;
        Console.OutputEncoding = System.Text.Encoding.Default;

#if UNIX
        // macOS prints terminal junk on start which we'll get rid of here
        Globals.ClearConsole();
#endif

        Globals.PrintInfo();

#if DEBUG
        args = [@"C:\Users\User 1\Documents\TapTitans\New2\Xhatt\912af0dff974604f1321254ca8ff38b6.adat"];
#elif UNIX  // macOS & Linux impl (drag and drop into terminal window)

        Console.WriteLine($" - {Locale.InstructionsFile}\n - {Locale.InstructionsFolder}\n - {Locale.ReadmeNotice}\n");
        Console.Write(Locale.InstructionsUnix + ' ');

        // Trim leading/trailing whitespace, quotation chars, and escaped whitespace
        args = [Console.ReadLine()?.Trim().Trim('\"').Replace("\\", "") ?? ""];
        Console.WriteLine();
#else   // Windows impl (drag and drop onto exe)
        if (args.Length != 1)
        {
            Console.WriteLine($" - {InstructionsFile}\n - {InstructionsFolder}\n - {ReadmeNotice}");
            Globals.PrintExitString();
            return;
        }
#endif

        bool inputIsDirectory;

        try
        {
            var fi = new FileInfo(args[0]);
            inputIsDirectory = (fi.Attributes & FileAttributes.Directory) != 0;
        }
        catch
        {
            // IOException (illegal path, e.g. C:\\C:\\Test)
            inputIsDirectory = false;
        }

        SaveFile saveFile;
        string outputPath = Path.ChangeExtension(args[0], inputIsDirectory ? SaveFile.Extension : null);

        // Load and save SaveFile

        if (inputIsDirectory)
        {
            saveFile = SaveFile.ReadFromFolder(args[0]);

            if (saveFile.Error == SaveFileError.None)
            {
                saveFile.WriteToFile(outputPath);
            }
        }
        else
        {
            saveFile = SaveFile.ReadFromFile(args[0]);

            if (saveFile.Error == SaveFileError.None)
            {
                saveFile.WriteToFolder(outputPath);
            }
        }

        // Print completion message

        if (saveFile.Error == SaveFileError.None)
        {
            // Success

            Console.WriteLine($" - {(inputIsDirectory ? SavedOverwriteFile : SavedOverwriteFolder)}");
            Globals.PrintColor($"   {outputPath}\n", ConsoleColor.Green);
        }
        else
        {
            // Fail

            if (saveFile.Error == SaveFileError.FailRead_PathNonExistant)
            {
                Globals.PrintColor($" - The input path '{args[0]}' does not exist.\n", ConsoleColor.Red);
            }
            else if (saveFile.Error == SaveFileError.FailWrite_FileContention)
            {
                Globals.PrintColor($" - {(inputIsDirectory ? FailedOverwriteFile : FailedOverwriteFolder)}:\n   {outputPath}\n", ConsoleColor.Red);
            }
            else
            {
                // Generic, non-specific error string
                Globals.PrintColor($" - {saveFile.GetErrorDescription()}\n", ConsoleColor.Red);
            }
        }

        Globals.PrintExitString();
    }
}
