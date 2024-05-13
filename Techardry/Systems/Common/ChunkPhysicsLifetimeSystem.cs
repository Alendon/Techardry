using System.Collections.Concurrent;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Registries;
using MintyCore.Utils;
using MintyCore.Utils.Events;
using Serilog;
using Serilog.Core;
using Techardry.Identifications;
using Techardry.Systems.Common.Physics;
using Techardry.Utils;
using Techardry.World;

namespace Techardry.Systems.Common;

//TODO find a way to prevent two systems accessing the physics world at the same time

[RegisterSystem("chunk_physics_lifetime")]
[ExecuteInSystemGroup<InitializationSystemGroup>]
[ExecuteAfter<PhysicsInitializationSystem>]
public class ChunkPhysicsLifetimeSystem(IEventBus eventBus) : ASystem
{
    public override Identification Identification => SystemIDs.ChunkPhysicsLifetime;

    private ConcurrentQueue<(Int3 chunkPosition, bool add)> _chunkUpdates = new();


    private Dictionary<Int3, (StaticHandle, TypedIndex)> _chunkPhysics = new();

    private EventBinding<AddChunkEvent>? _addChunkEventBinding;
    private EventBinding<RemoveChunkEvent>? _removeChunkEventBinding;
    private EventBinding<UpdateChunkEvent>? _updateChunkEventBinding;

    public override void Setup(SystemManager systemManager)
    {
        _addChunkEventBinding = new EventBinding<AddChunkEvent>(eventBus, OnAddChunk);
        _removeChunkEventBinding = new EventBinding<RemoveChunkEvent>(eventBus, OnRemoveChunk);
        _updateChunkEventBinding = new EventBinding<UpdateChunkEvent>(eventBus, OnUpdateChunk);
    }

    private EventResult OnUpdateChunk(UpdateChunkEvent e)
    {
        if (ReferenceEquals(World, e.ContainingWorld))
        {
            _chunkUpdates.Enqueue((e.ChunkPosition, false));
            _chunkUpdates.Enqueue((e.ChunkPosition, true));
        }

        return EventResult.Continue;
    }

    private EventResult OnRemoveChunk(RemoveChunkEvent e)
    {
        if (ReferenceEquals(World, e.ContainingWorld))
            _chunkUpdates.Enqueue((e.ChunkPosition, false));

        return EventResult.Continue;
    }

    private EventResult OnAddChunk(AddChunkEvent e)
    {
        if (ReferenceEquals(World, e.ContainingWorld))
            _chunkUpdates.Enqueue((e.ChunkPosition, true));

        return EventResult.Continue;
    }

    protected override void Execute()
    {
        if (World is not TechardryWorld world) return;
        var simulation = world.PhysicsWorld.Simulation;

        while (_chunkUpdates.TryDequeue(out var entry))
        {
            var (chunkPosition, add) = entry;

            if (add)
            {
                AddChunk(world, chunkPosition, simulation);
            }
            else
            {
                RemoveChunk(chunkPosition, simulation);
            }
        }
    }

    private void RemoveChunk(Int3 chunkPosition, Simulation simulation)
    {
        if (!_chunkPhysics.Remove(chunkPosition, out var physicsInfo))
        {
            Log.Error("Tried to remove physics for chunk at {ChunkPosition} but it does not exist",
                chunkPosition);
            return;
        }
                
        simulation.Statics.Remove(physicsInfo.Item1);
        simulation.Shapes.Remove(physicsInfo.Item2);
    }

    private void AddChunk(TechardryWorld world, Int3 chunkPosition, Simulation simulation)
    {
        if (!world.ChunkManager.TryGetChunk(chunkPosition, out var chunk))
        {
            Log.Warning("Tried to update physics for chunk at {ChunkPosition} but it does not exist",
                chunkPosition);
            return;
        }

        if (_chunkPhysics.ContainsKey(chunkPosition))
        {
            Log.Error("Tried to add physics for chunk at {ChunkPosition} but it already exists", chunkPosition);
            return;
        }

        var collider = chunk.CreateCollider();
        var shape = simulation.Shapes.Add(collider);
        var staticHandle = simulation.Statics.Add(
            new StaticDescription(
                new Vector3(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) *
                Chunk.Size, shape));

        _chunkPhysics.Add(chunkPosition, (staticHandle, shape));
    }

    protected override void Dispose(bool disposing)
    {
        _addChunkEventBinding?.UnregisterBinding(eventBus);
        _removeChunkEventBinding?.UnregisterBinding(eventBus);
        _updateChunkEventBinding?.UnregisterBinding(eventBus);
        
        base.Dispose(disposing);
    }
}