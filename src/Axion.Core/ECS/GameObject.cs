using System;
using System.Collections.Generic;

namespace Axion.Core;

/// <summary>Unity-equivalent of GameObject. A named container for Components.</summary>
public sealed class GameObject
{
    private static int _nextId;
    public int    Id    { get; }
    public string Name  { get; set; }
    public string Tag   { get; set; } = "Untagged";
    public int    Layer { get; set; } = 0;

    private bool _active = true;
    public bool Active
    {
        get => _active;
        set { if (_active == value) return; _active = value; }
    }

    public bool ActiveInHierarchy
    {
        get
        {
            if (!_active) return false;
            var t = Transform.Parent;
            while (t != null) { if (!t.GameObject._active) return false; t = t.Parent; }
            return true;
        }
    }

    public Scene Scene { get; internal set; } = null!;
    public Transform Transform { get; }

    private readonly List<Component> _components = new();
    public IReadOnlyList<Component> Components => _components;

    public GameObject(string name = "GameObject")
    {
        Id = System.Threading.Interlocked.Increment(ref _nextId);
        Name = name;
        Transform = new Transform { GameObject = this };
        _components.Add(Transform);
    }

    public T AddComponent<T>() where T : Component, new()
    {
        var c = new T { GameObject = this };
        _components.Add(c);
        c.OnAttach();
        Scene?.NotifyComponentAdded(c);
        return c;
    }

    public Component AddComponent(Type type)
    {
        if (!typeof(Component).IsAssignableFrom(type))
            throw new ArgumentException($"{type} is not a Component", nameof(type));
        var c = (Component)Activator.CreateInstance(type)!;
        c.GameObject = this;
        _components.Add(c);
        c.OnAttach();
        Scene?.NotifyComponentAdded(c);
        return c;
    }

    public T? GetComponent<T>() where T : Component
    {
        foreach (var c in _components) if (c is T t) return t;
        return null;
    }

    public IEnumerable<T> GetComponents<T>() where T : Component
    {
        foreach (var c in _components) if (c is T t) yield return t;
    }

    public T? GetComponentInChildren<T>() where T : Component
    {
        var r = GetComponent<T>(); if (r != null) return r;
        foreach (var child in Transform.Children) { r = child.GameObject.GetComponentInChildren<T>(); if (r != null) return r; }
        return null;
    }

    public void RemoveComponent(Component c)
    {
        if (c is Transform) throw new InvalidOperationException("Cannot remove Transform.");
        if (_components.Remove(c)) { Scene?.NotifyComponentRemoved(c); c.OnDetach(); }
    }

    public override string ToString() => $"GameObject({Name}#{Id})";
}
