using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuPhysics.CollisionDetection.SweepTasks;
using BepuPhysics.Constraints;
using BepuUtilities;
using MintyCore;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Network;
using MintyCore.Physics;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Lib.FastNoseLite;
using Techardry.Networking;
using Techardry.Voxels;
using Int3 = Techardry.Utils.Int3;

namespace Techardry.World;

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
    public EntityManager EntityManager => _entityManager ?? throw new Exception("Object is Disposed");

    /// <summary>
    ///     The <see cref="PhysicsWorld" /> of the <see cref="World" />
    /// </summary>
    public PhysicsWorld PhysicsWorld => _physicsWorld ?? throw new Exception("Object is Disposed");
    
    public Identification Identification => WorldIDs.Default;

    private ConcurrentDictionary<Int3, Chunk> _chunks = new();

    private List<StaticHandle> _bodyHandles = new();
    
    /// <summary>
    ///     Whether or not this world is a server world.
    /// </summary>
    public bool IsServerWorld { get; }

    internal void CreateChunk(Int3 chunkPos)
    {
        if (_chunks.ContainsKey(chunkPos))
        {
            return;
        }

        var chunk = new Chunk(chunkPos, this);
        _chunks.TryAdd(chunkPos, chunk);

        var collider = new VoxelCollider(chunk.Octree);

        _bodyHandles.Add(PhysicsWorld.Simulation.Statics.Add(
            new StaticDescription(new Vector3(chunkPos.X, chunkPos.Y, chunkPos.Z) * VoxelOctree.Dimensions,
                PhysicsWorld.Simulation.Shapes.Add(collider))));

        
    }

    public bool TryGetChunk(Int3 chunkPos, [MaybeNullWhen(false)] out Chunk chunk)
    {
        return _chunks.TryGetValue(chunkPos, out chunk);
    }

    public IEnumerable<Int3> GetLoadedChunks()
    {
        return _chunks.Keys;
    }

    public TechardryWorld(bool isServerWorld)
    {
        IsServerWorld = isServerWorld;
        _entityManager = new EntityManager(this);
        _systemManager = new SystemManager(this);

        var narrowPhase = new MintyNarrowPhaseCallback(new SpringSettings(30f, 1f), 1f, 2f);
        
        var poseIntegrator = new MintyPoseIntegratorCallback(new Vector3(0, -10, 0), 0.03f, 0.03f);
        var solveDescription = new SolveDescription(8, 8);

        _physicsWorld = PhysicsWorld.Create(narrowPhase, poseIntegrator, solveDescription);
        
        SystemManager.SetSystemActive(SystemIDs.RenderInstanced, false);
        RegisterPhysicsExtensions();
        CreateSomeChunks();
    }

    public TechardryWorld(bool isServerWorld, PhysicsWorld physicsWorld)
    {
        IsServerWorld = isServerWorld;
        _entityManager = new EntityManager(this);
        _systemManager = new SystemManager(this);
        _physicsWorld = physicsWorld;
        
        SystemManager.SetSystemActive(SystemIDs.RenderInstanced, false);
        RegisterPhysicsExtensions();
        CreateSomeChunks();
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

    private void CreateSomeChunks()
    {
        Int3 chunkRadius = new()
        {
            X = 5,
            Y = 1,
            Z = 5
        };

        Int3 chunkPos = default;
        
        int seed = 5;

        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.02f);
        
        Stopwatch sw = Stopwatch.StartNew();

        for (int x = -chunkRadius.X; x < chunkRadius.X; x++)
        {
            {
                for (int z = -chunkRadius.Z; z < chunkRadius.Z; z++)
                {
                    chunkPos.X = x;
                    chunkPos.Y = 0;
                    chunkPos.Z = z;




                    CreateChunk(chunkPos);
                    TryGetChunk(chunkPos, out var chunk);
                    FillChunk(chunk!, noise);
                }
            }
        }

        sw.Stop();
        Logger.WriteLog($"Chunk gen took {sw.ElapsedMilliseconds}ms", LogImportance.Debug, "TechardryWorld");

        if (!IsServerWorld) return;

        foreach (var staticHandle in _bodyHandles)
        {
            var staticReference = PhysicsWorld.Simulation.Statics.GetStaticReference(staticHandle);
            var collider = PhysicsWorld.Simulation.Shapes.GetShape<VoxelCollider>(staticReference.Shape.Index);
            
            for (int i = 0; i < collider.ChildCount; i++)
            {
                collider.GetPosedLocalChild(i, out var data, out var pose);
                collider.GetLocalChild(i, out data);

                pose.Position += staticReference.Pose.Position;

                var voxelEntity = EntityManager.CreateEntity(ArchetypeIDs.TestRender, null);
                var render = EntityManager.GetComponent<InstancedRenderAble>(voxelEntity);
            
                render.MaterialMeshCombination = InstancedRenderDataIDs.DualBlock;
                EntityManager.SetComponent(voxelEntity, render);
            
                var scale = EntityManager.GetComponent<Scale>(voxelEntity);
                scale.Value = new Vector3(data.Width, data.Height, data.Length);
                EntityManager.SetComponent(voxelEntity, scale);
            
                var position = EntityManager.GetComponent<Position>(voxelEntity);
                position.Value = pose.Position;
                EntityManager.SetComponent(voxelEntity, position);
            }
        }
        
        
    }

    void FillChunk(Chunk chunk, FastNoiseLite fastNoiseLite)
    {
        for (int x = 0; x < VoxelOctree.Dimensions; x++)
        {
            for (int z = 0; z < VoxelOctree.Dimensions; z++)
            {
                Vector3 pos = new()
                {
                    X = x,
                    Y = 0,
                    Z = z
                };
                for (int y = 0; y < 6; y++)
                {
                    pos.Y = y;
                    chunk.SetBlock(pos, BlockIDs.Stone);
                }

                var noiseValue = fastNoiseLite.GetNoise(x + chunk.Position.X * VoxelOctree.Dimensions,
                    z + chunk.Position.Z * VoxelOctree.Dimensions);
                noiseValue += 0.5f;
                noiseValue /= 0.5f;
                noiseValue *= 6;

                for (int y = 6; y < 7 + noiseValue; y++)
                {
                    pos.Y = y;
                    chunk.SetBlock(pos, BlockIDs.Dirt);
                }
            }
        }
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var chunk in _chunks.Values)
        {
            chunk.Dispose();
        }
        
        GC.SuppressFinalize(this);
        EntityManager.Dispose();
        SystemManager.Dispose();
        PhysicsWorld.Dispose();

        _entityManager = null;
        _physicsWorld = null;
        _systemManager = null;
    }

    private Stopwatch _lastChange = Stopwatch.StartNew();
    private bool dirt;

    /// <summary>
    ///     Simulate one <see cref="World" /> tick
    /// </summary>
    public void Tick()
    {
        IsExecuting = true;
        SystemManager.Execute();
        IsExecuting = false;
        
        if(_lastChange.ElapsedMilliseconds > 1000)
        {
            _lastChange.Restart();
            if (_chunks.TryGetValue(new Int3(0, 0, 0), out var chunk))
            {
                chunk.SetBlock(new Vector3(8,14,8), dirt ? BlockIDs.Stone : BlockIDs.Dirt);
                dirt = !dirt;
            }
        }

        foreach (var chunk in _chunks.Values)
        {
            chunk.Update();
        }
    }

    
}

internal struct MintyPoseIntegratorCallback : IPoseIntegratorCallbacks
{
    private Vector<float> _dtAngularDamping;

    private Vector3Wide _dtGravity;
    private Vector<float> _dtLinearDamping;
    private readonly float _angularDamping;
    private readonly Vector3 _gravity;
    private readonly float _linearDamping;

    public MintyPoseIntegratorCallback(Vector3 gravity, float angularDamping, float linearDamping)
    {
        AngularIntegrationMode = AngularIntegrationMode.Nonconserving;
        IntegrateVelocityForKinematics = false;
        AllowSubstepsForUnconstrainedBodies = false;

        _gravity = gravity;
        _angularDamping = angularDamping;
        _linearDamping = linearDamping;

        _dtGravity = default;
        _dtAngularDamping = default;
        _dtLinearDamping = default;
    }


    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
        _dtGravity = Vector3Wide.Broadcast(_gravity * dt);
        _dtLinearDamping = new Vector<float>(MathF.Pow( BepuUtilities.MathHelper.Clamp(1 - _linearDamping, 0, 1), dt));
        _dtAngularDamping = new Vector<float>(MathF.Pow(BepuUtilities.MathHelper.Clamp(1 - _angularDamping, 0, 1), dt));
    }

    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
        BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt,
        ref BodyVelocityWide velocity)
    {
        velocity.Linear = (velocity.Linear + _dtGravity) * _dtLinearDamping;
        velocity.Angular *= _dtAngularDamping;
    }

    public AngularIntegrationMode AngularIntegrationMode { get; }
    public bool AllowSubstepsForUnconstrainedBodies { get; }
    public bool IntegrateVelocityForKinematics { get; }
}

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