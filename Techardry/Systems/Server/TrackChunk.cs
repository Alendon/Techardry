using System.Runtime.CompilerServices;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Registries;
using MintyCore.Utils;
using Serilog;
using Techardry.Components.Common;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.World;

namespace Techardry.Systems.Server;

[RegisterSystem("track_chunk")]
[ExecuteInSystemGroup<FinalizationSystemGroup>]
[ExecutionSide(GameType.Server)]
public partial class TrackChunk() : ASystem
{
    [ComponentQuery] private ComponentQuery<LastChunk, Position> _componentQuery = new();

    private QuickDictionary<Int2, QuickSet<Entity, EntityComparer>, IntComparer> _chunkEntities;
    private QuickSet<Int2, IntComparer> _chunksToRemove;
    private QuickList<(Entity Entity, Int2 ChunkPos)> _entitiesToRemove;


    private readonly BufferPool _bufferPool = new(expectedPooledResourceCount: 256);
    private const int RenderDistance = 4;
    private const int MinChunkY = -2;
    private const int MaxChunkY = 2;


    public override void Setup(SystemManager systemManager)
    {
        _chunkEntities = new QuickDictionary<Int2, QuickSet<Entity, EntityComparer>, IntComparer>(128, _bufferPool);
        _chunksToRemove = new QuickSet<Int2, IntComparer>(32, _bufferPool);
        _entitiesToRemove = new QuickList<(Entity, Int2)>(8, _bufferPool);

        _componentQuery.Setup(this);

        IEntityManager.PreEntityDeleteEvent += OnEntityDelete;
    }

    private void OnEntityDelete(IWorld world, Entity entity)
    {
        if (!ReferenceEquals(World, world) || !World.IsServerWorld) return;

        ref var lastChunk = ref World.EntityManager.TryGetComponent<LastChunk>(entity, out var found);
        if (!found || !lastChunk.HasValue) return;

        _entitiesToRemove.Add((entity, lastChunk.Value), _bufferPool);
    }

    protected override void Execute()
    {
        if (World is not TechardryWorld techardryWorld) return;

        _chunksToRemove.Clear();

        foreach (var currentEntity in _componentQuery)
        {
            var entity = currentEntity.Entity;
            var position = currentEntity.GetPosition();
            var currentChunk = new Int2((int)(position.Value.X / Chunk.Size), (int)(position.Value.Z / Chunk.Size));
            ref var lastChunk = ref currentEntity.GetLastChunk();

            if (lastChunk.HasValue && lastChunk.Value == currentChunk) continue;

            Log.Debug("Entity {Entity} moved to chunk {Chunk}. New position {Position}", entity, currentChunk,
                position.Value);

            for (int dx = -RenderDistance; dx <= RenderDistance; dx++)
            for (int dz = -RenderDistance; dz <= RenderDistance; dz++)
            {
                var chunkToLoad = new Int2(currentChunk.X + dx, currentChunk.Y + dz);
                var oldChunk = lastChunk.Value;
                AddEntityTracking(ref lastChunk, ref chunkToLoad, ref oldChunk, ref entity,
                    techardryWorld.ChunkManager);


                var chunkToUnload = new Int2(oldChunk.X + dx, oldChunk.Y + dz);
                RemoveEntityTracking(ref lastChunk, ref chunkToUnload, ref currentChunk, ref entity,
                    techardryWorld.ChunkManager);
            }

            lastChunk.Value = currentChunk;
            lastChunk.HasValue = true;
            lastChunk.Dirty = true;
        }

        for (var i = 0; i < _entitiesToRemove.Count; i++)
        {
            ref var entry = ref _entitiesToRemove.Span[i];
            RemoveDeletedEntity(ref entry.ChunkPos, ref entry.Entity, techardryWorld.ChunkManager);
        }


        for (var i = 0; i < _chunksToRemove.Count; i++)
        {
            ref var chunk = ref _chunksToRemove.Span[i];
            var chunkExists = _chunkEntities.TryGetValue(ref chunk, out var entitySet);
            if (!chunkExists)
            {
                Log.Error("Chunk {Chunk} to check for removal was not found in the dictionary", chunk);
                continue;
            }

            if (entitySet.Count != 0) continue;
            _chunkEntities.FastRemove(ref chunk);

            for (int y = MinChunkY; y <= MaxChunkY; y++)
            {
                var chunkPos = new Int3(chunk.X, y, chunk.Y);
                techardryWorld.ChunkManager.RemoveChunk(chunkPos);
            }
        }

        _chunksToRemove.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveDeletedEntity(ref Int2 chunkToUnload, ref Entity entity, ChunkManager chunkManager)
    {
        if (!_chunkEntities.TryGetValue(ref chunkToUnload, out var entitySet)) return;

        entitySet.FastRemove(ref entity);

        for (int y = MinChunkY; y <= MaxChunkY; y++)
        {
            var chunkPos = new Int3(chunkToUnload.X, y, chunkToUnload.Y);
            chunkManager.RemoveEntityFromChunk(chunkPos, entity);
        }

        if (entitySet.Count == 0)
        {
            _chunksToRemove.Add(ref chunkToUnload, _bufferPool);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveEntityTracking(ref LastChunk lastChunk, ref Int2 chunkToUnload, ref Int2 currentChunk,
        ref Entity entity, ChunkManager chunkManager)
    {
        if (!lastChunk.HasValue || IsChunkInRenderDistance(chunkToUnload, currentChunk)) return;
        if (!_chunkEntities.TryGetValue(ref chunkToUnload, out var entitySet)) return;

        entitySet.FastRemove(ref entity);

        for (int y = MinChunkY; y <= MaxChunkY; y++)
        {
            var chunkPos = new Int3(chunkToUnload.X, y, chunkToUnload.Y);
            chunkManager.RemoveEntityFromChunk(chunkPos, entity);
        }

        if (entitySet.Count == 0)
        {
            _chunksToRemove.Add(ref chunkToUnload, _bufferPool);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddEntityTracking(ref LastChunk lastChunk, ref Int2 chunkToLoad, ref Int2 oldChunk, ref Entity entity,
        ChunkManager chunkManager)
    {
        // if the chunk was in the render distance last tick, it is already tracked and loaded
        if (lastChunk.HasValue && IsChunkInRenderDistance(chunkToLoad, oldChunk)) return;

        if (!_chunkEntities.TryGetValue(ref chunkToLoad, out var entitySet))
        {
            entitySet = new QuickSet<Entity, EntityComparer>(8, _bufferPool);

            _chunkEntities.Add(ref chunkToLoad, in entitySet, _bufferPool);

            for (int y = MinChunkY; y <= MaxChunkY; y++)
            {
                var chunkPos = new Int3(chunkToLoad.X, y, chunkToLoad.Y);
                chunkManager.CreateChunk(chunkPos);
            }
        }

        entitySet.Add(ref entity, _bufferPool);

        for (int y = MinChunkY; y <= MaxChunkY; y++)
        {
            var chunkPos = new Int3(chunkToLoad.X, y, chunkToLoad.Y);
            chunkManager.AddEntityToChunk(chunkPos, entity);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsChunkInRenderDistance(Int2 chunk, Int2 playerChunk)
    {
        return Math.Abs(chunk.X - playerChunk.X) <= RenderDistance &&
               Math.Abs(chunk.Y - playerChunk.Y) <= RenderDistance;
    }

    public override Identification Identification => SystemIDs.TrackChunk;

    struct IntComparer : IEqualityComparerRef<Int2>
    {
        public int Hash(ref Int2 item)
        {
            return item.GetHashCode();
        }

        public bool Equals(ref Int2 a, ref Int2 b)
        {
            return a.X == b.X && a.Y == b.Y;
        }
    }

    struct EntityComparer : IEqualityComparerRef<Entity>
    {
        public int Hash(ref Entity item)
        {
            return item.GetHashCode();
        }

        public bool Equals(ref Entity a, ref Entity b)
        {
            return a.Id == b.Id && a.ArchetypeId == b.ArchetypeId;
        }
    }
}