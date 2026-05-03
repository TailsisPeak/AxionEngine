using System;

namespace Axion.Core;

/// <summary>Base class for anything that can be attached to a GameObject.</summary>
public abstract class Component
{
    /// <summary>Owning GameObject. Set by GameObject.AddComponent — never assign manually.</summary>
    public GameObject GameObject { get; internal set; } = null!;
    public Transform  Transform  => GameObject.Transform;
    public Scene      Scene      => GameObject.Scene;
    public string     Name       => GameObject.Name;

    public bool Enabled { get; set; } = true;

    /// <summary>True if this component is enabled AND its GameObject is active in the scene hierarchy.</summary>
    public bool ActiveInHierarchy => Enabled && GameObject.ActiveInHierarchy;

    public virtual void OnAttach() { }
    public virtual void OnDetach() { }

    public T? GetComponent<T>() where T : Component => GameObject.GetComponent<T>();
    public T   AddComponent<T>() where T : Component, new() => GameObject.AddComponent<T>();
}
