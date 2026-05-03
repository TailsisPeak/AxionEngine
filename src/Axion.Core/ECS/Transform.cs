using System.Collections.Generic;
using System.Numerics;

namespace Axion.Core;

/// <summary>
/// World-space + local-space transform. Every GameObject has exactly one Transform.
/// Forms the scene hierarchy (parent/child).
/// </summary>
public sealed class Transform : Component
{
    private Vector3    _localPos   = Vector3.Zero;
    private Quaternion _localRot   = Quaternion.Identity;
    private Vector3    _localScale = Vector3.One;

    public Vector3    LocalPosition { get => _localPos; set { _localPos = value; Dirty(); } }
    public Quaternion LocalRotation { get => _localRot; set { _localRot = value; Dirty(); } }
    public Vector3    LocalScale    { get => _localScale; set { _localScale = value; Dirty(); } }

    public Vector3 LocalEulerAngles
    {
        get => _localRot.ToEulerDeg();
        set => LocalRotation = Vec.Euler(value);
    }

    // ── Hierarchy ────────────────────────────────────────────────────────────
    private Transform? _parent;
    private readonly List<Transform> _children = new();
    public Transform? Parent => _parent;
    public IReadOnlyList<Transform> Children => _children;
    public int ChildCount => _children.Count;

    public void SetParent(Transform? newParent, bool keepWorldPosition = true)
    {
        if (_parent == newParent) return;
        var worldPos = Position; var worldRot = Rotation;
        _parent?._children.Remove(this);
        _parent = newParent;
        _parent?._children.Add(this);
        if (keepWorldPosition) { Position = worldPos; Rotation = worldRot; }
        Dirty();
    }

    // ── Cached world matrix ──────────────────────────────────────────────────
    private Matrix4x4 _cachedWorld = Matrix4x4.Identity;
    private bool _dirty = true;
    private void Dirty() { _dirty = true; foreach (var c in _children) c.Dirty(); }

    public Matrix4x4 LocalToWorld
    {
        get
        {
            if (_dirty)
            {
                var local = Matrix4x4.CreateScale(_localScale)
                          * Matrix4x4.CreateFromQuaternion(_localRot)
                          * Matrix4x4.CreateTranslation(_localPos);
                _cachedWorld = _parent != null ? local * _parent.LocalToWorld : local;
                _dirty = false;
            }
            return _cachedWorld;
        }
    }

    public Matrix4x4 WorldToLocal
    {
        get { Matrix4x4.Invert(LocalToWorld, out var inv); return inv; }
    }

    public Vector3 Position
    {
        get => Vector3.Transform(Vector3.Zero, LocalToWorld);
        set
        {
            if (_parent == null) { _localPos = value; Dirty(); }
            else _localPos = Vector3.Transform(value, _parent.WorldToLocal);
            Dirty();
        }
    }

    public Quaternion Rotation
    {
        get => _parent == null ? _localRot : _parent.Rotation * _localRot;
        set
        {
            if (_parent == null) _localRot = value;
            else _localRot = Quaternion.Inverse(_parent.Rotation) * value;
            Dirty();
        }
    }

    public Vector3 EulerAngles
    {
        get => Rotation.ToEulerDeg();
        set => Rotation = Vec.Euler(value);
    }

    public Vector3 Forward => Vector3.Transform(Vec.Forward, Rotation);
    public Vector3 Right   => Vector3.Transform(Vec.Right,   Rotation);
    public Vector3 Up      => Vector3.Transform(Vec.Up,      Rotation);

    public void Translate(Vector3 delta, bool worldSpace = true)
    {
        if (worldSpace) Position += delta;
        else            LocalPosition += delta;
    }

    public void Rotate(Vector3 eulerDelta) => LocalRotation = Vec.Euler(eulerDelta) * _localRot;

    public void LookAt(Vector3 target, Vector3? up = null)
    {
        var u = up ?? Vec.Up;
        var view = Matrix4x4.CreateLookAt(Position, target, u);
        Matrix4x4.Invert(view, out var world);
        Rotation = Quaternion.CreateFromRotationMatrix(world);
    }
}
