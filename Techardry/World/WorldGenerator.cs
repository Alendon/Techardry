﻿using System.Numerics;
using Techardry.Identifications;
using Techardry.Lib.FastNoseLite;
using Techardry.Utils;

namespace Techardry.World;

public class WorldGenerator
{
    private readonly TechardryWorld _parentWorld;
    private FastNoiseLite _noise;

    public WorldGenerator(TechardryWorld techardryWorld, WorldGeneratorSettings settings)
    {
        _parentWorld = techardryWorld;
        _noise = settings.Noise;
    }


    public Chunk GenerateChunk(Int3 chunkPosition)
    {
        var chunk = new Chunk(chunkPosition, _parentWorld);

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

        return chunk;
    }
}