using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Axion.Core;

namespace Axion.Scripting;

/// <summary>
/// Compiles Axion scripts (Gel/Silica/Blocks) to C# in-memory, then to a .NET assembly via Roslyn,
/// and exposes the generated Behavior types so the engine can attach them to GameObjects.
/// </summary>
public sealed class ScriptHost
{
    private readonly Dictionary<string, Type> _typeCache = new();
    private readonly List<MetadataReference> _references;

    public ScriptHost()
    {
        // Reference every assembly currently loaded in the AppDomain so user scripts can use Axion types.
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var refs = new List<MetadataReference>();
        foreach (var p in trusted)
            try { refs.Add(MetadataReference.CreateFromFile(p)); } catch { /* skip locked files */ }

        // Make sure Axion.Core is referenced even when not in TPA list.
        TryAdd(refs, typeof(Behavior).Assembly.Location);
        TryAdd(refs, typeof(object).Assembly.Location);
        _references = refs;
    }

    private static void TryAdd(List<MetadataReference> refs, string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        if (refs.Any(r => r.Display == path)) return;
        refs.Add(MetadataReference.CreateFromFile(path));
    }

    public Type? Compile(string source, AxionLanguage lang, string scriptName)
    {
        if (_typeCache.TryGetValue(scriptName, out var cached)) return cached;

        string csharp;
        try
        {
            var ast = LanguageConverter.Parse(source, lang);
            csharp = CSharpEmitter.Emit(ast, new() { ClassName = scriptName, AsBehavior = true });
        }
        catch (Exception ex) { Log.Exception(ex, "ScriptHost.Parse"); return null; }

        var tree   = CSharpSyntaxTree.ParseText(csharp);
        var asmName= "AxionScript_" + scriptName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var compilation = CSharpCompilation.Create(asmName,
            new[] { tree },
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var ms = new MemoryStream();
        EmitResult result = compilation.Emit(ms);
        if (!result.Success)
        {
            foreach (var d in result.Diagnostics)
                if (d.Severity == DiagnosticSeverity.Error)
                    Log.Error($"  CS{d.Id} {d.GetMessage()} @ {d.Location.GetLineSpan().StartLinePosition}", "ScriptHost");
            Log.Trace("---- generated C# ----\n" + csharp, "ScriptHost");
            return null;
        }
        ms.Position = 0;
        var asm = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(ms);
        var type = asm.GetTypes().FirstOrDefault(t => typeof(Behavior).IsAssignableFrom(t));
        if (type == null) { Log.Error($"No Behavior subclass produced by script {scriptName}", "ScriptHost"); return null; }
        _typeCache[scriptName] = type;
        Log.Info($"compiled {lang} script '{scriptName}' → {type.FullName}", "ScriptHost");
        return type;
    }

    public Type? CompileFile(string path)
    {
        var src = File.ReadAllText(path);
        var lang = LanguageConverter.DetectFromExtension(path);
        var name = Path.GetFileNameWithoutExtension(path).Replace('-', '_').Replace(' ', '_');
        if (name.Length == 0 || char.IsDigit(name[0])) name = "Script_" + name;
        return Compile(src, lang, char.ToUpper(name[0]) + name[1..]);
    }

    /// <summary>
    /// Attach a script to a GameObject. Gel and Silica scripts are run directly
    /// by <see cref="GelInterpreter"/> (no Roslyn step) so errors stay in the
    /// source language. Other languages (C#, Blocks) still go through the
    /// Roslyn transpile path below.
    /// </summary>
    public Behavior? Attach(GameObject go, string sourcePath)
    {
        var lang = LanguageConverter.DetectFromExtension(sourcePath);
        if (lang == AxionLanguage.Gel || lang == AxionLanguage.Silica)
        {
            var gb = go.AddComponent<GelBehavior>();
            gb.LoadFromFile(sourcePath);
            return gb;
        }

        var type = CompileFile(sourcePath);
        if (type == null) return null;
        var inst = (Behavior)go.AddComponent(type);
        return inst;
    }

    public void ClearCache() => _typeCache.Clear();
}
