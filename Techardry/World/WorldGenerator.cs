using System.Collections.Concurrent;
using System.Numerics;
using DotNext.Threading;
using MintyCore.Utils.Events;
using Serilog;
using Techardry.Identifications;
using Techardry.Lib.FastNoseLite;
using Techardry.Utils;

namespace Techardry.World;

public class WorldGenerator(WorldGeneratorSettings settings, IEventBus eventBus)
{
    private readonly FastNoiseLite _noise = settings.Noise;

    private volatile bool _running;
    private Thread[] _worldGenThreads = [];

    private readonly BlockingCollection<Chunk> _chunksToGenerate = new(new ConcurrentQueue<Chunk>());

    public void StartWorldGenThreads()
    {
        var threadCount = Environment.ProcessorCount / 4;
        threadCount = Math.Max(1, threadCount);
        _running = true;

        _worldGenThreads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            _worldGenThreads[i] = new Thread(WorldGenWorker);
            _worldGenThreads[i].Name = $"WorldGenThread-{i}";
            _worldGenThreads[i].Start();
        }
    }

    public void StopWorldGenThreads()
    {
        _chunksToGenerate.CompleteAdding();

        _running = false;
        foreach (var thread in _worldGenThreads)
        {
            thread.Join();
        }
    }

    public void EnqueueChunkGeneration(Chunk chunk)
    {
        if (!_running)
        {
            Log.Warning("Tried to enqueue chunk generation while world generator is not running");
            return;
        }
        
        _chunksToGenerate.Add(chunk);
    }

    private void WorldGenWorker()
    {
        foreach (var chunk in _chunksToGenerate.GetConsumingEnumerable())
        {
            GenerateChunk(chunk);

            if (_chunksToGenerate.Count == 0)
            {
                Log.Information("All current chunks generated, waiting for more");
            }
        }
    }

    private void GenerateChunk(Chunk chunk)
    {
        var chunkPosition = chunk.Position;
        using var octreeLock = chunk.Octree.AcquireWriteLock();
        
        chunk.Octree.CompactingEnabled = false;
        

        var realChunkPosition = new Vector3(chunkPosition.X * Chunk.Size, chunkPosition.Y * Chunk.Size,
            chunkPosition.Z * Chunk.Size);

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                var noiseValue = _noise.GetNoise(x + chunk.Position.X * Chunk.Size,
                    z + chunk.Position.Z * Chunk.Size);
                noiseValue += 0.5f;
                noiseValue /= 0.5f;
                noiseValue *= 6;

                for (int y = 0; y < Chunk.Size; y++)
                {
                    var localPos = new Vector3(x, y, z);
                    var pos = localPos + realChunkPosition;

                    if (pos.Y < 6)
                    {
                        chunk.SetBlock(localPos, BlockIDs.Stone);
                        continue;
                    }

                    if (pos.Y < 7 + noiseValue)
                    {
                        chunk.SetBlock(localPos, BlockIDs.Dirt);
                        continue;
                    }

                    break;
                }
            }
        }

        chunk.Octree.CompactingEnabled = true;
        chunk.Octree.Compact(true);

        eventBus.InvokeEvent(new UpdateChunkEvent(chunk.ParentWorld, chunk.Position,
            UpdateChunkEvent.ChunkUpdateKind.Octree));
    }
}