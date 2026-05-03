using System;
using System.IO;
using Axion.Core;

namespace Axion.Scripting;

/// <summary>
/// A <see cref="Behavior"/> that executes a Gel (or Silica) script directly
/// using <see cref="GelInterpreter"/> — no C# transpile, no Roslyn. The script's
/// top-level statements run in <see cref="Awake"/>, then any of these named
/// functions, if present, are invoked on the matching engine event:
///
/// <list type="bullet">
///   <item><c>start</c> / <c>Start</c>           → <see cref="Start"/></item>
///   <item><c>loop</c> / <c>update</c> / <c>Update</c> → <see cref="Update"/></item>
///   <item><c>fixed</c> / <c>fixedUpdate</c>     → <see cref="FixedUpdate"/></item>
///   <item><c>late</c> / <c>lateUpdate</c>       → <see cref="LateUpdate"/></item>
///   <item><c>onDestroy</c> / <c>destroyed</c>   → <see cref="OnDestroy"/></item>
/// </list>
///
/// Errors raised inside Gel surface as <see cref="GelRuntimeException"/> with
/// the original <c>.gel</c> source line — no downstream C# compiler errors.
/// </summary>
public sealed class GelBehavior : Behavior
{
    public string ScriptPath { get; private set; } = "";
    public AxionLanguage Language { get; private set; } = AxionLanguage.Gel;

    private GelInterpreter? _interp;
    private GelFuncValue? _start, _update, _fixed, _late, _destroy;

    public void LoadFromFile(string path)
    {
        ScriptPath = path;
        Language   = LanguageConverter.DetectFromExtension(path);
        var src    = File.ReadAllText(path);
        Load(src);
    }

    public void Load(string source)
    {
        try
        {
            var ast = LanguageConverter.Parse(source, Language);
            _interp = new GelInterpreter(Console.Out);
            EngineGelStdlib.Register(_interp.Global, Console.Out, this);
            _interp.Run(ast); // executes top-level + hoists FuncDecls

            _start   = _interp.FindFunc("start", "Start");
            _update  = _interp.FindFunc("loop", "update", "Update");
            _fixed   = _interp.FindFunc("fixed", "fixedUpdate", "FixedUpdate");
            _late    = _interp.FindFunc("late", "lateUpdate", "LateUpdate");
            _destroy = _interp.FindFunc("onDestroy", "destroyed", "OnDestroy");

            Log.Info($"loaded Gel script '{Path.GetFileName(ScriptPath)}' on '{GameObject?.Name}'", "GelBehavior");
        }
        catch (GelRuntimeException gx)
        {
            Log.Error($"{Path.GetFileName(ScriptPath)}({gx.Line}): {gx.Message}", "GelBehavior");
            _interp = null;
        }
        catch (Exception ex)
        {
            Log.Exception(ex, $"GelBehavior:{Path.GetFileName(ScriptPath)}");
            _interp = null;
        }
    }

    public override void Start()       => Invoke(_start,   nameof(Start));
    public override void Update()      => Invoke(_update,  nameof(Update));
    public override void FixedUpdate() => Invoke(_fixed,   nameof(FixedUpdate));
    public override void LateUpdate()  => Invoke(_late,    nameof(LateUpdate));
    public override void OnDestroy()   => Invoke(_destroy, nameof(OnDestroy));

    private void Invoke(GelFuncValue? fn, string label)
    {
        if (fn == null || _interp == null) return;
        try { _interp.CallFunction(fn, Array.Empty<object?>()); }
        catch (GelRuntimeException gx)
        {
            Log.Error($"{Path.GetFileName(ScriptPath)}({gx.Line}) in {fn.Name}: {gx.Message}", "GelBehavior");
        }
        catch (Exception ex)
        {
            Log.Exception(ex, $"GelBehavior:{Path.GetFileName(ScriptPath)}.{label}");
        }
    }
}
