using System;
using System.Collections.Generic;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace Axion.Rendering;

/// <summary>Vertex format: position(3), normal(3), uv(2) = 8 floats, stride 32 bytes.</summary>
public sealed class Mesh : IDisposable
{
    public int Vao { get; private set; }
    public int Vbo { get; private set; }
    public int Ebo { get; private set; }
    public int IndexCount { get; private set; }

    public Mesh(float[] vertices, uint[] indices)
    {
        Vao = GL.GenVertexArray();
        Vbo = GL.GenBuffer();
        Ebo = GL.GenBuffer();

        GL.BindVertexArray(Vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        const int stride = 8 * sizeof(float);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);                  // pos
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));  // nrm
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));  // uv
        GL.EnableVertexAttribArray(2);

        GL.BindVertexArray(0);
        IndexCount = indices.Length;
    }

    public void Draw()
    {
        GL.BindVertexArray(Vao);
        GL.DrawElements(PrimitiveType.Triangles, IndexCount, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (Vao != 0) GL.DeleteVertexArray(Vao);
        if (Vbo != 0) GL.DeleteBuffer(Vbo);
        if (Ebo != 0) GL.DeleteBuffer(Ebo);
        Vao = Vbo = Ebo = 0;
    }

    // ── Primitive builders ────────────────────────────────────────────────────
    public static Mesh Cube()
    {
        // 24 verts (one per corner-per-face for correct normals/UVs)
        float[] v = {
            // +X
             1,-1,-1,  1,0,0,  0,0,
             1, 1,-1,  1,0,0,  1,0,
             1, 1, 1,  1,0,0,  1,1,
             1,-1, 1,  1,0,0,  0,1,
            // -X
            -1,-1, 1, -1,0,0,  0,0,
            -1, 1, 1, -1,0,0,  1,0,
            -1, 1,-1, -1,0,0,  1,1,
            -1,-1,-1, -1,0,0,  0,1,
            // +Y
            -1, 1,-1,  0,1,0,  0,0,
            -1, 1, 1,  0,1,0,  1,0,
             1, 1, 1,  0,1,0,  1,1,
             1, 1,-1,  0,1,0,  0,1,
            // -Y
            -1,-1, 1,  0,-1,0, 0,0,
            -1,-1,-1,  0,-1,0, 1,0,
             1,-1,-1,  0,-1,0, 1,1,
             1,-1, 1,  0,-1,0, 0,1,
            // +Z
            -1,-1, 1,  0,0,1,  0,0,
             1,-1, 1,  0,0,1,  1,0,
             1, 1, 1,  0,0,1,  1,1,
            -1, 1, 1,  0,0,1,  0,1,
            // -Z
             1,-1,-1,  0,0,-1, 0,0,
            -1,-1,-1,  0,0,-1, 1,0,
            -1, 1,-1,  0,0,-1, 1,1,
             1, 1,-1,  0,0,-1, 0,1,
        };
        var i = new uint[36];
        for (uint f = 0; f < 6; f++)
        {
            uint b = f * 4;
            i[f*6+0]=b+0; i[f*6+1]=b+1; i[f*6+2]=b+2;
            i[f*6+3]=b+0; i[f*6+4]=b+2; i[f*6+5]=b+3;
        }
        return new Mesh(v, i);
    }

    public static Mesh Quad()
    {
        float[] v = {
            -1,0,-1, 0,1,0, 0,0,
             1,0,-1, 0,1,0, 1,0,
             1,0, 1, 0,1,0, 1,1,
            -1,0, 1, 0,1,0, 0,1,
        };
        uint[] i = { 0,1,2, 0,2,3 };
        return new Mesh(v, i);
    }

    public static Mesh Sphere(int segments = 16, int rings = 16)
    {
        var verts = new List<float>();
        var idx   = new List<uint>();
        for (int y = 0; y <= rings; y++)
        {
            float v = (float)y / rings;
            float phi = v * MathF.PI;
            for (int x = 0; x <= segments; x++)
            {
                float u = (float)x / segments;
                float theta = u * MathF.PI * 2;
                float px = MathF.Sin(phi) * MathF.Cos(theta);
                float py = MathF.Cos(phi);
                float pz = MathF.Sin(phi) * MathF.Sin(theta);
                verts.AddRange(new[] { px, py, pz, px, py, pz, u, v });
            }
        }
        for (int y = 0; y < rings; y++)
            for (int x = 0; x < segments; x++)
            {
                uint i0 = (uint)(y * (segments + 1) + x);
                uint i1 = i0 + 1;
                uint i2 = i0 + (uint)(segments + 1);
                uint i3 = i2 + 1;
                idx.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
            }
        return new Mesh(verts.ToArray(), idx.ToArray());
    }
}
