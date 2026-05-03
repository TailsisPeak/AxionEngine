using System.IO;
using System.Text.Json.Nodes;

namespace Axion.Core;

/// <summary>A serialized GameObject tree that can be instantiated into any scene.</summary>
public sealed class Prefab
{
    public string Json { get; }
    public string Name { get; }

    private Prefab(string json, string name) { Json = json; Name = name; }

    public static Prefab FromGameObject(GameObject go)
    {
        var tmpScene = new Scene("__prefab__");
        tmpScene.Add(go);
        var json = SceneSerializer.ToJson(tmpScene);
        return new Prefab(json, go.Name);
    }

    public static Prefab Load(string path) => new(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
    public void Save(string path)         => File.WriteAllText(path, Json);

    /// <summary>Spawn a fresh copy into the given scene. Returns the new root GameObject.</summary>
    public GameObject Instantiate(Scene scene)
    {
        var staged = SceneSerializer.FromJson(Json);
        if (staged.Roots.Count == 0) return scene.CreateGameObject(Name);
        var root = staged.Roots[0];
        staged.Remove(root);
        scene.Add(root);
        return root;
    }
}
