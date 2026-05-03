using System;
using System.Collections.Generic;

namespace Axion.Core;

/// <summary>Type-safe global pub/sub bus. Use sparingly — direct references are usually clearer.</summary>
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public static void Subscribe<T>(Action<T> h)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) { list = new(); _handlers[typeof(T)] = list; }
        list.Add(h);
    }

    public static void Unsubscribe<T>(Action<T> h)
    {
        if (_handlers.TryGetValue(typeof(T), out var list)) list.Remove(h);
    }

    public static void Publish<T>(T evt)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) return;
        foreach (var h in list.ToArray())
            try { ((Action<T>)h).Invoke(evt); }
            catch (Exception ex) { Log.Exception(ex, "EventBus"); }
    }

    public static void Clear() => _handlers.Clear();
}
