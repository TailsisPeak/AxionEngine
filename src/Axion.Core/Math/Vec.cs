using System.Numerics;

namespace Axion.Core;

/// <summary>Convenience constructors and helpers for vectors / quaternions.</summary>
public static class Vec
{
    public static readonly Vector3 Zero    = Vector3.Zero;
    public static readonly Vector3 One     = Vector3.One;
    public static readonly Vector3 Up      = Vector3.UnitY;
    public static readonly Vector3 Down    = -Vector3.UnitY;
    public static readonly Vector3 Right   = Vector3.UnitX;
    public static readonly Vector3 Left    = -Vector3.UnitX;
    public static readonly Vector3 Forward = -Vector3.UnitZ;   // OpenGL: -Z is forward
    public static readonly Vector3 Back    = Vector3.UnitZ;

    public static Vector3 V(float x, float y, float z)        => new(x, y, z);
    public static Vector2 V(float x, float y)                  => new(x, y);
    public static Vector4 V(float x, float y, float z, float w) => new(x, y, z, w);

    public static Quaternion Euler(float pitchDeg, float yawDeg, float rollDeg)
        => Quaternion.CreateFromYawPitchRoll(yawDeg * Mathf.Deg2Rad, pitchDeg * Mathf.Deg2Rad, rollDeg * Mathf.Deg2Rad);

    public static Quaternion Euler(Vector3 eulerDeg) => Euler(eulerDeg.X, eulerDeg.Y, eulerDeg.Z);

    public static Vector3 ToEulerDeg(this Quaternion q)
    {
        // ZYX intrinsic
        float sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
        float cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        float roll  = Mathf.Atan2(sinr_cosp, cosr_cosp);
        float sinp  = 2 * (q.W * q.Y - q.Z * q.X);
        float pitch = Mathf.Abs(sinp) >= 1 ? Mathf.Sign(sinp) * Mathf.PI / 2 : Mathf.Asin(sinp);
        float siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
        float cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        float yaw  = Mathf.Atan2(siny_cosp, cosy_cosp);
        return new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
    }

    public static Vector3 RotatedBy(this Vector3 v, Quaternion q) => Vector3.Transform(v, q);

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, Mathf.Clamp01(t));
    public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => Quaternion.Slerp(a, b, Mathf.Clamp01(t));
}
