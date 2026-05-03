using System;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Axion.Editor;

/// <summary>
/// Minimal Dear ImGui (via ImGui.NET) backend for OpenTK 4 / OpenGL 3.3 core.
/// One per window. Update() must be called once per frame BEFORE any ImGui.* calls;
/// Render() must be called AFTER the 3D scene draws.
/// </summary>
public sealed class ImGuiController : IDisposable
{
    private readonly GameWindow _window;
    private int _width, _height;
    private bool _frameBegun;

    // GL resources
    private int _vao, _vbo, _ibo;
    private int _shader;
    private int _attribLocationTex, _attribLocationProjMtx;
    private int _attribLocationVtxPos, _attribLocationVtxUV, _attribLocationVtxColor;
    private int _fontTexture;

    // Input state
    private readonly System.Collections.Generic.List<char> _pressedChars = new();

    public ImGuiController(GameWindow window)
    {
        _window = window;
        _width  = window.ClientSize.X;
        _height = window.ClientSize.Y;

        var ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(ctx);
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigFlags  |= ImGuiConfigFlags.NavEnableKeyboard;

        ImGui.StyleColorsDark();
        StyleAxion();

        CreateDeviceResources();
        SetKeyMappings();

        _window.TextInput += e => _pressedChars.Add((char)e.Unicode);
        _window.Resize    += e => WindowResized(e.Width, e.Height);

        ImGui.NewFrame();
        _frameBegun = true;
    }

    private static void StyleAxion()
    {
        var s = ImGui.GetStyle();
        s.WindowRounding = 4; s.FrameRounding = 3; s.GrabRounding = 3; s.ScrollbarRounding = 3;
        s.WindowBorderSize = 1; s.FrameBorderSize = 0;
        s.WindowPadding = new System.Numerics.Vector2(8, 8);
        s.FramePadding  = new System.Numerics.Vector2(6, 4);
        s.ItemSpacing   = new System.Numerics.Vector2(6, 4);

        var c = s.Colors;
        System.Numerics.Vector4 V(float r, float g, float b, float a) => new(r, g, b, a);
        c[(int)ImGuiCol.WindowBg]        = V(0.10f, 0.11f, 0.13f, 1);
        c[(int)ImGuiCol.MenuBarBg]       = V(0.13f, 0.14f, 0.17f, 1);
        c[(int)ImGuiCol.TitleBg]         = V(0.10f, 0.11f, 0.13f, 1);
        c[(int)ImGuiCol.TitleBgActive]   = V(0.16f, 0.18f, 0.22f, 1);
        c[(int)ImGuiCol.Header]          = V(0.20f, 0.36f, 0.55f, 1);
        c[(int)ImGuiCol.HeaderHovered]   = V(0.25f, 0.45f, 0.70f, 1);
        c[(int)ImGuiCol.HeaderActive]    = V(0.30f, 0.55f, 0.85f, 1);
        c[(int)ImGuiCol.Button]          = V(0.20f, 0.22f, 0.27f, 1);
        c[(int)ImGuiCol.ButtonHovered]   = V(0.30f, 0.55f, 0.85f, 1);
        c[(int)ImGuiCol.ButtonActive]    = V(0.20f, 0.45f, 0.75f, 1);
        c[(int)ImGuiCol.FrameBg]         = V(0.16f, 0.17f, 0.20f, 1);
        c[(int)ImGuiCol.FrameBgHovered]  = V(0.20f, 0.22f, 0.27f, 1);
        c[(int)ImGuiCol.FrameBgActive]   = V(0.25f, 0.28f, 0.34f, 1);
        c[(int)ImGuiCol.Tab]             = V(0.13f, 0.14f, 0.17f, 1);
        c[(int)ImGuiCol.TabActive]       = V(0.20f, 0.36f, 0.55f, 1);
    }

    public void WindowResized(int w, int h) { _width = w; _height = h; }

    public void Update(float dtSeconds)
    {
        if (_frameBegun) ImGui.Render();

        var io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(_width, _height);
        io.DisplayFramebufferScale = new System.Numerics.Vector2(1, 1);
        io.DeltaTime = dtSeconds > 0 ? dtSeconds : 1f / 60f;

        var mouse = _window.MouseState;
        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, mouse.IsButtonDown(MouseButton.Left));
        io.AddMouseButtonEvent(1, mouse.IsButtonDown(MouseButton.Right));
        io.AddMouseButtonEvent(2, mouse.IsButtonDown(MouseButton.Middle));
        io.AddMouseWheelEvent(mouse.ScrollDelta.X, mouse.ScrollDelta.Y);

        var kb = _window.KeyboardState;
        foreach (Keys k in Enum.GetValues<Keys>())
        {
            if (k == Keys.Unknown) continue;
            var imK = MapKey(k);
            if (imK == ImGuiKey.None) continue;
            io.AddKeyEvent(imK, kb.IsKeyDown(k));
        }
        io.AddKeyEvent(ImGuiKey.ModCtrl,  kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModShift, kb.IsKeyDown(Keys.LeftShift)   || kb.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModAlt,   kb.IsKeyDown(Keys.LeftAlt)     || kb.IsKeyDown(Keys.RightAlt));
        io.AddKeyEvent(ImGuiKey.ModSuper, kb.IsKeyDown(Keys.LeftSuper)   || kb.IsKeyDown(Keys.RightSuper));

        foreach (var c in _pressedChars) io.AddInputCharacter(c);
        _pressedChars.Clear();

        ImGui.NewFrame();
        _frameBegun = true;
    }

    public void Render()
    {
        if (!_frameBegun) return;
        _frameBegun = false;
        ImGui.Render();
        RenderImDrawData(ImGui.GetDrawData());
    }

    // ── GL backend ──────────────────────────────────────────────────────────
    private void CreateDeviceResources()
    {
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ibo = GL.GenBuffer();

        const string vs = @"#version 330 core
layout(location=0) in vec2 in_Position;
layout(location=1) in vec2 in_TexCoord;
layout(location=2) in vec4 in_Color;
uniform mat4 projection_matrix;
out vec4 color; out vec2 texCoord;
void main(){ gl_Position = projection_matrix * vec4(in_Position, 0, 1); color = in_Color; texCoord = in_TexCoord; }";
        const string fs = @"#version 330 core
in vec4 color; in vec2 texCoord;
uniform sampler2D in_fontTexture;
out vec4 outputColor;
void main(){ outputColor = color * texture(in_fontTexture, texCoord); }";
        _shader = LinkProgram(vs, fs);

        _attribLocationTex     = GL.GetUniformLocation(_shader, "in_fontTexture");
        _attribLocationProjMtx = GL.GetUniformLocation(_shader, "projection_matrix");
        _attribLocationVtxPos  = GL.GetAttribLocation(_shader, "in_Position");
        _attribLocationVtxUV   = GL.GetAttribLocation(_shader, "in_TexCoord");
        _attribLocationVtxColor= GL.GetAttribLocation(_shader, "in_Color");

        RecreateFontDeviceTexture();
    }

    private static int LinkProgram(string vsSource, string fsSource)
    {
        int Compile(ShaderType t, string s)
        {
            int sh = GL.CreateShader(t);
            GL.ShaderSource(sh, s); GL.CompileShader(sh);
            GL.GetShader(sh, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0) throw new Exception($"ImGui shader compile failed:\n{GL.GetShaderInfoLog(sh)}");
            return sh;
        }
        int vs = Compile(ShaderType.VertexShader, vsSource);
        int fs = Compile(ShaderType.FragmentShader, fsSource);
        int p = GL.CreateProgram();
        GL.AttachShader(p, vs); GL.AttachShader(p, fs);
        GL.LinkProgram(p);
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0) throw new Exception($"ImGui shader link failed:\n{GL.GetProgramInfoLog(p)}");
        GL.DetachShader(p, vs); GL.DetachShader(p, fs);
        GL.DeleteShader(vs);    GL.DeleteShader(fs);
        return p;
    }

    private unsafe void RecreateFontDeviceTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int w, out int h, out int bpp);

        _fontTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, w, h, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        io.Fonts.SetTexID((IntPtr)_fontTexture);
        io.Fonts.ClearTexData();
    }

    private unsafe void RenderImDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0) return;

        // Save GL state
        GL.GetInteger(GetPName.ActiveTexture, out int prevActiveTexture);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.GetInteger(GetPName.CurrentProgram, out int prevProgram);
        GL.GetInteger(GetPName.TextureBinding2D, out int prevTexture);
        GL.GetInteger(GetPName.ArrayBufferBinding, out int prevArrayBuffer);
        GL.GetInteger(GetPName.VertexArrayBinding, out int prevVao);
        GL.GetInteger(GetPName.ElementArrayBufferBinding, out int prevEbo);
        bool prevBlendEnabled    = GL.IsEnabled(EnableCap.Blend);
        bool prevCullEnabled     = GL.IsEnabled(EnableCap.CullFace);
        bool prevDepthEnabled    = GL.IsEnabled(EnableCap.DepthTest);
        bool prevScissorEnabled  = GL.IsEnabled(EnableCap.ScissorTest);

        GL.Enable(EnableCap.Blend);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha,
                              BlendingFactorSrc.One,     BlendingFactorDest.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable (EnableCap.ScissorTest);

        var io = ImGui.GetIO();
        var L = drawData.DisplayPos.X;
        var R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        var T = drawData.DisplayPos.Y;
        var B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;
        var ortho = Matrix4.CreateOrthographicOffCenter(L, R, B, T, -1, 1);

        GL.UseProgram(_shader);
        GL.Uniform1(_attribLocationTex, 0);
        GL.UniformMatrix4(_attribLocationProjMtx, false, ref ortho);

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ibo);

        GL.EnableVertexAttribArray(_attribLocationVtxPos);
        GL.EnableVertexAttribArray(_attribLocationVtxUV);
        GL.EnableVertexAttribArray(_attribLocationVtxColor);
        int stride = sizeof(float) * 4 + 4; // 2 pos + 2 uv + 4 color (uint8)
        GL.VertexAttribPointer(_attribLocationVtxPos,   2, VertexAttribPointerType.Float, false, stride, 0);
        GL.VertexAttribPointer(_attribLocationVtxUV,    2, VertexAttribPointerType.Float, false, stride, sizeof(float) * 2);
        GL.VertexAttribPointer(_attribLocationVtxColor, 4, VertexAttribPointerType.UnsignedByte, true, stride, sizeof(float) * 4);

        var clipOff   = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];
            int vtxBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
            int idxBytes = cmdList.IdxBuffer.Size * sizeof(ushort);

            GL.BufferData(BufferTarget.ArrayBuffer,        vtxBytes, cmdList.VtxBuffer.Data, BufferUsageHint.StreamDraw);
            GL.BufferData(BufferTarget.ElementArrayBuffer, idxBytes, cmdList.IdxBuffer.Data, BufferUsageHint.StreamDraw);

            for (int cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++)
            {
                var pcmd = cmdList.CmdBuffer[cmdI];
                var clipMin = new System.Numerics.Vector2((pcmd.ClipRect.X - clipOff.X) * clipScale.X, (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y);
                var clipMax = new System.Numerics.Vector2((pcmd.ClipRect.Z - clipOff.X) * clipScale.X, (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y);
                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) continue;

                GL.Scissor((int)clipMin.X, _height - (int)clipMax.Y,
                           (int)(clipMax.X - clipMin.X), (int)(clipMax.Y - clipMin.Y));
                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);
                GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount,
                    DrawElementsType.UnsignedShort, (IntPtr)(pcmd.IdxOffset * sizeof(ushort)),
                    (int)pcmd.VtxOffset);
            }
        }

        // Restore state
        GL.UseProgram(prevProgram);
        GL.BindTexture(TextureTarget.Texture2D, prevTexture);
        GL.ActiveTexture((TextureUnit)prevActiveTexture);
        GL.BindVertexArray(prevVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, prevArrayBuffer);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, prevEbo);
        if (prevBlendEnabled)   GL.Enable(EnableCap.Blend);    else GL.Disable(EnableCap.Blend);
        if (prevCullEnabled)    GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
        if (prevDepthEnabled)   GL.Enable(EnableCap.DepthTest);else GL.Disable(EnableCap.DepthTest);
        if (!prevScissorEnabled) GL.Disable(EnableCap.ScissorTest);
    }

    private static void SetKeyMappings() { /* using new io.AddKeyEvent — no-op */ }

    private static ImGuiKey MapKey(Keys k) => k switch
    {
        Keys.Tab => ImGuiKey.Tab, Keys.Left => ImGuiKey.LeftArrow, Keys.Right => ImGuiKey.RightArrow,
        Keys.Up => ImGuiKey.UpArrow, Keys.Down => ImGuiKey.DownArrow,
        Keys.PageUp => ImGuiKey.PageUp, Keys.PageDown => ImGuiKey.PageDown,
        Keys.Home => ImGuiKey.Home, Keys.End => ImGuiKey.End, Keys.Insert => ImGuiKey.Insert,
        Keys.Delete => ImGuiKey.Delete, Keys.Backspace => ImGuiKey.Backspace, Keys.Space => ImGuiKey.Space,
        Keys.Enter => ImGuiKey.Enter, Keys.Escape => ImGuiKey.Escape, Keys.Apostrophe => ImGuiKey.Apostrophe,
        Keys.Comma => ImGuiKey.Comma, Keys.Minus => ImGuiKey.Minus, Keys.Period => ImGuiKey.Period,
        Keys.Slash => ImGuiKey.Slash, Keys.Semicolon => ImGuiKey.Semicolon, Keys.Equal => ImGuiKey.Equal,
        Keys.LeftBracket => ImGuiKey.LeftBracket, Keys.RightBracket => ImGuiKey.RightBracket,
        Keys.Backslash => ImGuiKey.Backslash, Keys.GraveAccent => ImGuiKey.GraveAccent,
        Keys.CapsLock => ImGuiKey.CapsLock, Keys.ScrollLock => ImGuiKey.ScrollLock,
        Keys.NumLock => ImGuiKey.NumLock, Keys.PrintScreen => ImGuiKey.PrintScreen, Keys.Pause => ImGuiKey.Pause,
        Keys.D0 => ImGuiKey._0, Keys.D1 => ImGuiKey._1, Keys.D2 => ImGuiKey._2, Keys.D3 => ImGuiKey._3,
        Keys.D4 => ImGuiKey._4, Keys.D5 => ImGuiKey._5, Keys.D6 => ImGuiKey._6, Keys.D7 => ImGuiKey._7,
        Keys.D8 => ImGuiKey._8, Keys.D9 => ImGuiKey._9,
        Keys.A => ImGuiKey.A, Keys.B => ImGuiKey.B, Keys.C => ImGuiKey.C, Keys.D => ImGuiKey.D,
        Keys.E => ImGuiKey.E, Keys.F => ImGuiKey.F, Keys.G => ImGuiKey.G, Keys.H => ImGuiKey.H,
        Keys.I => ImGuiKey.I, Keys.J => ImGuiKey.J, Keys.K => ImGuiKey.K, Keys.L => ImGuiKey.L,
        Keys.M => ImGuiKey.M, Keys.N => ImGuiKey.N, Keys.O => ImGuiKey.O, Keys.P => ImGuiKey.P,
        Keys.Q => ImGuiKey.Q, Keys.R => ImGuiKey.R, Keys.S => ImGuiKey.S, Keys.T => ImGuiKey.T,
        Keys.U => ImGuiKey.U, Keys.V => ImGuiKey.V, Keys.W => ImGuiKey.W, Keys.X => ImGuiKey.X,
        Keys.Y => ImGuiKey.Y, Keys.Z => ImGuiKey.Z,
        Keys.F1 => ImGuiKey.F1, Keys.F2 => ImGuiKey.F2, Keys.F3 => ImGuiKey.F3, Keys.F4 => ImGuiKey.F4,
        Keys.F5 => ImGuiKey.F5, Keys.F6 => ImGuiKey.F6, Keys.F7 => ImGuiKey.F7, Keys.F8 => ImGuiKey.F8,
        Keys.F9 => ImGuiKey.F9, Keys.F10 => ImGuiKey.F10, Keys.F11 => ImGuiKey.F11, Keys.F12 => ImGuiKey.F12,
        _ => ImGuiKey.None,
    };

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ibo);
        GL.DeleteProgram(_shader);
        GL.DeleteTexture(_fontTexture);
    }
}
