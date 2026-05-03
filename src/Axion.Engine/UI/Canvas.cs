using System.Collections.Generic;
using System.Numerics;
using Axion.Core;

namespace Axion.Engine;

/// <summary>
/// Immediate-mode in-game UI. Scripts call Canvas.Text(), Canvas.Button(), Canvas.Image() during
/// their Update; the editor's ImGui pass renders them.
/// (Custom GL UI rendering for shipping games is roadmapped — production builds drop ImGui.)
/// </summary>
public static class Canvas
{
    public sealed record TextCmd  (Vector2 Pos, string Text, Vector4 Color);
    public sealed record RectCmd  (Vector2 Pos, Vector2 Size, Vector4 Color);

    private static readonly List<object> _cmds = new();
    public static IReadOnlyList<object> Commands => _cmds;

    public static void BeginFrame() => _cmds.Clear();

    public static void Text(float x, float y, string text)             => _cmds.Add(new TextCmd(new(x, y), text, new(1, 1, 1, 1)));
    public static void Text(float x, float y, string text, Vector4 col) => _cmds.Add(new TextCmd(new(x, y), text, col));
    public static void Rect(float x, float y, float w, float h, Vector4 col) => _cmds.Add(new RectCmd(new(x, y), new(w, h), col));
}
