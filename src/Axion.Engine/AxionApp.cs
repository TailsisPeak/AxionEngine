using System;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using Axion.Core;
using Axion.Rendering;
using Axion.Physics;
using Axion.Scripting;

namespace Axion.Engine;

/// <summary>
/// The Axion runtime host — top-level orchestrator that wires together window, renderer,
/// physics, scripting host, and active scene. One instance per running game.
/// </summary>
public sealed class AxionApp : IDisposable
{
    public static AxionApp? Instance { get; private set; }

    public Window         Window   { get; }
    public Renderer       Renderer { get; private set; } = null!;
    public PhysicsWorld   Physics  { get; }
    public AudioSystem    Audio    { get; }
    public ScriptHost     Scripts  { get; }
    public Scene          Scene    { get; private set; }

    /// <summary>If set, the editor (or any host) overrides the scene's main camera with these matrices.</summary>
    public System.Func<(Matrix4x4 view, Matrix4x4 proj)>? CameraOverride { get; set; }

    /// <summary>If false, scripts/behaviors and physics do not tick. The scene still renders.</summary>
    public bool Simulating { get; set; } = true;

    private float _physAccum;
    private bool  _disposed;

    public AxionApp(string title = "Axion", int width = 1280, int height = 720, Scene? scene = null)
    {
        Instance = this;
        Texture.RegisterLoader();
        Window   = new Window(title, width, height);
        Window.OnLoadCallback   += () => { Renderer_Init(); };
        Window.OnRenderCallback += dt  => Render();
        Window.OnUpdateCallback += dt  => Update(dt);
        Window.OnResizeCallback += (w, h) => OnResize(w, h);
        Physics  = new PhysicsWorld();
        Audio    = new AudioSystem();
        Scripts  = new ScriptHost();
        Scene    = scene ?? new Scene("Main");
    }

    private void Renderer_Init()
    {
        // Rendering ctor must run after the GL context exists.
        Renderer = new Renderer();
        AwakeAllBehaviorsInScene();
    }

    public void LoadScene(Scene scene)   { Scene = scene; AwakeAllBehaviorsInScene(); }
    public void LoadSceneFile(string p)  { LoadScene(SceneSerializer.Load(p)); }

    /// <summary>Run the main loop until the window is closed.</summary>
    public void Run() => Window.Run();

    private void OnResize(int w, int h)
    {
        var cam = ActiveCamera();
        if (cam != null) cam.Camera.Aspect = w / (float)h;
    }

    private void AwakeAllBehaviorsInScene()
    {
        foreach (var b in Scene.AllOfType<Behavior>()) b.OnAttach();
        // Sync rigidbodies into the simulation
        foreach (var rb in Scene.AllOfType<RigidbodyComponent>()) RegisterRigidbody(rb);
    }

    private void Update(float dt)
    {
        Time.Tick();
        if (!Simulating) return;
        CoroutineRunner.Tick();

        // Behaviors: Start once, then Update + LateUpdate
        foreach (var b in Scene.AllOfType<Behavior>())
            if (b.ActiveInHierarchy) b.RunStart();
        foreach (var b in Scene.AllOfType<Behavior>())
            if (b.ActiveInHierarchy) b.RunUpdate();

        // Fixed-step physics
        _physAccum += Time.DeltaTime;
        while (_physAccum >= Time.FixedDelta)
        {
            foreach (var b in Scene.AllOfType<Behavior>())
                if (b.ActiveInHierarchy) b.RunFixedUpdate();
            Physics.Step(Time.FixedDelta);
            _physAccum -= Time.FixedDelta;
        }
        SyncPhysicsToTransforms();

        foreach (var b in Scene.AllOfType<Behavior>())
            if (b.ActiveInHierarchy) b.RunLateUpdate();

        // Auto-play audio sources
        foreach (var a in Scene.AllOfType<AudioSourceComponent>())
            if (a.PlayOnStart && a.AlSource == 0) a.Play();
    }

    private void SyncPhysicsToTransforms()
    {
        foreach (var rb in Scene.AllOfType<RigidbodyComponent>())
        {
            if (!rb.Registered) continue;
            var body = Physics.Simulation.Bodies[rb.BodyHandle];
            rb.Transform.Position = body.Pose.Position;
            rb.Transform.Rotation = body.Pose.Orientation;
        }
    }

    private void RegisterRigidbody(RigidbodyComponent rb)
    {
        if (rb.Registered) return;
        TypedIndex shape = rb.Shape == ColliderShape.Sphere
            ? Physics.Simulation.Shapes.Add(new Sphere(rb.Size.X * 0.5f))
            : Physics.Simulation.Shapes.Add(new Box(rb.Size.X, rb.Size.Y, rb.Size.Z));
        var pose = new RigidPose(rb.Transform.Position, rb.Transform.Rotation);
        if (rb.IsKinematic || rb.Mass <= 0)
        {
            rb.BodyHandle = Physics.Simulation.Bodies.Add(BodyDescription.CreateKinematic(pose, new CollidableDescription(shape, 0.1f), default));
        }
        else
        {
            BodyInertia inertia;
            if (rb.Shape == ColliderShape.Sphere) inertia = new Sphere(rb.Size.X * 0.5f).ComputeInertia(rb.Mass);
            else                                  inertia = new Box(rb.Size.X, rb.Size.Y, rb.Size.Z).ComputeInertia(rb.Mass);
            rb.BodyHandle = Physics.Simulation.Bodies.Add(BodyDescription.CreateDynamic(pose, inertia, new CollidableDescription(shape, 0.1f), default));
        }
        rb.Registered = true;
    }

    private CameraComponent? ActiveCamera()
    {
        CameraComponent? best = null;
        foreach (var c in Scene.AllOfType<CameraComponent>())
            if (c.ActiveInHierarchy && (best == null || (c.IsMain && !best.IsMain))) best = c;
        return best;
    }

    private void Render()
    {
        if (Renderer == null) return;
        Renderer.BeginFrame(Window.ClientSize.X, Window.ClientSize.Y);

        Matrix4x4 view, proj;
        if (CameraOverride != null)
        {
            (view, proj) = CameraOverride();
        }
        else
        {
            var cam = ActiveCamera();
            if (cam == null) return;
            view = cam.GetView();
            proj = cam.GetProjection();
        }

        // Update lighting from the brightest directional light, if any.
        foreach (var lt in Scene.AllOfType<LightComponent>())
            if (lt.Type == LightType.Directional && lt.ActiveInHierarchy)
            {
                Renderer.LightDir   = lt.Transform.Forward;
                Renderer.LightColor = lt.Color * lt.Intensity;
                break;
            }

        foreach (var mr in Scene.AllOfType<MeshRendererComponent>())
        {
            if (!mr.ActiveInHierarchy || mr.Mesh == null) continue;
            Renderer.Draw(mr.Mesh, mr.Material, mr.Transform.LocalToWorld, view, proj);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Renderer?.Dispose();
        Physics.Dispose();
        Audio.Dispose();
        Window.Dispose();
        Instance = null;
    }
}
