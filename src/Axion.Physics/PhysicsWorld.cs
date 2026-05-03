using System;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;

namespace Axion.Physics;

/// <summary>Wraps a BepuPhysics2 Simulation. One per scene.</summary>
public sealed class PhysicsWorld : IDisposable
{
    public Simulation       Simulation { get; }
    public BufferPool       BufferPool { get; }
    private readonly ThreadDispatcher _dispatcher;

    public Vector3 Gravity { get; set; } = new(0, -9.81f, 0);

    public PhysicsWorld()
    {
        BufferPool = new BufferPool();
        _dispatcher = new ThreadDispatcher(Math.Max(1, Environment.ProcessorCount - 2));
        Simulation = Simulation.Create(BufferPool,
            new DefaultNarrowPhaseCallbacks(new SpringSettings(30, 1)),
            new DefaultPoseIntegratorCallbacks(Gravity),
            new SolveDescription(8, 1));
    }

    public void Step(float dt)
    {
        Simulation.Timestep(dt, _dispatcher);
    }

    public void Dispose()
    {
        Simulation.Dispose();
        BufferPool.Clear();
        _dispatcher.Dispose();
    }
}

// ─── Default callbacks ──────────────────────────────────────────────────────
public struct DefaultNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public SpringSettings ContactSpringiness;
    public DefaultNarrowPhaseCallbacks(SpringSettings springiness) { ContactSpringiness = springiness; }

    public void Initialize(Simulation simulation) { }
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) => a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;
    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pm) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pm.FrictionCoefficient = 1f;
        pm.MaximumRecoveryVelocity = 2f;
        pm.SpringSettings = ContactSpringiness;
        return true;
    }
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;
    public void Dispose() { }
}

public struct DefaultPoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public Vector3 Gravity;
    public Vector3Wide GravityWideDt;
    private float _dt;

    public DefaultPoseIntegratorCallbacks(Vector3 gravity) { Gravity = gravity; GravityWideDt = default; _dt = 0; }

    public void Initialize(Simulation simulation) { }
    public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public bool AllowSubstepsForUnconstrainedBodies => false;
    public bool IntegrateVelocityForKinematics => false;

    public void PrepareForIntegration(float dt)
    {
        _dt = dt;
        GravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
    }

    public void IntegrateVelocity(System.Numerics.Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, System.Numerics.Vector<int> integrationMask, int workerIndex, System.Numerics.Vector<float> dt, ref BodyVelocityWide velocity)
    {
        velocity.Linear += GravityWideDt;
    }
}
