using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Axion.Core;

/// <summary>JSON-based scene save/load. Components are saved by their full type name.</summary>
public static class SceneSerializer
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string ToJson(Scene scene)
    {
        var root = new JsonObject
        {
            ["name"]  = scene.Name,
            ["roots"] = SerializeList(scene.Roots),
        };
        return root.ToJsonString(Opts);
    }

    public static void Save(Scene scene, string path) => File.WriteAllText(path, ToJson(scene));

    private static JsonArray SerializeList(IEnumerable<GameObject> list)
    {
        var arr = new JsonArray();
        foreach (var go in list) arr.Add(SerializeGo(go));
        return arr;
    }

    private static JsonObject SerializeGo(GameObject go)
    {
        var t = go.Transform;
        var obj = new JsonObject
        {
            ["name"]   = go.Name,
            ["tag"]    = go.Tag,
            ["layer"]  = go.Layer,
            ["active"] = go.Active,
            ["transform"] = new JsonObject
            {
                ["position"] = SerializeV3(t.LocalPosition),
                ["rotation"] = SerializeV3(t.LocalEulerAngles),
                ["scale"]    = SerializeV3(t.LocalScale),
            },
            ["components"] = new JsonArray(),
        };
        var comps = (JsonArray)obj["components"]!;
        foreach (var c in go.Components)
        {
            if (c is Transform) continue;
            var jc = new JsonObject { ["type"] = c.GetType().AssemblyQualifiedName };
            if (c is ISerializableComponent ser)
            {
                var data = ser.Serialize();
                if (data != null) jc["data"] = data;
            }
            comps.Add(jc);
        }
        var children = new JsonArray();
        foreach (var ch in t.Children) children.Add(SerializeGo(ch.GameObject));
        if (children.Count > 0) obj["children"] = children;
        return obj;
    }

    public static Scene Load(string path) => FromJson(File.ReadAllText(path));

    public static Scene FromJson(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var scene = new Scene(root["name"]?.GetValue<string>() ?? "Untitled");
        if (root["roots"] is JsonArray roots)
            foreach (var n in roots) DeserializeGo(n!.AsObject(), scene, parent: null);
        return scene;
    }

    private static GameObject DeserializeGo(JsonObject obj, Scene scene, Transform? parent)
    {
        var go = scene.CreateGameObject(obj["name"]?.GetValue<string>() ?? "GameObject");
        if (obj["tag"]    is JsonValue v1) go.Tag    = v1.GetValue<string>();
        if (obj["layer"]  is JsonValue v2) go.Layer  = v2.GetValue<int>();
        if (obj["active"] is JsonValue v3) go.Active = v3.GetValue<bool>();
        if (obj["transform"] is JsonObject tr)
        {
            go.Transform.LocalPosition    = ReadV3(tr["position"]);
            go.Transform.LocalEulerAngles = ReadV3(tr["rotation"]);
            go.Transform.LocalScale       = ReadV3(tr["scale"], Vector3.One);
        }
        if (parent != null) go.Transform.SetParent(parent, keepWorldPosition: false);

        if (obj["components"] is JsonArray comps)
            foreach (var n in comps)
            {
                var jc = n!.AsObject();
                var typeName = jc["type"]?.GetValue<string>();
                if (string.IsNullOrEmpty(typeName)) continue;
                var t = Type.GetType(typeName);
                if (t == null) { Log.Warn($"Unknown component type: {typeName}"); continue; }
                try
                {
                    var c = go.AddComponent(t);
                    if (c is ISerializableComponent ser && jc["data"] is JsonObject d) ser.Deserialize(d);
                }
                catch (Exception ex) { Log.Exception(ex, "SceneSerializer"); }
            }

        if (obj["children"] is JsonArray ch)
            foreach (var n in ch) DeserializeGo(n!.AsObject(), scene, go.Transform);
        return go;
    }

    private static JsonObject SerializeV3(Vector3 v) => new() { ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z };
    private static Vector3 ReadV3(JsonNode? n, Vector3 fallback = default)
    {
        if (n is not JsonObject o) return fallback;
        return new Vector3(
            o["x"]?.GetValue<float>() ?? 0,
            o["y"]?.GetValue<float>() ?? 0,
            o["z"]?.GetValue<float>() ?? 0);
    }
}

/// <summary>Implement on a Component to participate in JSON scene save/load.</summary>
public interface ISerializableComponent
{
    JsonObject? Serialize();
    void Deserialize(JsonObject data);
}
