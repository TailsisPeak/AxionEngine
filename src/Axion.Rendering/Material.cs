using System.Numerics;

namespace Axion.Rendering;

/// <summary>A simple PBR-lite material: diffuse colour + optional albedo texture.</summary>
public sealed class Material
{
    public Vector3 Color   { get; set; } = new(1, 1, 1);
    public Texture? Albedo { get; set; }
    public Shader? Shader  { get; set; }      // null = use renderer's default

    public Material() { }
    public Material(Vector3 color) { Color = color; }
    public Material(float r, float g, float b) { Color = new Vector3(r, g, b); }

    public static Material Default => new();
}
