using System.Text.Json.Nodes;
using Axion.Core;

namespace Axion.Engine;

/// <summary>
/// 3D positional audio source. Plays a clip resolved via AssetDatabase.
/// Engine integration: the AudioSystem (one per AxionApp) drives playback via OpenAL.
/// </summary>
public sealed class AudioSourceComponent : Component, ISerializableComponent
{
    public string Clip       { get; set; } = "";
    public float  Volume     { get; set; } = 1f;
    public float  Pitch      { get; set; } = 1f;
    public bool   Loop       { get; set; } = false;
    public bool   PlayOnStart{ get; set; } = false;

    internal int AlSource;       // OpenAL source name; 0 = unallocated
    internal int AlBuffer;

    public void Play()  { AudioSystem.Instance?.Play(this); }
    public void Stop()  { AudioSystem.Instance?.Stop(this); }
    public void Pause() { AudioSystem.Instance?.Pause(this); }

    public JsonObject? Serialize() => new()
    {
        ["clip"] = Clip, ["volume"] = Volume, ["pitch"] = Pitch,
        ["loop"] = Loop, ["playOnStart"] = PlayOnStart,
    };
    public void Deserialize(JsonObject d)
    {
        Clip = d["clip"]?.GetValue<string>() ?? "";
        Volume = d["volume"]?.GetValue<float>() ?? 1;
        Pitch  = d["pitch"]?.GetValue<float>() ?? 1;
        Loop   = d["loop"]?.GetValue<bool>() ?? false;
        PlayOnStart = d["playOnStart"]?.GetValue<bool>() ?? false;
    }
}
