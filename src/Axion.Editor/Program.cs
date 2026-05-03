using System;
using System.IO;
using Axion.Core;
using Axion.Engine;
using Axion.Scripting;

namespace Axion.Editor;

/// <summary>
/// Axion Editor — entry point.
///
/// Usage:
///   AxionEditor                       Open the default scene (samples/cube-scene.json if present).
///   AxionEditor &lt;scene.json&gt;     Load and run the given scene.
///   AxionEditor --convert FROM TO PATH    Convert source between languages.
///                                            FROM/TO ∈ { gel, silica, cs, blocks }
///   AxionEditor --version
/// </summary>
public static class Program
{
    public const string Version = "0.1.0";

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "--version" or "-v":
                        Console.WriteLine($"Axion Editor v{Version}");
                        return 0;
                    case "--help" or "-h":
                        PrintHelp(); return 0;
                    case "--convert":
                        if (args.Length < 4) { PrintHelp(); return 1; }
                        return RunConvert(args[1], args[2], args[3]);
                }
            }

            string? scenePath = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : DiscoverDefaultScene();
            EditorApp.Run(scenePath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Fatal: " + ex);
            return 2;
        }
    }

    private static string? DiscoverDefaultScene()
    {
        // Walk up from the EXE location AND the current working directory, looking for samples/.
        var roots = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exeDir = AppContext.BaseDirectory;
        var cwd    = Directory.GetCurrentDirectory();
        foreach (var start in new[] { exeDir, cwd })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "samples", "cube-scene.json");
                if (File.Exists(candidate)) return candidate;
                roots.Add(dir.FullName);
            }
        }
        Console.WriteLine("[Editor] No scene found. Searched roots:");
        foreach (var r in roots) Console.WriteLine("  - " + r);
        return null;
    }

    private static int RunConvert(string from, string to, string path)
    {
        var langFrom = ParseLang(from);
        var langTo   = ParseLang(to);
        if (langFrom == null || langTo == null) { Console.Error.WriteLine("Unknown language."); PrintHelp(); return 1; }
        var src = File.ReadAllText(path);
        var ast = LanguageConverter.Parse(src, langFrom.Value);
        var output = LanguageConverter.Emit(ast, langTo.Value);
        Console.Write(output);
        return 0;
    }

    private static AxionLanguage? ParseLang(string s) => s.ToLowerInvariant() switch
    {
        "gel"            => AxionLanguage.Gel,
        "silica" or "sil" => AxionLanguage.Silica,
        "blocks"         => AxionLanguage.Blocks,
        "cs" or "csharp" => AxionLanguage.CSharp,
        _ => null,
    };

    private static void PrintHelp()
    {
        Console.WriteLine($"Axion Editor v{Version}");
        Console.WriteLine();
        Console.WriteLine("USAGE");
        Console.WriteLine("  AxionEditor                          Open default scene.");
        Console.WriteLine("  AxionEditor <scene.json>             Load given scene.");
        Console.WriteLine("  AxionEditor --convert <from> <to> <path>");
        Console.WriteLine("                                       Convert source between languages.");
        Console.WriteLine("                                       <from>/<to> ∈ gel | silica | blocks | cs");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES");
        Console.WriteLine("  AxionEditor samples/cube-scene.json");
        Console.WriteLine("  AxionEditor --convert gel silica samples/cube.gel > cube.sil");
        Console.WriteLine("  AxionEditor --convert silica gel samples/cube.sil");
    }
}
