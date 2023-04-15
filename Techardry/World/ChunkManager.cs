using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using DotNext.Threading;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Physics;
using MintyCore.Utils;
using Techardry.Components.Common;
using Techardry.Identifications;
using Techardry.Networking;
using Techardry.Utils;
using Techardry.Voxels;

namespace Techardry.World;

public class ChunkManager : IDisposable
{
    private readonly TechardryWorld _parentWorld;
    private readonly Dictionary<Int3, ChunkEntry> _chunks = new();
    private readonly Dictionary<Int3, HashSet<ushort>> _chunkPlayers = new();

    private readonly UniqueQueue<ChunkLoadEntry> ChunksToLoad = new();
    private readonly UniqueQueue<Int3> _chunksToUnload = new();
    private readonly Queue<(Int3, VoxelOctree)> _chunksToUpdate = new();

    private readonly HashSet<Int3> _activeChunkCreations = new();

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public ChunkManager(TechardryWorld parentWorld)
    {
        _parentWorld = parentWorld;

        if (!_parentWorld.IsServerWorld) return;
        
        EntityManager.PostEntityCreateEvent += OnEntityCreate;
        EntityManager.PreEntityDeleteEvent += OnEntityDelete;
    }

    public void PlayerChunkChanged(Int3? oldChunk, Int3? newChunk, ushort player)
    {
        Logger.AssertAndThrow(_parentWorld.IsServerWorld, "Player Chunk Changes can only be processed on the server",
            "ChunkManager");

        Task.Run(() => ProcessPlayerChunkChanged(oldChunk, newChunk, player), _cancellationTokenSource.Token);
    }

    private void CreateChunk(Int3 chunkPosition)
    {
        var chunk = _parentWorld.WorldGenerator.GenerateChunk(chunkPosition);

        using var toLoadLock = ChunksToLoad.AcquireWriteLock();
        ChunksToLoad.TryEnqueue(new ChunkLoadEntry { ChunkPosition = chunkPosition, Chunk = chunk });
    }

    private void ProcessPlayerChunkChanged(Int3? oldChunk, Int3? newChunk, ushort player)
    {
        IEnumerable<Int3> chunksToLoad;
        IEnumerable<Int3> chunksToUnload;

        if (oldChunk is null && newChunk is not null)
        {
            chunksToUnload = Enumerable.Empty<Int3>();
            chunksToLoad = AreaAroundChunk(newChunk.Value);
        }
        else if (oldChunk is not null && newChunk is null)
        {
            chunksToLoad = Enumerable.Empty<Int3>();
            chunksToUnload = AreaAroundChunk(oldChunk.Value);
        }
        else if (oldChunk is not null && newChunk is not null)
        {
            chunksToLoad = AreaAroundChunk(newChunk.Value).Except(AreaAroundChunk(oldChunk.Value));
            chunksToUnload = AreaAroundChunk(oldChunk.Value).Except(AreaAroundChunk(newChunk.Value));
        }
        else
        {
            chunksToLoad = Enumerable.Empty<Int3>();
            chunksToUnload = Enumerable.Empty<Int3>();
        }

        List<Int3> toLoad = new();
        List<Int3> toUnload = new();

        using (_chunkPlayers.AcquireWriteLock())
        {
            foreach (var chunk in chunksToLoad)
            {
                if (!_chunkPlayers.TryGetValue(chunk, out var players))
                {
                    players = new HashSet<ushort>();
                    _chunkPlayers.Add(chunk, players);

                    toLoad.Add(chunk);
                }

                players.Add(player);

                var createMessage = new CreateChunk()
                {
                    ChunkPosition = chunk,
                    WorldId = _parentWorld.Identification
                };

                createMessage.Send(player);
            }

            foreach (var chunk in chunksToUnload)
            {
                if (!_chunkPlayers.TryGetValue(chunk, out var players))
                {
                    Logger.WriteLog($"Player {player} tried to unload chunk {chunk} but it was not loaded",
                        LogImportance.Error, "ChunkManager");
                    continue;
                }

                players.Remove(player);

                var releaseMessage = new ReleaseChunk()
                {
                    ChunkPosition = chunk,
                    WorldId = _parentWorld.Identification
                };
                releaseMessage.Send(player);

                if (players.Count != 0) continue;

                _chunkPlayers.Remove(chunk);
                toUnload.Add(chunk);
            }
        }

        using (_chunksToUnload.AcquireWriteLock())
        {
            foreach (var chunk in toUnload)
            {
                _chunksToUnload.TryEnqueue(chunk);
            }
        }

        using (ChunksToLoad.AcquireWriteLock())
        using (_activeChunkCreations.AcquireWriteLock())
        using (_chunks.AcquireReadLock())
        {
            foreach (var chunk in toLoad)
            {
                if (_chunks.ContainsKey(chunk)) continue;

                if (_activeChunkCreations.Contains(chunk)) continue;

                _activeChunkCreations.Add(chunk);
                Task.Run(() => CreateChunk(chunk), _cancellationTokenSource.Token);
            }
        }

        IEnumerable<Int3> AreaAroundChunk(Int3 chunk)
        {
            //calculate all positions in the render area around the chunk
            for (var x = chunk.X - TechardryMod.Instance!.ServerRenderDistance;
                 x <= chunk.X + TechardryMod.Instance.ServerRenderDistance;
                 x++)
            {
                for (int y = chunk.Y - TechardryMod.Instance.ServerRenderDistance;
                     y <= chunk.Y + TechardryMod.Instance.ServerRenderDistance;
                     y++)
                {
                    for (int z = chunk.Z - TechardryMod.Instance.ServerRenderDistance;
                         z <= chunk.Z + TechardryMod.Instance.ServerRenderDistance;
                         z++)
                    {
                        yield return new Int3(x, y, z);
                    }
                }
            }
        }
    }


    private void OnEntityDelete(IWorld world, Entity entity)
    {
        if (world != _parentWorld) return;
        if (entity.ArchetypeId != ArchetypeIDs.TestCamera) return;

        var player = _parentWorld.EntityManager.GetEntityOwner(entity);
        var position = _parentWorld.EntityManager.GetComponent<Position>(entity);
        var chunkPos = new Int3((int)position.Value.X / Chunk.Size, (int)position.Value.Y / Chunk.Size,
            (int)position.Value.Z / Chunk.Size);

        PlayerChunkChanged(chunkPos, null, player);
    }

    private void OnEntityCreate(IWorld world, Entity entity)
    {
        if (world != _parentWorld) return;
        if (entity.ArchetypeId != ArchetypeIDs.TestCamera) return;

        var player = _parentWorld.EntityManager.GetEntityOwner(entity);
        var position = _parentWorld.EntityManager.GetComponent<Position>(entity);
        var chunkPos = new Int3((int)position.Value.X / Chunk.Size, (int)position.Value.Y / Chunk.Size,
            (int)position.Value.Z / Chunk.Size);
        
        _parentWorld.EntityManager.GetComponent<LastChunk>(entity).Value = chunkPos;

        PlayerChunkChanged(null, chunkPos, player);
    }

    public IEnumerable<Int3> GetLoadedChunks()
    {
        return _chunks.Keys;
    }

    public void Update()
    {
        using var chunksLock = _chunks.AcquireWriteLock();

        using (_chunksToUnload.AcquireWriteLock())
        {
            while (_chunksToUnload.TryDequeue(out var chunkToUnload))
            {
                if (!_chunks.Remove(chunkToUnload, out var chunkEntry)) continue;

                _parentWorld.PhysicsWorld.Simulation.Statics.Remove(chunkEntry.StaticHandle);
                chunkEntry.Chunk.Dispose();
                _parentWorld.PhysicsWorld.Simulation.Shapes.Remove(chunkEntry.Shape);

                Logger.WriteLog($"Unloaded Chunk {chunkToUnload}", LogImportance.Info, "ChunkManager");
            }
        }


        using (ChunksToLoad.AcquireWriteLock())
        using (_activeChunkCreations.AcquireWriteLock())
            while (ChunksToLoad.TryDequeue(out var toLoad))
            {
                var (chunkPosition, chunk) = toLoad;
                VoxelCollider collider = chunk.CreateCollider();
                var shape = _parentWorld.PhysicsWorld.Simulation.Shapes.Add(collider);
                var bodyHandle = _parentWorld.PhysicsWorld.Simulation.Statics.Add(
                    new StaticDescription(
                        new Vector3(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) *
                        Chunk.Size, shape));

                _chunks.TryAdd(toLoad.ChunkPosition, new ChunkEntry(toLoad.Chunk, bodyHandle, shape));
                _activeChunkCreations.Remove(toLoad.ChunkPosition);
            }

        if (!_parentWorld.IsServerWorld)
        {
            using (_chunksToUpdate.AcquireWriteLock())
            {

                foreach (var (chunkPosition, octree) in _chunksToUpdate)
                {
                    if (!_chunks.TryGetValue(chunkPosition, out var chunkEntry))
                    {
                        Logger.WriteLog($"Chunk to update was not found: {chunkPosition}", LogImportance.Error, "ChunkManager");
                        continue;
                    }

                    var (chunk, staticHandle, shape) = chunkEntry;

                    chunk.SetOctree(octree);

                    //destroy physics objects
                    _parentWorld.PhysicsWorld.Simulation.Statics.Remove(staticHandle);
                    _parentWorld.PhysicsWorld.Simulation.Shapes.Remove(shape);

                    //create new physics objects
                    var collider = chunk.CreateCollider();
                    shape = _parentWorld.PhysicsWorld.Simulation.Shapes.Add(collider);
                    staticHandle = _parentWorld.PhysicsWorld.Simulation.Statics.Add(
                        new StaticDescription(
                            new Vector3(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) *
                            Chunk.Size, shape));

                    _chunks[chunkPosition] = new ChunkEntry(chunk, staticHandle, shape);
                }
                
            }
        }


        foreach (var (chunk, _, _) in _chunks.Values)
        {
            chunk.Update();
        }
    }

    public void UpdateChunk(Int3 chunkPosition, VoxelOctree octree)
    {
        Logger.AssertAndThrow(!_parentWorld.IsServerWorld, "Client tried to update chunk on server", "ChunkManager");

        using var lockHandle = _chunksToUpdate.AcquireWriteLock();
        
        _chunksToUpdate.Enqueue((chunkPosition, octree));
    }

    public bool TryGetChunk(Int3 chunkPosition, [MaybeNullWhen(false)] out Chunk chunk)
    {
        using var lockHandle = _chunks.AcquireReadLock();
        if (!lockHandle.IsEmpty && _chunks.TryGetValue(chunkPosition, out var chunkEntry))
        {
            chunk = chunkEntry.Chunk;
            return true;
        }

        chunk = null;
        return false;
    }

    record ChunkEntry(Chunk Chunk, StaticHandle StaticHandle, TypedIndex Shape);

    //Basically a tuple but the comparison only uses the chunk position
    internal struct ChunkLoadEntry
    {
        public Int3 ChunkPosition;
        public Chunk Chunk;

        public override bool Equals(object? obj)
        {
            return obj is ChunkLoadEntry entry && ChunkPosition.Equals(entry.ChunkPosition);
        }

        public override int GetHashCode()
        {
            return ChunkPosition.GetHashCode();
        }

        public void Deconstruct(out Int3 chunkPosition, out Chunk chunk)
        {
            chunkPosition = ChunkPosition;
            chunk = Chunk;
        }
    }

    public void Dispose()
    {
        foreach (var chunkEntry in _chunks.Values)
        {
            _parentWorld.PhysicsWorld.Simulation.Statics.Remove(chunkEntry.StaticHandle);
            _parentWorld.PhysicsWorld.Simulation.Shapes.Remove(chunkEntry.Shape);
            chunkEntry.Chunk.Dispose();
        }
    }

    internal void AddChunk(Int3 chunkPosition)
    {
        Logger.AssertAndThrow(!_parentWorld.IsServerWorld, "Chunks can only be manually added to the client world",
            "ChunkManager");

        using (ChunksToLoad.AcquireWriteLock())
        {
            ChunksToLoad.TryEnqueue(new ChunkLoadEntry()
            {
                ChunkPosition = chunkPosition,
                Chunk = new Chunk(chunkPosition, _parentWorld)
            });
        }
    }

    internal void RemoveChunk(Int3 chunkPosition)
    {
        Logger.AssertAndThrow(!_parentWorld.IsServerWorld, "Chunks can only be manually removed from the client world",
            "ChunkManager");

        using (_chunksToUnload.AcquireWriteLock())
        {
            _chunksToUnload.TryEnqueue(chunkPosition);
        }
    }
}