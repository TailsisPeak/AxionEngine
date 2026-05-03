using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Audio.OpenAL;
using Axion.Core;

namespace Axion.Engine;

/// <summary>
/// Minimal OpenAL audio backend: loads .wav clips, drives all AudioSourceComponents.
/// Format support: 16-bit PCM mono/stereo. (MP3/Ogg are roadmapped.)
/// </summary>
public sealed class AudioSystem : IDisposable
{
    public static AudioSystem? Instance { get; private set; }

    private readonly ALDevice  _device;
    private readonly ALContext _context;
    private readonly Dictionary<string, int> _bufferCache = new();
    private bool _disabled;

    public AudioSystem()
    {
        Instance = this;
        try
        {
            _device  = ALC.OpenDevice(null);
            _context = ALC.CreateContext(_device, (int[]?)null);
            ALC.MakeContextCurrent(_context);
        }
        catch (Exception ex) { Log.Warn("Audio disabled: " + ex.Message); _disabled = true; }
    }

    public void Play(AudioSourceComponent c)
    {
        if (_disabled || string.IsNullOrEmpty(c.Clip)) return;
        if (c.AlSource == 0) c.AlSource = AL.GenSource();
        if (c.AlBuffer == 0) c.AlBuffer = LoadClip(c.Clip);
        AL.Source(c.AlSource, ALSourcei.Buffer, c.AlBuffer);
        AL.Source(c.AlSource, ALSourcef.Gain,   c.Volume);
        AL.Source(c.AlSource, ALSourcef.Pitch,  c.Pitch);
        AL.Source(c.AlSource, ALSourceb.Looping, c.Loop);
        var p = c.Transform.Position;
        AL.Source(c.AlSource, ALSource3f.Position, p.X, p.Y, p.Z);
        AL.SourcePlay(c.AlSource);
    }

    public void Stop (AudioSourceComponent c) { if (c.AlSource != 0) AL.SourceStop(c.AlSource); }
    public void Pause(AudioSourceComponent c) { if (c.AlSource != 0) AL.SourcePause(c.AlSource); }

    private int LoadClip(string relativePath)
    {
        if (_bufferCache.TryGetValue(relativePath, out var cached)) return cached;
        var full = Path.Combine(AssetDatabase.RootPath, relativePath);
        if (!File.Exists(full)) { Log.Warn("Audio clip not found: " + full); return 0; }
        var (data, fmt, freq) = LoadWav(full);
        int buf = AL.GenBuffer();
        AL.BufferData(buf, fmt, ref data[0], data.Length, freq);
        _bufferCache[relativePath] = buf;
        return buf;
    }

    /// <summary>Tiny PCM-only .wav loader.</summary>
    private static (byte[] data, ALFormat fmt, int freq) LoadWav(string path)
    {
        using var br = new BinaryReader(File.OpenRead(path));
        if (new string(br.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a RIFF file");
        br.ReadInt32(); // size
        if (new string(br.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAVE file");
        short channels = 1, bits = 16; int rate = 44100;
        byte[] data = Array.Empty<byte>();
        while (br.BaseStream.Position < br.BaseStream.Length)
        {
            var id = new string(br.ReadChars(4));
            int sz = br.ReadInt32();
            if (id == "fmt ")
            {
                br.ReadInt16(); channels = br.ReadInt16();
                rate = br.ReadInt32(); br.ReadInt32(); br.ReadInt16();
                bits = br.ReadInt16();
                if (sz > 16) br.ReadBytes(sz - 16);
            }
            else if (id == "data") { data = br.ReadBytes(sz); break; }
            else br.ReadBytes(sz);
        }
        var fmt = (channels, bits) switch
        {
            (1, 8)  => ALFormat.Mono8,
            (1, 16) => ALFormat.Mono16,
            (2, 8)  => ALFormat.Stereo8,
            (2, 16) => ALFormat.Stereo16,
            _ => ALFormat.Mono16,
        };
        return (data, fmt, rate);
    }

    public void Dispose()
    {
        if (_disabled) return;
        foreach (var b in _bufferCache.Values) AL.DeleteBuffer(b);
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(_context);
        ALC.CloseDevice(_device);
        Instance = null;
    }
}
