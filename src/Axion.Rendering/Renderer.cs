using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Axion.Core;

namespace Axion.Rendering;

/// <summary>One Renderer per Window. Owns the default shader; called once per frame by AxionApp.</summary>
public sealed class Renderer : System.IDisposable
{
    public Shader DefaultShader { get; }
    public Vector3 ClearColor   { get; set; } = new(0.10f, 0.11f, 0.13f);
    public Vector3 LightDir     { get; set; } = Vector3.Normalize(new(-0.4f, -1f, -0.3f));
    public Vector3 LightColor   { get; set; } = new(0.85f, 0.85f, 0.80f);
    public Vector3 Ambient      { get; set; } = new(0.18f, 0.18f, 0.22f);

    public Renderer()
    {
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);
        DefaultShader = Shader.Default();
    }

    public void BeginFrame(int width, int height)
    {
        GL.Viewport(0, 0, width, height);
        GL.ClearColor(ClearColor.X, ClearColor.Y, ClearColor.Z, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void Draw(Mesh mesh, Material material, Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj)
    {
        var sh = material.Shader ?? DefaultShader;
        sh.Use();
        sh.Set("uModel",      model);
        sh.Set("uView",       view);
        sh.Set("uProj",       proj);
        sh.Set("uColor",      material.Color);
        sh.Set("uLightDir",   LightDir);
        sh.Set("uLightColor", LightColor);
        sh.Set("uAmbient",    Ambient);
        material.Albedo?.Bind(0);
        mesh.Draw();
    }

    public void Dispose() => DefaultShader.Dispose();
}
