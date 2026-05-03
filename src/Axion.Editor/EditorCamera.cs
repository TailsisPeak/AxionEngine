using System;
using System.Numerics;
using Axion.Core;
using Axion.Engine;

namespace Axion.Editor;

/// <summary>
/// Free-fly editor camera, used in Scene-view mode. Independent of any GameObject.
/// Controls (active when right mouse button is held over the viewport):
///   • RMB drag → look around (mouselook)
///   • W A S D  → move forward / left / back / right
///   • Q / E    → move down / up
///   • Shift    → fast move
///   • Mouse wheel → adjust FOV
/// </summary>
public sealed class EditorCamera
{
    public Vector3 Position { get; set; } = new Vector3(4, 3, 6);
    public float   Yaw      { get; set; } = -135f * Mathf.Deg2Rad; // looking back at origin
    public float   Pitch    { get; set; } = -25f  * Mathf.Deg2Rad;
    public float   Fov      { get; set; } = 60f;
    public float   Near     { get; set; } = 0.05f;
    public float   Far      { get; set; } = 2000f;

    public float MoveSpeed       { get; set; } = 5f;
    public float FastMultiplier  { get; set; } = 3f;
    public float LookSensitivity { get; set; } = 0.15f;

    private bool _looking;

    public Vector3 Forward
    {
        get
        {
            var cy = MathF.Cos(Yaw);   var sy = MathF.Sin(Yaw);
            var cp = MathF.Cos(Pitch); var sp = MathF.Sin(Pitch);
            return Vector3.Normalize(new Vector3(cy * cp, sp, sy * cp));
        }
    }
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
    public Vector3 Up    => Vector3.Normalize(Vector3.Cross(Right, Forward));

    /// <summary>Update from input. Pass dt and whether the cursor is over an ImGui window
    /// (in which case we ignore camera input so panels stay interactive).</summary>
    public void Update(float dt, bool inputCaptured)
    {
        // Right-mouse-look
        bool rmb = Input.GetKey(Key.MouseRight);
        if (rmb && !inputCaptured) _looking = true;
        if (!rmb)                  _looking = false;

        if (_looking)
        {
            var d = Input.MouseDelta;
            Yaw   += d.X * LookSensitivity * Mathf.Deg2Rad;
            Pitch -= d.Y * LookSensitivity * Mathf.Deg2Rad;
            Pitch = Math.Clamp(Pitch, -1.55f, 1.55f);

            float speed = MoveSpeed * (Input.GetKey(Key.LShift) || Input.GetKey(Key.RShift) ? FastMultiplier : 1f);
            float dist = speed * dt;
            if (Input.GetKey(Key.W)) Position += Forward * dist;
            if (Input.GetKey(Key.S)) Position -= Forward * dist;
            if (Input.GetKey(Key.D)) Position += Right   * dist;
            if (Input.GetKey(Key.A)) Position -= Right   * dist;
            if (Input.GetKey(Key.E)) Position += Vector3.UnitY * dist;
            if (Input.GetKey(Key.Q)) Position -= Vector3.UnitY * dist;
        }

        if (!inputCaptured && MathF.Abs(Input.ScrollDelta) > 0.0001f)
        {
            Fov = Math.Clamp(Fov - Input.ScrollDelta * 2f, 15f, 110f);
        }
    }

    public (Matrix4x4 view, Matrix4x4 proj) GetMatrices(float aspect)
    {
        var view = Matrix4x4.CreateLookAt(Position, Position + Forward, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(Fov * Mathf.Deg2Rad, aspect, Near, Far);
        return (view, proj);
    }

    public void FrameOrigin()
    {
        Position = new Vector3(4, 3, 6);
        Yaw   = -135f * Mathf.Deg2Rad;
        Pitch = -25f  * Mathf.Deg2Rad;
    }
}
