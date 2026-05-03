using System;
using System.Collections.Generic;
using System.IO;

namespace Axion.Core;

/// <summary>
/// Resolves asset paths and caches loaded objects by GUID/path. Unity's Resources/AssetDatabase analog.
/// Renderer/audio subsystems register loaders here.
/// </summary>
public static class AssetDatabase
{
    public static string RootPath { get; set; } = "Assets";

    private static readonly Dictionary<string, object> _cache = new();
    private static readonly Dictionary<Type, Func<string, object?>> _loaders = new();

    public static void RegisterLoader<T>(Func<string, T?> loader) where T : class
        => _loaders[typeof(T)] = path => loader(path);

    public static T? Load<T>(string relativePath) where T : class
    {
        var key = typeof(T).Name + "::" + relativePath;
        if (_cache.TryGetValue(key, out var cached) && cached is T tc) return tc;
        if (!_loaders.TryGetValue(typeof(T), out var loader)) { Log.Warn($"No loader registered for {typeof(T).Name}"); return null; }
        var fullPath = Path.Combine(RootPath, relativePath);
        if (!File.Exists(fullPath)) { Log.Warn($"Asset not found: {fullPath}"); return null; }
        try
        {
            var obj = loader(fullPath);
            if (obj is T t) { _cache[key] = t; return t; }
            return null;
        }
        catch (Exception ex) { Log.Exception(ex, "AssetDatabase"); return null; }
    }

    public static void Unload(string relativePath)
    {
        var keysToRemove = new List<string>();
        foreach (var k in _cache.Keys) if (k.EndsWith("::" + relativePath)) keysToRemove.Add(k);
        foreach (var k in keysToRemove) _cache.Remove(k);
    }

    public static void Clear() => _cache.Clear();
}
