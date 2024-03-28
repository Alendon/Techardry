using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Network;
using MintyCore.Utils;
using Serilog;
using Serilog.Core;
using Techardry.Blocks;
using Techardry.Components.Common;
using Techardry.Identifications;
using Techardry.Networking;
using Techardry.Render;
using Techardry.Utils;
using Techardry.Voxels;

namespace Techardry.World;

public class ChunkManager : IDisposable
{
    private readonly TechardryWorld _parentWorld;
    private readonly ConcurrentDictionary<Int3, ChunkEntry> _chunks = new();
    private readonly ConcurrentDictionary<Int3, ConcurrentDictionary<ushort, object?>> _chunkPlayers = new();

    private readonly ConcurrentUniqueQueue<ChunkLoadEntry> _chunksToLoad = new();
    private readonly ConcurrentUniqueQueue<Int3> _chunksToUnload = new();
    private readonly ConcurrentQueue<(Int3, VoxelOctree)> _chunksToUpdate = new();

    private readonly ConcurrentDictionary<Int3, object?> _activeChunkCreations = new();
    private readonly List<Task> _newChunkUpdateTasks = new();
    private Task _currentWaitTasks = Task.CompletedTask;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public event Action<Int3> ChunkAdded = delegate { };
    public event Action<Int3> ChunkUpdated = delegate { };
    public event Action<Int3> ChunkRemoved = delegate { };

    private INetworkHandler NetworkHandler { get; }
    private IPlayerHandler PlayerHandler { get; }
    private ITextureAtlasHandler TextureAtlasHandler { get; }
    private IBlockHandler BlockHandler { get; }
    private IInputDataManager? InputDataManager { get; }


    public ChunkManager(TechardryWorld parentWorld, INetworkHandler networkHandler, IPlayerHandler playerHandler,
        ITextureAtlasHandler textureAtlasHandler, IBlockHandler blockHandler, IInputDataManager? inputDataManager)
    {
        _parentWorld = parentWorld;
        NetworkHandler = networkHandler;
        PlayerHandler = playerHandler;
        TextureAtlasHandler = textureAtlasHandler;
        BlockHandler = blockHandler;
        InputDataManager = inputDataManager;

        IEntityManager.PostEntityCreateEvent += OnEntityCreate;
        IEntityManager.PreEntityDeleteEvent += OnEntityDelete;
    }

    public void PlayerChunkChanged(Int3? oldChunk, Int3? newChunk, ushort player)
    {
        if (!_parentWorld.IsServerWorld)
            throw new InvalidOperationException("Player Chunk Changes can only be processed on the server");

        Task.Run(() => ProcessPlayerChunkChanged(oldChunk, newChunk, player), _cancellationTokenSource.Token);
    }

    private void CreateChunk(Int3 chunkPosition)
    {
        if (_chunks.ContainsKey(chunkPosition))
            return;

        var chunk = _parentWorld.WorldGenerator.GenerateChunk(chunkPosition);

        _chunksToLoad.TryEnqueue(new ChunkLoadEntry { ChunkPosition = chunkPosition, Chunk = chunk });
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


        foreach (var chunk in chunksToLoad)
        {
            _chunkPlayers.AddOrUpdate(chunk,
                _ =>
                {
                    var dic = new ConcurrentDictionary<ushort, object?>();
                    dic.TryAdd(player, null);
                    toLoad.Add(chunk);
                    return dic;
                }
                , (_, players) =>
                {
                    players.TryAdd(player, null);
                    return players;
                });

            var createMessage = NetworkHandler.CreateMessage<CreateChunk>();
            createMessage.ChunkPosition = chunk;
            createMessage.WorldId = _parentWorld.Identification;

            createMessage.Send(player);
        }

        foreach (var chunk in chunksToUnload)
        {
            if (!_chunkPlayers.TryGetValue(chunk, out var players))
            {
                Log.Error("Player {Player} tried to unload chunk {Chunk} but it was not loaded", player, chunk);
                continue;
            }

            players.TryRemove(player, out _);

            var releaseMessage = NetworkHandler.CreateMessage<ReleaseChunk>();
            releaseMessage.ChunkPosition = chunk;
            releaseMessage.WorldId = _parentWorld.Identification;
            releaseMessage.Send(player);

            if (players.Count != 0) continue;

            _chunkPlayers.TryRemove(chunk, out _);
            toUnload.Add(chunk);
        }


        foreach (var chunk in toUnload)
        {
            _chunksToUnload.TryEnqueue(chunk);
        }


        foreach (var chunk in toLoad)
        {
            if (_chunks.ContainsKey(chunk)) continue;

            if (!_activeChunkCreations.TryAdd(chunk, null)) continue;

            var newTask = _currentWaitTasks.ContinueWith(_ => CreateChunk(chunk), _cancellationTokenSource.Token);

            _newChunkUpdateTasks.Add(newTask);
            var maxConcurrencyLevel = Task.Factory.Scheduler?.MaximumConcurrencyLevel ?? 4;
            if (_newChunkUpdateTasks.Count < maxConcurrencyLevel / 4) continue;

            _currentWaitTasks = Task.WhenAll(_newChunkUpdateTasks);
            _newChunkUpdateTasks.Clear();
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
        if (world != _parentWorld || world is { IsServerWorld: false }) return;
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
        while (_chunksToUnload.TryDequeue(out var chunkToUnload))
        {
            if (!_chunks.Remove(chunkToUnload, out var chunkEntry)) continue;

            _parentWorld.PhysicsWorld.Simulation.Statics.Remove(chunkEntry.StaticHandle);
            chunkEntry.Chunk.Dispose();
            _parentWorld.PhysicsWorld.Simulation.Shapes.Remove(chunkEntry.Shape);

            ChunkRemoved(chunkToUnload);

            if (!_parentWorld.IsServerWorld)
                InputDataManager?.RemoveKeyIndexedInputData(RenderInputDataIDs.Voxel, chunkToUnload);
        }


        while (_chunksToLoad.TryDequeue(out var toLoad))
        {
            var (chunkPosition, chunk) = toLoad;
            VoxelCollider collider = chunk.CreateCollider();
            var shape = _parentWorld.PhysicsWorld.Simulation.Shapes.Add(collider);
            var bodyHandle = _parentWorld.PhysicsWorld.Simulation.Statics.Add(
                new StaticDescription(
                    new Vector3(chunkPosition.X, chunkPosition.Y, chunkPosition.Z) *
                    Chunk.Size, shape));

            _chunks.TryAdd(toLoad.ChunkPosition, new ChunkEntry(toLoad.Chunk, bodyHandle, shape));
            _activeChunkCreations.TryRemove(toLoad.ChunkPosition, out _);

            ChunkAdded(chunkPosition);

            if (!_parentWorld.IsServerWorld)
                InputDataManager?.SetKeyIndexedInputData(RenderInputDataIDs.Voxel, chunkPosition, chunk.Octree);
        }

        if (!_parentWorld.IsServerWorld)
        {
            while (_chunksToUpdate.TryDequeue(out var toUpdate))
            {
                var (chunkPosition, octree) = toUpdate;
                if (!_chunks.TryGetValue(chunkPosition, out var chunkEntry))
                {
                    Log.Error("Chunk to update was not found: {ChunkPosition}", chunkPosition);
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

                ChunkUpdated(chunkPosition);

                if (!_parentWorld.IsServerWorld)
                    InputDataManager?.SetKeyIndexedInputData(RenderInputDataIDs.Voxel, chunkPosition, octree);
            }
        }

        foreach (var (chunk, _, _) in _chunks.Values)
        {
            chunk.Update();
        }
    }

    public void UpdateChunk(Int3 chunkPosition, VoxelOctree octree)
    {
        if (_parentWorld.IsServerWorld)
            throw new InvalidOperationException("Client tried to update chunk on server");

        _chunksToUpdate.Enqueue((chunkPosition, octree));
    }

    public bool TryGetChunk(Int3 chunkPosition, [MaybeNullWhen(false)] out Chunk chunk)
    {
        if (_chunks.TryGetValue(chunkPosition, out var chunkEntry))
        {
            chunk = chunkEntry.Chunk;
            return true;
        }

        chunk = null;
        return false;
    }

    public void SetBlock(Vector3 blockPos, Identification blockId, int depth,
        BlockRotation rotation = BlockRotation.None)
    {
        Int3 chunkPos = new((int)blockPos.X / Chunk.Size, (int)blockPos.Y / Chunk.Size, (int)blockPos.Z / Chunk.Size);
        if (blockPos.X < 0)
            chunkPos.X -= 1;
        if (blockPos.Y < 0)
            chunkPos.Y -= 1;
        if (blockPos.Z < 0)
            chunkPos.Z -= 1;

        SetBlock(chunkPos, blockPos, blockId, depth, rotation);
    }

    public void SetBlock(Int3 chunkPos, Vector3 blockPos, Identification blockId, int depth,
        BlockRotation rotation = BlockRotation.None)
    {
        if (!_chunks.TryGetValue(chunkPos, out var chunkEntry))
        {
            Log.Error("Chunk to set block in was not found: {ChunkPos}", chunkPos);
            return;
        }

        var (chunk, _, _) = chunkEntry;

        chunk.SetBlock(blockPos, blockId, depth, rotation);

        ChunkUpdated(chunkPos);
    }

    public Identification GetBlockId(Vector3 blockPos)
    {
        Int3 chunkPos = new((int)blockPos.X / Chunk.Size, (int)blockPos.Y / Chunk.Size, (int)blockPos.Z / Chunk.Size);
        if (blockPos.X < 0)
            chunkPos.X -= 1;
        if (blockPos.Y < 0)
            chunkPos.Y -= 1;
        if (blockPos.Z < 0)
            chunkPos.Z -= 1;

        return GetBlockId(chunkPos, blockPos);
    }

    public Identification GetBlockId(Int3 chunkPos, Vector3 blockPos)
    {
        if (!_chunks.TryGetValue(chunkPos, out var chunkEntry))
        {
            Log.Error("Chunk to get block in was not found: {ChunkPos}", chunkPos);
            return BlockIDs.Air;
        }

        var (chunk, _, _) = chunkEntry;
        return chunk.GetBlockId(blockPos);
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

        _chunks.Clear();
    }

    internal void AddChunk(Int3 chunkPosition)
    {
        if (_parentWorld.IsServerWorld)
            throw new InvalidOperationException("Chunks can only be manually added to the client world");

        _chunksToLoad.TryEnqueue(new ChunkLoadEntry()
        {
            ChunkPosition = chunkPosition,
            Chunk = new Chunk(chunkPosition, _parentWorld, PlayerHandler, NetworkHandler, BlockHandler,
                TextureAtlasHandler)
        });
    }


    internal void RemoveChunk(Int3 chunkPosition)
    {
        if (_parentWorld.IsServerWorld)
        {
            throw new InvalidOperationException("Chunks can only be manually removed from the client world");
        }

        _chunksToUnload.TryEnqueue(chunkPosition);
    }
}