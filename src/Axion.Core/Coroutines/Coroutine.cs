using System.Collections;
using System.Collections.Generic;

namespace Axion.Core;

/// <summary>Yield helpers for coroutine-style scripts (matches Unity's WaitForSeconds / WaitForFixedUpdate).</summary>
public static class Yield
{
    public static IEnumerator WaitForSeconds(float seconds)
    {
        float end = Time.ElapsedTime + seconds;
        while (Time.ElapsedTime < end) yield return null;
    }

    public static IEnumerator WaitFrames(int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;
    }
}

/// <summary>Drives all running coroutines. The engine ticks this every frame.</summary>
public static class CoroutineRunner
{
    private static readonly List<IEnumerator> _running = new();
    private static readonly List<IEnumerator> _toAdd = new();

    public static IEnumerator Start(IEnumerator co) { _toAdd.Add(co); return co; }

    public static void Stop(IEnumerator co)
    {
        _running.Remove(co);
        _toAdd.Remove(co);
    }

    public static void StopAll() { _running.Clear(); _toAdd.Clear(); }

    public static void Tick()
    {
        if (_toAdd.Count > 0) { _running.AddRange(_toAdd); _toAdd.Clear(); }
        for (int i = _running.Count - 1; i >= 0; i--)
        {
            var co = _running[i];
            try
            {
                if (!co.MoveNext()) _running.RemoveAt(i);
            }
            catch (System.Exception ex) { Log.Exception(ex, "Coroutine"); _running.RemoveAt(i); }
        }
    }
}
