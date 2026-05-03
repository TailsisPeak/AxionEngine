using System.Text.Json.Nodes;
using Axion.Core;
using Axion.Rendering;

namespace Axion.Engine;

/// <summary>Marks a GameObject as renderable. Holds a Mesh + Material reference.</summary>
public sealed class MeshRendererComponent : Component, ISerializableComponent
{
    private Mesh? _mesh;
    /// <summary>Mesh handle. Lazily built from PrimitiveName the first time it's accessed
    /// (which guarantees a live GL context).</summary>
    public Mesh? Mesh
    {
        get { EnsureMesh(); return _mesh; }
        set { _mesh = value; }
    }
    public Material Material { get; set; } = Material.Default;

    /// <summary>Convenience: assign a built-in primitive by name. Used by the editor + scene serializer.</summary>
    public string PrimitiveName { get; set; } = "";

    /// <summary>Set a built-in primitive. Mesh is built lazily — safe to call before GL exists.</summary>
    public void SetPrimitive(string name)
    {
        PrimitiveName = name;
        _mesh = null; // rebuild on next access
    }

    private void EnsureMesh()
    {
        if (_mesh != null || string.IsNullOrEmpty(PrimitiveName)) return;
        _mesh = PrimitiveName.ToLowerInvariant() switch
        {
            "cube"   => Rendering.Mesh.Cube(),
            "quad"   => Rendering.Mesh.Quad(),
            "sphere" => Rendering.Mesh.Sphere(),
            _        => Rendering.Mesh.Cube(),
        };
    }

    public JsonObject? Serialize() => new()
    {
        ["primitive"] = PrimitiveName,
        ["color"] = new JsonObject { ["r"] = Material.Color.X, ["g"] = Material.Color.Y, ["b"] = Material.Color.Z },
    };

    public void Deserialize(JsonObject d)
    {
        // Don't build the mesh here — GL may not exist yet (scene loads before window).
        PrimitiveName = d["primitive"]?.GetValue<string>() ?? "cube";
        if (d["color"] is JsonObject c)
            Material.Color = new System.Numerics.Vector3(
                c["r"]?.GetValue<float>() ?? 1,
                c["g"]?.GetValue<float>() ?? 1,
                c["b"]?.GetValue<float>() ?? 1);
    }
}
