namespace Axion.Core;

/// <summary>
/// MonoBehaviour-style script base. Scripts written by the user (in C# or transpiled
/// from Gel/Silica/Blocks) inherit from this and override the lifecycle hooks.
/// </summary>
public abstract class Behavior : Component
{
    private bool _awoken;
    private bool _started;

    /// <summary>Called once when the behavior is first attached.</summary>
    public virtual void Awake() { }

    /// <summary>Called once on the first frame after Awake.</summary>
    public virtual void Start() { }

    /// <summary>Called every frame.</summary>
    public virtual void Update() { }

    /// <summary>Called every fixed-timestep tick (used by physics).</summary>
    public virtual void FixedUpdate() { }

    /// <summary>Called every frame after Update.</summary>
    public virtual void LateUpdate() { }

    /// <summary>Called once when the behavior is removed.</summary>
    public virtual void OnDestroy() { }

    public override void OnAttach()
    {
        if (!_awoken) { _awoken = true; try { Awake(); } catch (System.Exception ex) { Log.Exception(ex, GetType().Name + ".Awake"); } }
    }

    public override void OnDetach()
    {
        try { OnDestroy(); } catch (System.Exception ex) { Log.Exception(ex, GetType().Name + ".OnDestroy"); }
    }

    public void RunStart()
    {
        if (_started) return;
        _started = true;
        try { Start(); } catch (System.Exception ex) { Log.Exception(ex, GetType().Name + ".Start"); }
    }

    public void RunUpdate()      { try { Update();      } catch (System.Exception ex) { Log.Exception(ex, GetType().Name + ".Update"); } }
    public void RunFixedUpdate() { try { FixedUpdate(); } catch (System.Exception ex) { Log.Exception(ex, GetType().Name + ".FixedUpdate"); } }
    public void RunLateUpdate()  { try { LateUpdate();  } catch (System.Exception ex) { Log.Exception(ex, GetType().Name + ".LateUpdate"); } }
}
