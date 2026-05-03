using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Nodes;
using Axion.Core;

namespace Axion.Engine;

/// <summary>A keyframe in an AnimationClip — independent rotation/position curves keyed by time.</summary>
public sealed class Keyframe
{
    public float    Time;
    public Vector3? Position;
    public Vector3? EulerDeg;
    public Vector3? Scale;
}

public sealed class AnimationClip
{
    public string Name = "clip";
    public bool   Loop = true;
    public List<Keyframe> Keys = new();
    public float Duration => Keys.Count == 0 ? 0 : Keys[^1].Time;
}

/// <summary>
/// Drives a Transform from one or more AnimationClips. Linear interpolation between keyframes.
/// Production-grade animation (skeletal, blend trees) is roadmapped; this gives you transform tweens.
/// </summary>
public sealed class AnimatorComponent : Behavior, ISerializableComponent
{
    public AnimationClip? Clip;
    public bool Playing = true;
    public float TimeScale = 1f;
    private float _t;

    public override void Update()
    {
        if (Clip == null || !Playing || Clip.Keys.Count == 0) return;
        _t += Time.DeltaTime * TimeScale;
        if (Clip.Loop) _t %= System.MathF.Max(0.001f, Clip.Duration);
        else if (_t > Clip.Duration) { _t = Clip.Duration; Playing = false; }

        // find surrounding keys
        Keyframe a = Clip.Keys[0], b = Clip.Keys[^1];
        for (int i = 0; i < Clip.Keys.Count - 1; i++)
            if (_t >= Clip.Keys[i].Time && _t <= Clip.Keys[i + 1].Time) { a = Clip.Keys[i]; b = Clip.Keys[i + 1]; break; }
        float span = System.MathF.Max(0.0001f, b.Time - a.Time);
        float u = Mathf.Clamp01((_t - a.Time) / span);
        if (a.Position is Vector3 ap && b.Position is Vector3 bp) Transform.LocalPosition = Vec.Lerp(ap, bp, u);
        if (a.EulerDeg is Vector3 ae && b.EulerDeg is Vector3 be) Transform.LocalEulerAngles = Vec.Lerp(ae, be, u);
        if (a.Scale    is Vector3 asn && b.Scale  is Vector3 bs) Transform.LocalScale = Vec.Lerp(asn, bs, u);
    }

    public JsonObject? Serialize()
    {
        if (Clip == null) return null;
        var keys = new JsonArray();
        foreach (var k in Clip.Keys)
        {
            var ko = new JsonObject { ["t"] = k.Time };
            if (k.Position is Vector3 p) ko["pos"] = new JsonObject { ["x"] = p.X, ["y"] = p.Y, ["z"] = p.Z };
            if (k.EulerDeg is Vector3 e) ko["rot"] = new JsonObject { ["x"] = e.X, ["y"] = e.Y, ["z"] = e.Z };
            if (k.Scale    is Vector3 s) ko["scl"] = new JsonObject { ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z };
            keys.Add(ko);
        }
        return new JsonObject
        {
            ["name"] = Clip.Name, ["loop"] = Clip.Loop,
            ["timeScale"] = TimeScale, ["keys"] = keys,
        };
    }

    public void Deserialize(JsonObject d)
    {
        Clip = new AnimationClip
        {
            Name = d["name"]?.GetValue<string>() ?? "clip",
            Loop = d["loop"]?.GetValue<bool>() ?? true,
        };
        TimeScale = d["timeScale"]?.GetValue<float>() ?? 1;
        if (d["keys"] is JsonArray ks)
            foreach (var n in ks)
            {
                var o = n!.AsObject();
                var k = new Keyframe { Time = o["t"]?.GetValue<float>() ?? 0 };
                if (o["pos"] is JsonObject p) k.Position = new Vector3(p["x"]?.GetValue<float>() ?? 0, p["y"]?.GetValue<float>() ?? 0, p["z"]?.GetValue<float>() ?? 0);
                if (o["rot"] is JsonObject r) k.EulerDeg = new Vector3(r["x"]?.GetValue<float>() ?? 0, r["y"]?.GetValue<float>() ?? 0, r["z"]?.GetValue<float>() ?? 0);
                if (o["scl"] is JsonObject s) k.Scale    = new Vector3(s["x"]?.GetValue<float>() ?? 1, s["y"]?.GetValue<float>() ?? 1, s["z"]?.GetValue<float>() ?? 1);
                Clip.Keys.Add(k);
            }
    }
}
