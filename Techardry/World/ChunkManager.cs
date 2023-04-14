using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using BepuPhysics;
using MintyCore.Utils;
using Techardry.Utils;

namespace Techardry.World;

public class ChunkManager : IDisposable
{
    private readonly TechardryWorld _parentWorld;
    private readonly ConcurrentDictionary<Int3, ChunkEntry> _chunks = new();

    private readonly ConcurrentDictionary<Int3, Chunk> _chunksToLoad = new();
    private readonly ConcurrentBag<Int3> _chunksToUnload = new();

    public ChunkManager(TechardryWorld parentWorld)
    {
        _parentWorld = parentWorld;
    }

    public void PlayerChunkChanged(Int3 oldChunk, Int3 newChunk, ushort player)
    {
        Logger.AssertAndThrow(_parentWorld.IsServerWorld, "Player Chunk Changes can only be processed on the server",
            "ChunkManager");
        Task.Run(() => ProcessPlayerChunkChanged(oldChunk, newChunk, player));
    }

    private void ProcessPlayerChunkChanged(Int3 oldChunk, Int3 newChunk, ushort player)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Int3> GetLoadedChunks()
    {
        return _chunks.Keys;
    }
    
    public void Update()
    {
        foreach (var chunkToUnload in _chunksToUnload)
        {
            
        }
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

    record ChunkEntry(Chunk Chunk, StaticHandle StaticHandle, ConcurrentBag<ushort> Players);

    public void Dispose()
    {
        foreach (var chunkEntry in _chunks.Values)
        {
            _parentWorld.PhysicsWorld.Simulation.Statics.Remove(chunkEntry.StaticHandle);
            chunkEntry.Chunk.Dispose();
        }
    }
}