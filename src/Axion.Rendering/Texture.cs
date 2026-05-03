using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using Axion.Core;

namespace Axion.Rendering;

public sealed class Texture : IDisposable
{
    public int  Handle { get; private set; }
    public int  Width  { get; }
    public int  Height { get; }

    private Texture(int handle, int w, int h) { Handle = handle; Width = w; Height = h; }

    public static Texture FromFile(string path)
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
        using var fs = File.OpenRead(path);
        var img = ImageResult.FromStream(fs, ColorComponents.RedGreenBlueAlpha);
        return FromBytes(img.Data, img.Width, img.Height);
    }

    public static Texture FromBytes(byte[] rgba, int w, int h)
    {
        int handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, handle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, w, h, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        return new Texture(handle, w, h);
    }

    public static Texture White()
    {
        var px = new byte[] { 255, 255, 255, 255 };
        return FromBytes(px, 1, 1);
    }

    public void Bind(int unit = 0) { GL.ActiveTexture(TextureUnit.Texture0 + unit); GL.BindTexture(TextureTarget.Texture2D, Handle); }

    public void Dispose() { if (Handle != 0) { GL.DeleteTexture(Handle); Handle = 0; } }

    /// <summary>Register Texture with the AssetDatabase so scripts can do AssetDatabase.Load&lt;Texture&gt;("foo.png").</summary>
    public static void RegisterLoader() => AssetDatabase.RegisterLoader<Texture>(FromFile);
}
