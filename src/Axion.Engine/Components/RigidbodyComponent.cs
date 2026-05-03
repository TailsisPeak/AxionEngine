using System.Numerics;
using System.Text.Json.Nodes;
using BepuPhysics;
using BepuPhysics.Collidables;
using Axion.Core;
using Axion.Physics;

namespace Axion.Engine;

public enum ColliderShape { Box, Sphere }

/// <summary>
/// A physics body owned by the scene's PhysicsWorld. Combine with the implicit Transform of the
/// GameObject; the engine syncs Bepu pose ↔ Transform once per fixed step.
/// </summary>
public sealed class RigidbodyComponent : Component, ISerializableComponent
{
    public ColliderShape Shape { get; set; } = ColliderShape.Box;
    public Vector3 Size  { get; set; } = Vector3.One;       // box extents (full); sphere: x = radius
    public float   Mass  { get; set; } = 1f;
    public bool    IsKinematic { get; set; } = false;

    internal BodyHandle BodyHandle;
    internal bool       Registered;

    public void ApplyImpulse(Vector3 impulse)
    {
        if (!Registered) return;
        var sim = AxionApp.Instance?.Physics?.Simulation;
        if (sim == null) return;
        var body = sim.Bodies[BodyHandle];
        body.ApplyLinearImpulse(impulse);
    }

    public Vector3 LinearVelocity
    {
        get
        {
            var sim = AxionApp.Instance?.Physics?.Simulation;
            if (sim == null || !Registered) return Vector3.Zero;
            return sim.Bodies[BodyHandle].Velocity.Linear;
        }
        set
        {
            var sim = AxionApp.Instance?.Physics?.Simulation;
            if (sim == null || !Registered) return;
            var b = sim.Bodies[BodyHandle];
            b.Velocity.Linear = value;
        }
    }

    public JsonObject? Serialize() => new()
    {
        ["shape"] = Shape.ToString(),
        ["size"]  = new JsonObject { ["x"] = Size.X, ["y"] = Size.Y, ["z"] = Size.Z },
        ["mass"]  = Mass,
        ["kinematic"] = IsKinematic,
    };

    public void Deserialize(JsonObject d)
    {
        if (d["shape"] is JsonValue s && System.Enum.TryParse<ColliderShape>(s.GetValue<string>(), out var sh)) Shape = sh;
        if (d["size"] is JsonObject sz) Size = new Vector3(
            sz["x"]?.GetValue<float>() ?? 1,
            sz["y"]?.GetValue<float>() ?? 1,
            sz["z"]?.GetValue<float>() ?? 1);
        Mass = d["mass"]?.GetValue<float>() ?? 1;
        IsKinematic = d["kinematic"]?.GetValue<bool>() ?? false;
    }
}
