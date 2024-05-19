using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;
using MintyCore.Utils.Events;
using Serilog;
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
    private readonly ConcurrentDictionary<Int3, Chunk> _chunks = new();

    private INetworkHandler NetworkHandler { get; }
    private IPlayerHandler PlayerHandler { get; }
    private ITextureAtlasHandler TextureAtlasHandler { get; }
    private IBlockHandler BlockHandler { get; }
    private IEventBus EventBus { get; }


    public ChunkManager(TechardryWorld parentWorld, INetworkHandler networkHandler, IPlayerHandler playerHandler,
        ITextureAtlasHandler textureAtlasHandler, IBlockHandler blockHandler, IEventBus eventBus)
    {
        _parentWorld = parentWorld;
        NetworkHandler = networkHandler;
        PlayerHandler = playerHandler;
        TextureAtlasHandler = textureAtlasHandler;
        BlockHandler = blockHandler;
        EventBus = eventBus;
    }

    public void CreateChunk(Int3 chunkPosition)
    {
        if (_chunks.ContainsKey(chunkPosition))
            return;

        var chunk = new Chunk(chunkPosition, _parentWorld, PlayerHandler, NetworkHandler, BlockHandler,
            TextureAtlasHandler);

        _chunks.TryAdd(chunkPosition, chunk);

        EventBus.InvokeEvent(new AddChunkEvent(_parentWorld, chunkPosition));
        
        if (_parentWorld.IsServerWorld)
            _parentWorld.WorldGenerator.EnqueueChunkGeneration(chunk);
    }

    internal void RemoveChunk(Int3 chunkPosition)
    {
        if (!_chunks.TryGetValue(chunkPosition, out var chunk))
        {
            Log.Error("Chunk to remove was not found: {ChunkPosition}", chunkPosition);
            return;
        }

        EventBus.InvokeEvent(new RemoveChunkEvent(_parentWorld, chunkPosition));
        chunk.Dispose();
    }

    public IEnumerable<Int3> GetLoadedChunks()
    {
        return _chunks.Keys;
    }

    public void Update()
    {
        foreach (var (_, chunk) in _chunks)
        {
            chunk.Update();
        }
    }

    public bool TryGetChunk(Int3 chunkPosition, [MaybeNullWhen(false)] out Chunk chunk)
    {
        if (_chunks.TryGetValue(chunkPosition, out chunk))
        {
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
        if (!_chunks.TryGetValue(chunkPos, out var chunk))
        {
            Log.Error("Chunk to set block in was not found: {ChunkPos}", chunkPos);
            return;
        }

        chunk.SetBlock(blockPos, blockId, depth, rotation);

        EventBus.InvokeEvent(new UpdateChunkEvent(_parentWorld, chunkPos, UpdateChunkEvent.ChunkUpdateKind.Voxel));
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
        if (!_chunks.TryGetValue(chunkPos, out var chunk))
        {
            Log.Error("Chunk to get block in was not found: {ChunkPos}", chunkPos);
            return BlockIDs.Air;
        }

        return chunk.GetBlockId(blockPos);
    }


    public void Dispose()
    {
        foreach (var (position, chunk) in _chunks)
        {
            EventBus.InvokeEvent(new RemoveChunkEvent(_parentWorld, position));
            chunk.Dispose();
        }

        _chunks.Clear();
    }

    public void RemoveEntityFromChunk(Int3 chunkToUnload, Entity entity)
    {
        if (!_chunks.TryGetValue(chunkToUnload, out var chunk))
        {
            Log.Error("Chunk to remove entity from was not found: {Chunk}", chunkToUnload);
            return;
        }
        
        chunk.RemovePlayerEntity(entity);
    }

    public void AddEntityToChunk(Int3 chunkToLoad, Entity entity)
    {
        if (!_chunks.TryGetValue(chunkToLoad, out var chunk))
        {
            Log.Error("Chunk to add entity to was not found: {Chunk}", chunkToLoad);
            return;
        }

        chunk.AddPlayerEntity(entity);
    }

    public void UpdateChunk(Int3 chunkPosition, VoxelOctree octree)
    {
        if(!_chunks.TryGetValue(chunkPosition, out var chunk))
        {
            Log.Error("Chunk to update was not found: {ChunkPosition}", chunkPosition);
            return;
        }
        
        chunk.SetOctree(octree);
        EventBus.InvokeEvent(new UpdateChunkEvent(_parentWorld, chunkPosition, UpdateChunkEvent.ChunkUpdateKind.Octree));
    }
}

/// <summary>
/// Event that is fired when a chunk is added to the world
/// </summary>
/// <param name="ContainingWorld"> The world the chunk was added to</param>
/// <param name="ChunkPosition"> The position of the chunk that was added</param>
[RegisterEvent("add_chunk")]
public record struct AddChunkEvent(TechardryWorld ContainingWorld, Int3 ChunkPosition) : IEvent
{
    public static Identification Identification => EventIDs.AddChunk;
    public static bool ModificationAllowed => false;
}

/// <summary>
///  Event that is fired when a chunk is removed from the world
/// </summary>
/// <param name="ContainingWorld"> The world the chunk was removed from</param>
/// <param name="ChunkPosition"> The position of the chunk that was removed</param>
[RegisterEvent("remove_chunk")]
public record struct RemoveChunkEvent(TechardryWorld ContainingWorld, Int3 ChunkPosition) : IEvent
{
    public static Identification Identification => EventIDs.RemoveChunk;
    public static bool ModificationAllowed => false;
}

/// <summary>
///  Event that is fired when a chunk object is updated
/// </summary>
/// <param name="ContainingWorld"> The world the chunk was updated in</param>
/// <param name="ChunkPosition"> The position of the chunk that was updated</param>
[RegisterEvent("update_chunk")]
public record struct UpdateChunkEvent(
    TechardryWorld ContainingWorld,
    Int3 ChunkPosition,
    UpdateChunkEvent.ChunkUpdateKind UpdateKind) : IEvent
{
    public static Identification Identification => EventIDs.UpdateChunk;
    public static bool ModificationAllowed => false;

    public enum ChunkUpdateKind
    {
        Voxel,
        Octree
    }
}