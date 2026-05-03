using System.Collections.Generic;
using System.Numerics;

namespace Axion.Core;

/// <summary>Keyboard & mouse keys (subset of the OpenTK key enum, mirrored here so Axion.Core has no rendering dependency).</summary>
public enum Key
{
    None,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    Space, Enter, Escape, Backspace, Tab,
    Left, Right, Up, Down,
    LShift, RShift, LCtrl, RCtrl, LAlt, RAlt,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    MouseLeft, MouseRight, MouseMiddle,
}

/// <summary>Frame-stable input state. Pumped by the engine; queried by scripts.</summary>
public static class Input
{
    private static readonly HashSet<Key> _down  = new();
    private static readonly HashSet<Key> _press = new();
    private static readonly HashSet<Key> _release = new();
    private static Vector2 _mousePos;
    private static Vector2 _mouseDelta;
    private static float _scrollDelta;

    public static Vector2 MousePosition => _mousePos;
    public static Vector2 MouseDelta    => _mouseDelta;
    public static float   ScrollDelta   => _scrollDelta;

    public static bool GetKey(Key k)        => _down.Contains(k);
    public static bool GetKeyDown(Key k)    => _press.Contains(k);
    public static bool GetKeyUp(Key k)      => _release.Contains(k);

    public static float GetAxis(string name) => name switch
    {
        "Horizontal" => (GetKey(Key.D) || GetKey(Key.Right) ? 1f : 0f) - (GetKey(Key.A) || GetKey(Key.Left) ? 1f : 0f),
        "Vertical"   => (GetKey(Key.W) || GetKey(Key.Up)    ? 1f : 0f) - (GetKey(Key.S) || GetKey(Key.Down) ? 1f : 0f),
        _ => 0f,
    };

    // ── Engine-internal hooks ────────────────────────────────────────────────
    public static void BeginFrame()
    {
        _press.Clear();
        _release.Clear();
        _mouseDelta = Vector2.Zero;
        _scrollDelta = 0;
    }

    public static void PushKeyDown(Key k)        { if (_down.Add(k)) _press.Add(k); }
    public static void PushKeyUp  (Key k)        { if (_down.Remove(k)) _release.Add(k); }
    public static void PushMouseMove(Vector2 p, Vector2 d) { _mousePos = p; _mouseDelta += d; }
    public static void PushScroll(float dy)      { _scrollDelta += dy; }
}
