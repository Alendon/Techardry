using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace Techardry.World;

internal readonly struct MintyNarrowPhaseCallback : INarrowPhaseCallbacks
{
    private readonly SpringSettings _contactSpringiness;
    private readonly float _frictionCoefficient;
    private readonly float _maximumRecoveryVelocity;

    public MintyNarrowPhaseCallback(SpringSettings contactSpringiness, float frictionCoefficient,
        float maximumRecoveryVelocity)
    {
        _contactSpringiness = contactSpringiness;
        _frictionCoefficient = frictionCoefficient;
        _maximumRecoveryVelocity = maximumRecoveryVelocity;
    }

    public void Initialize(Simulation simulation)
    {
    }

    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b,
        ref float speculativeMargin)
    {
        return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    }

    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair,
        ref TManifold manifold,
        out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = _frictionCoefficient;
        pairMaterial.MaximumRecoveryVelocity = _maximumRecoveryVelocity;
        pairMaterial.SpringSettings = _contactSpringiness;
        return true;
    }


    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB,
        ref ConvexContactManifold manifold)
    {
        return true;
    }

    public void Dispose()
    {
    }
}