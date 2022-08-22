using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
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

    internal void CreateChunk(Int3 chunkPos)
    {
        if (_chunks.ContainsKey(chunkPos))
        {
            return;
        }

        var chunk = new Chunk(chunkPos);
        _chunks.TryAdd(chunkPos, chunk);
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
        CreateSomeChunks();
    }

    public TechardryWorld(bool isServerWorld, PhysicsWorld physicsWorld) : base(isServerWorld, physicsWorld)
    {
        SystemManager.SetSystemActive(MintyCore.Identifications.SystemIDs.RenderInstanced, false);
        CreateSomeChunks();
    }

    private void CreateSomeChunks()
    {

        Int3 ChunkRadius = new()
        {
            X = 5,
            Y = 1,
            Z = 5
        };

        Int3 ChunkPos = default;
        
        
        int seed = 5;

        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.02f);
        
        Stopwatch sw = Stopwatch.StartNew();
        
        for(ChunkPos.X = -ChunkRadius.X; ChunkPos.X <= ChunkRadius.X; ChunkPos.X++)
        {
            for(ChunkPos.Y = -ChunkRadius.Y; ChunkPos.Y <= ChunkRadius.Y; ChunkPos.Y++)
            {
                for(ChunkPos.Z = -ChunkRadius.Z; ChunkPos.Z <= ChunkRadius.Z; ChunkPos.Z++)
                {
                    CreateChunk(ChunkPos);

                    if (!TryGetChunk(ChunkPos, out var chunk))
                    {
                        continue;
                    }
                    
                    FillChunk(chunk, noise);
                }
            }
        }
        
        sw.Stop();
        Logger.WriteLog($"Chunk gen took {sw.ElapsedMilliseconds}ms", LogImportance.Debug, "TechardryWorld");
    }
    
    void FillChunk(Chunk chunk, FastNoiseLite fastNoiseLite)
    {
        for (int x = 0; x < VoxelOctree.Dimensions; x++)
        {
            for (int z = 0; z < VoxelOctree.Dimensions; z++)
            {
                for (int y = 0; y < 6; y++)
                {
                    chunk.SetBlock(new Vector3(x, y, z), BlockIDs.Stone);
                }

                var noiseValue = fastNoiseLite.GetNoise(x, z);
                noiseValue += 0.5f;
                noiseValue /= 0.5f;
                noiseValue *= 6;

                for (int y = 6; y < 7 + noiseValue; y++)
                {
                    chunk.SetBlock(new Vector3(x, y, z), BlockIDs.Dirt);
                }
            }
        }
    }
}