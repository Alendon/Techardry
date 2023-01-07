﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuPhysics.CollisionDetection.SweepTasks;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.Physics;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Lib.FastNoseLite;
using Techardry.Utils;
using Techardry.Voxels;

namespace Techardry.World;

public class TechardryWorld : MintyCore.ECS.World
{
    private ConcurrentDictionary<Int3, Chunk> _chunks = new();

    private List<StaticHandle> _bodyHandles = new();

    internal void CreateChunk(Int3 chunkPos)
    {
        if (_chunks.ContainsKey(chunkPos))
        {
            return;
        }

        var chunk = new Chunk(chunkPos);
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

    public TechardryWorld(bool isServerWorld) : base(isServerWorld)
    {
        SystemManager.SetSystemActive(MintyCore.Identifications.SystemIDs.RenderInstanced, false);
        RegisterPhysicsExtensions();
        CreateSomeChunks();
    }

    public TechardryWorld(bool isServerWorld, PhysicsWorld physicsWorld) : base(isServerWorld, physicsWorld)
    {
        SystemManager.SetSystemActive(MintyCore.Identifications.SystemIDs.RenderInstanced, false);
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
            X = 2,
            Y = 1,
            Z = 2
        };

        Int3 chunkPos = default;


        int seed = 5;

        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.02f);

        Stopwatch sw = Stopwatch.StartNew();

        /*for (chunkPos.X = -chunkRadius.X; chunkPos.X < chunkRadius.X; chunkPos.X++)
        {
            for (chunkPos.Y = 0; chunkPos.Y < 1; chunkPos.Y++)
            {
                for (chunkPos.Z = -chunkRadius.Z; chunkPos.Z < chunkRadius.Z; chunkPos.Z++)
                {
                    CreateChunk(chunkPos);

                    if (!TryGetChunk(chunkPos, out var chunk))
                    {
                        continue;
                    }

                    FillChunk(chunk, noise);
                }
            }
        }*/
        
        CreateChunk(chunkPos);
        TryGetChunk(chunkPos, out var chunk);
        FillChunk(chunk!, noise);

        sw.Stop();
        Logger.WriteLog($"Chunk gen took {sw.ElapsedMilliseconds}ms", LogImportance.Debug, "TechardryWorld");
        
        
        
        if (!IsServerWorld) return;

        /*foreach (var staticHandle in _bodyHandles)
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
        }*/
        
        
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
}