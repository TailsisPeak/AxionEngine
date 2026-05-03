using System.Numerics;
using System.Text.Json.Nodes;
using Axion.Core;
using Axion.Rendering;

namespace Axion.Engine;

/// <summary>Component wrapper around Axion.Rendering.Camera. The active camera in a scene is rendered.</summary>
public sealed class CameraComponent : Component, ISerializableComponent
{
    public Camera Camera { get; } = new();
    public bool IsMain { get; set; } = true;

    public Matrix4x4 GetView()       => Camera.GetView(Transform.Position, Transform.Rotation);
    public Matrix4x4 GetProjection() => Camera.GetProjection();

    public JsonObject? Serialize() => new()
    {
        ["fov"]    = Camera.FieldOfViewDeg,
        ["near"]   = Camera.Near,
        ["far"]    = Camera.Far,
        ["isMain"] = IsMain,
    };

    public void Deserialize(JsonObject d)
    {
        Camera.FieldOfViewDeg = d["fov"]?.GetValue<float>() ?? 60;
        Camera.Near = d["near"]?.GetValue<float>() ?? 0.1f;
        Camera.Far  = d["far"]?.GetValue<float>() ?? 1000;
        IsMain = d["isMain"]?.GetValue<bool>() ?? true;
    }
}
