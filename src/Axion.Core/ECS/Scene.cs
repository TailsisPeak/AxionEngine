using System;
using System.Collections.Generic;

namespace Axion.Core;

/// <summary>A scene is a flat list of root GameObjects + indexed component lookups.</summary>
public sealed class Scene
{
    public string Name { get; set; }
    public List<GameObject> Roots { get; } = new();

    /// <summary>Indexes components by exact runtime type for fast lookup by systems.</summary>
    private readonly Dictionary<Type, List<Component>> _byType = new();

    public Scene(string name = "Untitled") => Name = name;

    public GameObject CreateGameObject(string name = "GameObject")
    {
        var go = new GameObject(name) { Scene = this };
        Roots.Add(go);
        return go;
    }

    public void Add(GameObject go)
    {
        if (go.Scene == this) return;
        go.Scene = this;
        Roots.Add(go);
        foreach (var c in go.Components) NotifyComponentAdded(c);
    }

    public void Remove(GameObject go)
    {
        Roots.Remove(go);
        foreach (var c in go.Components) NotifyComponentRemoved(c);
    }

    internal void NotifyComponentAdded(Component c)
    {
        var t = c.GetType();
        if (!_byType.TryGetValue(t, out var list)) { list = new(); _byType[t] = list; }
        list.Add(c);
    }

    internal void NotifyComponentRemoved(Component c)
    {
        if (_byType.TryGetValue(c.GetType(), out var list)) list.Remove(c);
    }

    public IEnumerable<T> AllOfType<T>() where T : Component
    {
        foreach (var (t, list) in _byType)
            if (typeof(T).IsAssignableFrom(t))
                foreach (var c in list) if (c is T ct) yield return ct;
    }

    public GameObject? Find(string name)
    {
        foreach (var go in EnumerateAll()) if (go.Name == name) return go;
        return null;
    }

    public IEnumerable<GameObject> FindByTag(string tag)
    {
        foreach (var go in EnumerateAll()) if (go.Tag == tag) yield return go;
    }

    public IEnumerable<GameObject> EnumerateAll()
    {
        foreach (var root in Roots)
            foreach (var go in EnumerateRecursive(root))
                yield return go;
    }

    private static IEnumerable<GameObject> EnumerateRecursive(GameObject go)
    {
        yield return go;
        foreach (var ch in go.Transform.Children)
            foreach (var d in EnumerateRecursive(ch.GameObject))
                yield return d;
    }
}
