using System.Numerics;
using Axion.Core;

namespace Axion.Rendering;

/// <summary>A perspective camera. Component form lives in Axion.Engine; this is the math.</summary>
public sealed class Camera
{
    public float FieldOfViewDeg { get; set; } = 60f;
    public float Near           { get; set; } = 0.1f;
    public float Far            { get; set; } = 1000f;
    public float Aspect         { get; set; } = 16f / 9f;

    public Matrix4x4 GetView(Vector3 position, Quaternion rotation)
    {
        var fwd = Vector3.Transform(Vec.Forward, rotation);
        var up  = Vector3.Transform(Vec.Up,      rotation);
        return Matrix4x4.CreateLookAt(position, position + fwd, up);
    }

    public Matrix4x4 GetProjection() =>
        Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewDeg * Mathf.Deg2Rad, Aspect, Near, Far);
}
