using System.Diagnostics;

namespace Axion.Core;

/// <summary>Engine-wide time (Unity-equivalent of UnityEngine.Time).</summary>
public static class Time
{
    private static readonly Stopwatch _sw = Stopwatch.StartNew();
    private static double _last;

    /// <summary>Real seconds since engine start.</summary>
    public static float ElapsedTime { get; private set; }
    /// <summary>Time between this frame and the previous, in seconds.</summary>
    public static float DeltaTime   { get; private set; }
    /// <summary>Frame index since engine start.</summary>
    public static long  FrameCount  { get; private set; }
    /// <summary>Multiplier applied to DeltaTime; 0 = paused, 1 = real-time, 2 = 2x speed.</summary>
    public static float TimeScale   { get; set; } = 1f;
    /// <summary>Fixed timestep used by the physics step (default 60 Hz).</summary>
    public static float FixedDelta  { get; set; } = 1f / 60f;

    /// <summary>Called by the engine main loop once per frame. Do not call yourself.</summary>
    public static void Tick()
    {
        double now = _sw.Elapsed.TotalSeconds;
        double rawDelta = _last == 0 ? 1.0 / 60.0 : now - _last;
        _last = now;
        ElapsedTime = (float)now;
        DeltaTime   = (float)rawDelta * TimeScale;
        FrameCount++;
    }

    public static void Reset()
    {
        _sw.Restart();
        _last = 0;
        ElapsedTime = 0;
        DeltaTime = 0;
        FrameCount = 0;
    }
}
