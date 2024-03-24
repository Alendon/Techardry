using System;
using System.Numerics;
using Autofac;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuPhysics.CollisionDetection.SweepTasks;
using BepuPhysics.Constraints;
using MintyCore;
using MintyCore.ECS;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Modding;
using MintyCore.Network;
using MintyCore.Physics;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Blocks;
using Techardry.Identifications;
using Techardry.Lib.FastNoseLite;
using Techardry.Render;
using Techardry.Voxels;

namespace Techardry.World;

[RegisterWorld("techardry_world")]
public class TechardryWorld : IWorld
{
    private SystemManager? _systemManager;
    private EntityManager? _entityManager;
    private PhysicsWorld? _physicsWorld;

    /// <summary>
    ///     Whether or not the systems are executing now
    /// </summary>
    public bool IsExecuting { get; private set; }

    /// <summary>
    ///     The SystemManager of the <see cref="World" />
    /// </summary>
    public SystemManager SystemManager => _systemManager ?? throw new Exception("Object is Disposed");

    /// <summary>
    ///     The EntityManager of the <see cref="World" />
    /// </summary>
    public IEntityManager EntityManager => _entityManager ?? throw new Exception("Object is Disposed");

    /// <summary>
    ///     The <see cref="PhysicsWorld" /> of the <see cref="World" />
    /// </summary>
    public IPhysicsWorld PhysicsWorld => _physicsWorld ?? throw new Exception("Object is Disposed");

    public Identification Identification => WorldIDs.TechardryWorld;

    public ChunkManager ChunkManager { get; }
    public WorldGenerator WorldGenerator { get; }

    /// <summary>
    ///     Whether or not this world is a server world.
    /// </summary>
    public bool IsServerWorld { get; init; }

    public TechardryWorld(INetworkHandler networkHandler, IPlayerHandler playerHandler,
        IArchetypeManager archetypeManager, IComponentManager componentManager, IModManager modManager,
        ITextureAtlasHandler textureAtlasHandler, IBlockHandler blockHandler, IInputDataManager? renderDataManager,
        bool isServerWorld)
    {
        IsServerWorld = isServerWorld;
        
        ChunkManager = new ChunkManager(this, networkHandler, playerHandler, textureAtlasHandler, blockHandler,renderDataManager);

        _entityManager = new EntityManager(this, archetypeManager, playerHandler, networkHandler);
        _systemManager = new SystemManager(this, componentManager, modManager);

        var noise = new FastNoiseLite(5);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.02f);

        WorldGenerator = new WorldGenerator(this, new WorldGeneratorSettings()
        {
            Noise = noise
        }, playerHandler, networkHandler, blockHandler, textureAtlasHandler);

        var narrowPhase = new MintyNarrowPhaseCallback(new SpringSettings(30f, 1f), 1f, 2f);

        var poseIntegrator = new MintyPoseIntegratorCallback(new Vector3(0, -10, 0), 0.03f, 0.03f);
        var solveDescription = new SolveDescription(8, 8);

        _physicsWorld = MintyCore.Physics.PhysicsWorld.Create(narrowPhase, poseIntegrator, solveDescription);


        RegisterPhysicsExtensions();
    }

    private void RegisterPhysicsExtensions()
    {
        var collisionTaskRegistry = PhysicsWorld.Simulation.NarrowPhase.CollisionTaskRegistry;

        collisionTaskRegistry.Register(
            new ConvexCompoundCollisionTask<Sphere, VoxelCollider,
                ConvexCompoundOverlapFinder<Sphere, SphereWide, VoxelCollider>, ConvexVoxelsContinuations,
                NonconvexReduction>());
        collisionTaskRegistry.Register(
            new ConvexCompoundCollisionTask<Capsule, VoxelCollider,
                ConvexCompoundOverlapFinder<Capsule, CapsuleWide, VoxelCollider>, ConvexVoxelsContinuations,
                NonconvexReduction>());
        collisionTaskRegistry.Register(
            new ConvexCompoundCollisionTask<Box, VoxelCollider, ConvexCompoundOverlapFinder<Box, BoxWide, VoxelCollider>
                , ConvexVoxelsContinuations, NonconvexReduction>());
        collisionTaskRegistry.Register(
            new ConvexCompoundCollisionTask<Triangle, VoxelCollider,
                ConvexCompoundOverlapFinder<Triangle, TriangleWide, VoxelCollider>, ConvexVoxelsContinuations,
                NonconvexReduction>());
        collisionTaskRegistry.Register(
            new ConvexCompoundCollisionTask<Cylinder, VoxelCollider,
                ConvexCompoundOverlapFinder<Cylinder, CylinderWide, VoxelCollider>, ConvexVoxelsContinuations,
                NonconvexReduction>());
        collisionTaskRegistry.Register(
            new ConvexCompoundCollisionTask<ConvexHull, VoxelCollider,
                ConvexCompoundOverlapFinder<ConvexHull, ConvexHullWide, VoxelCollider>, ConvexVoxelsContinuations,
                NonconvexReduction>());

        collisionTaskRegistry.Register(
            new CompoundPairCollisionTask<Compound, VoxelCollider, CompoundPairOverlapFinder<Compound, VoxelCollider>,
                CompoundVoxelsContinuations<Compound>, NonconvexReduction>());
        collisionTaskRegistry.Register(
            new CompoundPairCollisionTask<BigCompound, VoxelCollider,
                CompoundPairOverlapFinder<BigCompound, VoxelCollider>, CompoundVoxelsContinuations<BigCompound>,
                NonconvexReduction>());

        var sweepTaskRegistry = PhysicsWorld.Simulation.NarrowPhase.SweepTaskRegistry;

        sweepTaskRegistry.Register(
            new ConvexHomogeneousCompoundSweepTask<Sphere, SphereWide, VoxelCollider, Box, BoxWide,
                ConvexCompoundSweepOverlapFinder<Sphere, VoxelCollider>>());
        sweepTaskRegistry.Register(
            new ConvexHomogeneousCompoundSweepTask<Capsule, CapsuleWide, VoxelCollider, Box, BoxWide,
                ConvexCompoundSweepOverlapFinder<Capsule, VoxelCollider>>());
        sweepTaskRegistry.Register(
            new ConvexHomogeneousCompoundSweepTask<Box, BoxWide, VoxelCollider, Box, BoxWide,
                ConvexCompoundSweepOverlapFinder<Box, VoxelCollider>>());
        sweepTaskRegistry.Register(
            new ConvexHomogeneousCompoundSweepTask<Triangle, TriangleWide, VoxelCollider, Box, BoxWide,
                ConvexCompoundSweepOverlapFinder<Triangle, VoxelCollider>>());
        sweepTaskRegistry.Register(
            new ConvexHomogeneousCompoundSweepTask<Cylinder, CylinderWide, VoxelCollider, Box, BoxWide,
                ConvexCompoundSweepOverlapFinder<Cylinder, VoxelCollider>>());
        sweepTaskRegistry.Register(
            new ConvexHomogeneousCompoundSweepTask<ConvexHull, ConvexHullWide, VoxelCollider, Box, BoxWide,
                ConvexCompoundSweepOverlapFinder<ConvexHull, VoxelCollider>>());

        sweepTaskRegistry.Register(
            new CompoundHomogeneousCompoundSweepTask<Compound, VoxelCollider, Box, BoxWide,
                CompoundPairSweepOverlapFinder<Compound, VoxelCollider>>());
        sweepTaskRegistry.Register(
            new CompoundHomogeneousCompoundSweepTask<BigCompound, VoxelCollider, Box, BoxWide,
                CompoundPairSweepOverlapFinder<BigCompound, VoxelCollider>>());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ChunkManager.Dispose();

        GC.SuppressFinalize(this);
        EntityManager.Dispose();
        SystemManager.Dispose();
        PhysicsWorld.Dispose();

        _entityManager = null;
        _physicsWorld = null;
        _systemManager = null;
    }

    /// <summary>
    ///     Simulate one <see cref="World" /> tick
    /// </summary>
    public void Tick()
    {
        IsExecuting = true;
        SystemManager.Execute();
        IsExecuting = false;

        ChunkManager.Update();
    }
}