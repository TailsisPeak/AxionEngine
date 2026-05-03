using System;
using System.IO;
using System.Text.Json.Nodes;
using Axion.Core;
using Axion.Scripting;

namespace Axion.Engine;

/// <summary>
/// Loads a Gel/Silica/Blocks script from disk, compiles it to a Behavior subclass via ScriptHost,
/// and attaches a fresh instance to this GameObject. Serializable across save/load.
///
/// <para>Loading is <b>lazy and idempotent</b>: <see cref="OnAttach"/> may run before either
/// <see cref="ScriptPath"/> has been deserialized or before <see cref="AxionApp.Instance"/>
/// exists (both happen during scene load).  Either case is fine — the engine calls
/// <see cref="EnsureLoaded"/> once everything is ready.</para>
///
/// <para>If <see cref="ScriptPath"/> is relative, it is resolved by walking up the directory
/// tree from <c>AppContext.BaseDirectory</c> until a matching file is found.  This means a
/// scene saved with <c>"samples/cube.gel"</c> still loads correctly when the editor runs from
/// <c>bin/Debug/net8.0/</c>.</para>
/// </summary>
public sealed class ScriptComponent : Component, ISerializableComponent
{
    public string ScriptPath { get; set; } = "";
    public Behavior? Instance { get; private set; }

    private bool _loadAttempted;

    public override void OnAttach() => EnsureLoaded();

    /// <summary>
    /// Compile + attach the script if it hasn't been attached yet.  Safe to call
    /// repeatedly; safe to call before <see cref="ScriptPath"/> is set or before
    /// <see cref="AxionApp.Instance"/> exists (does nothing in those cases).
    /// </summary>
    public void EnsureLoaded()
    {
        if (Instance != null) return;
        if (string.IsNullOrEmpty(ScriptPath)) return;
        if (AxionApp.Instance == null) return;
        if (_loadAttempted) return;
        _loadAttempted = true;

        var resolved = ResolveScriptPath(ScriptPath);
        if (resolved == null)
        {
            Log.Warn($"ScriptComponent: could not find '{ScriptPath}' on '{GameObject.Name}'", "ScriptComponent");
            return;
        }

        try
        {
            Instance = AxionApp.Instance.Scripts.Attach(GameObject, resolved);
            if (Instance == null)
                Log.Warn($"ScriptComponent: failed to compile '{resolved}' on '{GameObject.Name}'", "ScriptComponent");
        }
        catch (Exception ex) { Log.Exception(ex, "ScriptComponent"); }
    }

    /// <summary>
    /// Resolve a (possibly relative) script path to an absolute path that exists on disk.
    /// Tries: absolute as-given, then <c>AxionApp.ContentRoot</c>, then the current working
    /// directory, then walks up from <c>AppContext.BaseDirectory</c>.  Returns null if no
    /// candidate exists.
    /// </summary>
    private static string? ResolveScriptPath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path)) return path;

        // Explicit content root set by the host (e.g. the editor)
        if (!string.IsNullOrEmpty(AxionApp.ContentRoot))
        {
            var p = Path.GetFullPath(Path.Combine(AxionApp.ContentRoot, path));
            if (File.Exists(p)) return p;
        }

        // Current working directory
        var cwd = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        if (File.Exists(cwd)) return cwd;

        // Walk up from the executable directory looking for the path.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, path);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }

        return null;
    }

    public JsonObject? Serialize() => new() { ["script"] = ScriptPath };

    public void Deserialize(JsonObject d)
    {
        ScriptPath = d["script"]?.GetValue<string>() ?? "";
        // Path may now be set even though OnAttach already ran during AddComponent.
        // Reset the attempt latch so the next EnsureLoaded() actually tries.
        _loadAttempted = false;
    }
}
