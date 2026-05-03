using System;
using System.Numerics;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Axion.Core;

namespace Axion.Rendering;

/// <summary>Owns the OS window + GL context. Forwards keyboard/mouse to Axion.Core.Input.</summary>
public sealed class Window : GameWindow
{
    public event Action? OnLoadCallback;
    public event Action<float>? OnUpdateCallback;
    public event Action<float>? OnRenderCallback;
    public event Action<int, int>? OnResizeCallback;

    public Window(string title, int width, int height)
        : base(GameWindowSettings.Default, new NativeWindowSettings
        {
            Title = title,
            ClientSize = new Vector2i(width, height),
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
        })
    { }

    protected override void OnLoad()
    {
        base.OnLoad();
        OnLoadCallback?.Invoke();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        OnResizeCallback?.Invoke(e.Width, e.Height);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        Input.BeginFrame();
        if (KeyboardState.IsKeyDown(Keys.Escape)) Close();

        // Pump key state ────────────────────────────────────────────────
        foreach (Keys k in Enum.GetValues<Keys>())
        {
            if (k == Keys.Unknown) continue;
            var ak = MapKey(k);
            if (ak == Key.None) continue;
            if (KeyboardState.IsKeyPressed(k))  Input.PushKeyDown(ak);
            if (KeyboardState.IsKeyReleased(k)) Input.PushKeyUp(ak);
        }
        // Mouse buttons
        if (MouseState.IsButtonPressed(MouseButton.Left))   Input.PushKeyDown(Key.MouseLeft);
        if (MouseState.IsButtonReleased(MouseButton.Left))  Input.PushKeyUp  (Key.MouseLeft);
        if (MouseState.IsButtonPressed(MouseButton.Right))  Input.PushKeyDown(Key.MouseRight);
        if (MouseState.IsButtonReleased(MouseButton.Right)) Input.PushKeyUp  (Key.MouseRight);
        if (MouseState.IsButtonPressed(MouseButton.Middle)) Input.PushKeyDown(Key.MouseMiddle);
        if (MouseState.IsButtonReleased(MouseButton.Middle))Input.PushKeyUp  (Key.MouseMiddle);
        Input.PushMouseMove(new System.Numerics.Vector2(MouseState.X, MouseState.Y), new System.Numerics.Vector2(MouseState.Delta.X, MouseState.Delta.Y));
        Input.PushScroll(MouseState.ScrollDelta.Y);

        OnUpdateCallback?.Invoke((float)args.Time);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        OnRenderCallback?.Invoke((float)args.Time);
        SwapBuffers();
    }

    private static Key MapKey(Keys k) => k switch
    {
        Keys.A => Key.A, Keys.B => Key.B, Keys.C => Key.C, Keys.D => Key.D, Keys.E => Key.E, Keys.F => Key.F,
        Keys.G => Key.G, Keys.H => Key.H, Keys.I => Key.I, Keys.J => Key.J, Keys.K => Key.K, Keys.L => Key.L,
        Keys.M => Key.M, Keys.N => Key.N, Keys.O => Key.O, Keys.P => Key.P, Keys.Q => Key.Q, Keys.R => Key.R,
        Keys.S => Key.S, Keys.T => Key.T, Keys.U => Key.U, Keys.V => Key.V, Keys.W => Key.W, Keys.X => Key.X,
        Keys.Y => Key.Y, Keys.Z => Key.Z,
        Keys.D0 => Key.D0, Keys.D1 => Key.D1, Keys.D2 => Key.D2, Keys.D3 => Key.D3, Keys.D4 => Key.D4,
        Keys.D5 => Key.D5, Keys.D6 => Key.D6, Keys.D7 => Key.D7, Keys.D8 => Key.D8, Keys.D9 => Key.D9,
        Keys.Space => Key.Space, Keys.Enter => Key.Enter, Keys.Escape => Key.Escape,
        Keys.Backspace => Key.Backspace, Keys.Tab => Key.Tab,
        Keys.Left => Key.Left, Keys.Right => Key.Right, Keys.Up => Key.Up, Keys.Down => Key.Down,
        Keys.LeftShift => Key.LShift, Keys.RightShift => Key.RShift,
        Keys.LeftControl => Key.LCtrl, Keys.RightControl => Key.RCtrl,
        Keys.LeftAlt => Key.LAlt, Keys.RightAlt => Key.RAlt,
        Keys.F1 => Key.F1, Keys.F2 => Key.F2, Keys.F3 => Key.F3, Keys.F4 => Key.F4,
        Keys.F5 => Key.F5, Keys.F6 => Key.F6, Keys.F7 => Key.F7, Keys.F8 => Key.F8,
        Keys.F9 => Key.F9, Keys.F10 => Key.F10, Keys.F11 => Key.F11, Keys.F12 => Key.F12,
        _ => Key.None,
    };
}
