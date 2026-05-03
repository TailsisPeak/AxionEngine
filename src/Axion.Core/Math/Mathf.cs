using System;
using System.Numerics;

namespace Axion.Core;

/// <summary>Unity-style math helpers backed by System.Numerics.</summary>
public static class Mathf
{
    public const float PI       = MathF.PI;
    public const float Deg2Rad  = MathF.PI / 180f;
    public const float Rad2Deg  = 180f / MathF.PI;
    public const float Epsilon  = 1e-6f;
    public const float Infinity = float.PositiveInfinity;

    public static float Sin(float v)               => MathF.Sin(v);
    public static float Cos(float v)               => MathF.Cos(v);
    public static float Tan(float v)               => MathF.Tan(v);
    public static float Asin(float v)              => MathF.Asin(v);
    public static float Acos(float v)              => MathF.Acos(v);
    public static float Atan(float v)              => MathF.Atan(v);
    public static float Atan2(float y, float x)    => MathF.Atan2(y, x);
    public static float Sqrt(float v)              => MathF.Sqrt(v);
    public static float Pow(float a, float b)      => MathF.Pow(a, b);
    public static float Exp(float v)               => MathF.Exp(v);
    public static float Log(float v)               => MathF.Log(v);
    public static float Abs(float v)               => MathF.Abs(v);
    public static int   Abs(int v)                 => Math.Abs(v);
    public static float Sign(float v)              => MathF.Sign(v);
    public static float Floor(float v)             => MathF.Floor(v);
    public static float Ceil(float v)              => MathF.Ceiling(v);
    public static float Round(float v)             => MathF.Round(v);
    public static int   FloorToInt(float v)        => (int)MathF.Floor(v);
    public static int   CeilToInt(float v)         => (int)MathF.Ceiling(v);
    public static int   RoundToInt(float v)        => (int)MathF.Round(v);

    public static float Min(float a, float b)      => MathF.Min(a, b);
    public static float Max(float a, float b)      => MathF.Max(a, b);
    public static int   Min(int a, int b)          => Math.Min(a, b);
    public static int   Max(int a, int b)          => Math.Max(a, b);
    public static float Clamp(float v, float lo, float hi)  => Math.Clamp(v, lo, hi);
    public static int   Clamp(int v, int lo, int hi)        => Math.Clamp(v, lo, hi);
    public static float Clamp01(float v)            => Math.Clamp(v, 0f, 1f);

    public static float Lerp(float a, float b, float t)         => a + (b - a) * Clamp01(t);
    public static float LerpUnclamped(float a, float b, float t)=> a + (b - a) * t;
    public static float InverseLerp(float a, float b, float v)  => a == b ? 0f : Clamp01((v - a) / (b - a));
    public static float SmoothStep(float a, float b, float t)
    { t = Clamp01((t - a) / (b - a)); return t * t * (3f - 2f * t); }

    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (Abs(target - current) <= maxDelta) return target;
        return current + Sign(target - current) * maxDelta;
    }

    public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);
    public static float PingPong(float t, float length)
    {
        t = Repeat(t, length * 2f);
        return length - Abs(t - length);
    }

    public static bool Approximately(float a, float b)
        => Abs(b - a) < Max(1e-6f * Max(Abs(a), Abs(b)), Epsilon * 8f);

    private static readonly Random _rng = new();
    public static float Random01()                   => (float)_rng.NextDouble();
    public static float Range(float min, float max)  => min + (float)_rng.NextDouble() * (max - min);
    public static int   Range(int min, int max)      => _rng.Next(min, max);
}
