using System.Numerics;
using System.Text.Json.Nodes;
using Axion.Core;

namespace Axion.Engine;

public enum LightType { Directional, Point, Spot }

/// <summary>A scene light. The renderer aggregates all enabled lights each frame.</summary>
public sealed class LightComponent : Component, ISerializableComponent
{
    public LightType Type      { get; set; } = LightType.Directional;
    public Vector3   Color     { get; set; } = new(1, 0.95f, 0.9f);
    public float     Intensity { get; set; } = 1f;
    public float     Range     { get; set; } = 10f;     // for point/spot
    public float     SpotAngleDeg { get; set; } = 45f;  // for spot

    public JsonObject? Serialize() => new()
    {
        ["type"] = Type.ToString(),
        ["color"] = new JsonObject { ["r"] = Color.X, ["g"] = Color.Y, ["b"] = Color.Z },
        ["intensity"] = Intensity,
        ["range"] = Range,
        ["spotAngle"] = SpotAngleDeg,
    };

    public void Deserialize(JsonObject d)
    {
        if (d["type"] is JsonValue t && System.Enum.TryParse<LightType>(t.GetValue<string>(), out var lt)) Type = lt;
        if (d["color"] is JsonObject c) Color = new Vector3(
            c["r"]?.GetValue<float>() ?? 1,
            c["g"]?.GetValue<float>() ?? 1,
            c["b"]?.GetValue<float>() ?? 1);
        Intensity = d["intensity"]?.GetValue<float>() ?? 1f;
        Range = d["range"]?.GetValue<float>() ?? 10f;
        SpotAngleDeg = d["spotAngle"]?.GetValue<float>() ?? 45f;
    }
}
