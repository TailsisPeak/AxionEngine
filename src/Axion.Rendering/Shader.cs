using System;
using System.Collections.Generic;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Axion.Core;

namespace Axion.Rendering;

/// <summary>GLSL shader program. Vertex + fragment, with a small uniform cache.</summary>
public sealed class Shader : IDisposable
{
    public int Handle { get; private set; }
    private readonly Dictionary<string, int> _uniformCache = new();

    public Shader(string vertSrc, string fragSrc)
    {
        int vs = Compile(ShaderType.VertexShader,   vertSrc);
        int fs = Compile(ShaderType.FragmentShader, fragSrc);
        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vs);
        GL.AttachShader(Handle, fs);
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0) { var info = GL.GetProgramInfoLog(Handle); throw new Exception("Shader link error: " + info); }
        GL.DetachShader(Handle, vs); GL.DetachShader(Handle, fs);
        GL.DeleteShader(vs); GL.DeleteShader(fs);
    }

    private static int Compile(ShaderType ty, string src)
    {
        int s = GL.CreateShader(ty);
        GL.ShaderSource(s, src); GL.CompileShader(s);
        GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) { var info = GL.GetShaderInfoLog(s); GL.DeleteShader(s); throw new Exception($"{ty} compile error: {info}\n{src}"); }
        return s;
    }

    public void Use() => GL.UseProgram(Handle);

    private int Loc(string name)
    {
        if (_uniformCache.TryGetValue(name, out var l)) return l;
        l = GL.GetUniformLocation(Handle, name);
        _uniformCache[name] = l;
        return l;
    }

    public void Set(string n, int    v) => GL.Uniform1(Loc(n), v);
    public void Set(string n, float  v) => GL.Uniform1(Loc(n), v);
    public void Set(string n, Vector3 v) => GL.Uniform3(Loc(n), v.X, v.Y, v.Z);
    public void Set(string n, Vector4 v) => GL.Uniform4(Loc(n), v.X, v.Y, v.Z, v.W);
    public unsafe void Set(string n, Matrix4x4 m)
    {
        var arr = new[] { m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
                          m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44 };
        GL.UniformMatrix4(Loc(n), 1, false, arr);
    }

    public void Dispose() { if (Handle != 0) { GL.DeleteProgram(Handle); Handle = 0; } }

    public static Shader Default()
    {
        const string V = """
            #version 330 core
            layout(location=0) in vec3 aPos;
            layout(location=1) in vec3 aNormal;
            layout(location=2) in vec2 aUV;
            uniform mat4 uModel;
            uniform mat4 uView;
            uniform mat4 uProj;
            out vec3 vNormal;
            out vec3 vWorldPos;
            out vec2 vUV;
            void main() {
                vec4 wp = uModel * vec4(aPos, 1.0);
                vWorldPos = wp.xyz;
                vNormal = mat3(transpose(inverse(uModel))) * aNormal;
                vUV = aUV;
                gl_Position = uProj * uView * wp;
            }
            """;
        const string F = """
            #version 330 core
            in vec3 vNormal; in vec3 vWorldPos; in vec2 vUV;
            uniform vec3 uColor;
            uniform vec3 uLightDir;
            uniform vec3 uLightColor;
            uniform vec3 uAmbient;
            out vec4 FragColor;
            void main() {
                vec3 N = normalize(vNormal);
                vec3 L = normalize(-uLightDir);
                float diff = max(dot(N, L), 0.0);
                vec3 col = uColor * (uAmbient + uLightColor * diff);
                FragColor = vec4(col, 1.0);
            }
            """;
        return new Shader(V, F);
    }
}
