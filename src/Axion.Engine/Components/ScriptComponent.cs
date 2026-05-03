using System.Text.Json.Nodes;
using Axion.Core;
using Axion.Scripting;

namespace Axion.Engine;

/// <summary>
/// Loads a Gel/Silica/Blocks script from disk, compiles it to a Behavior subclass via ScriptHost,
/// and attaches a fresh instance to this GameObject. Serializable across save/load.
/// </summary>
public sealed class ScriptComponent : Component, ISerializableComponent
{
    public string ScriptPath { get; set; } = "";
    public Behavior? Instance { get; private set; }

    public override void OnAttach()
    {
        if (string.IsNullOrEmpty(ScriptPath) || AxionApp.Instance == null) return;
        try
        {
            Instance = AxionApp.Instance.Scripts.Attach(GameObject, ScriptPath);
        }
        catch (System.Exception ex) { Log.Exception(ex, "ScriptComponent"); }
    }

    public JsonObject? Serialize() => new() { ["script"] = ScriptPath };
    public void Deserialize(JsonObject d) => ScriptPath = d["script"]?.GetValue<string>() ?? "";
}
