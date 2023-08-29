﻿using System.Diagnostics;
using System.Numerics;
using MintyCore.Utils;
using Silk.NET.Maths;
using Techardry.Blocks;
using Techardry.Identifications;
using Techardry.Render;

namespace Techardry.Voxels;

[DebuggerDisplay("{Id}")]
public readonly struct VoxelData : IEquatable<VoxelData>
{
    public readonly Identification Id;

    public VoxelData(Identification id)
    {
        Id = id;
    }

    public VoxelPhysicsData GetPhysicsData()
    {
        return default;
    }

    public VoxelRenderData GetRenderData()
    {
        var renderData = new VoxelRenderData()
        {
            Color = BlockHandler.GetBlockColor(Id).Rgba,
            TextureStart = Vector2.Zero,
            TextureSize = Vector2.Zero
        };

        var textureId = BlockHandler.GetBlockTexture(Id);


        if (!TextureAtlasHandler.TryGetAtlasLocation(TextureAtlasIDs.BlockTexture, textureId, out var info))
            return renderData;
        
        renderData.TextureStart = info.Position;
        renderData.TextureSize = info.Size;
        
        return renderData;
    }

    public void Serialize(DataWriter writer)
    {
        Id.Serialize(writer);
    }
    
    public static bool Deserialize(DataReader reader, out VoxelData data)
    {
        if (Identification.Deserialize(reader, out var id))
        {
            data = new VoxelData(id);
            return true;
        }
        
        data = default;
        return false;
    }

    public bool Equals(VoxelData other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is VoxelData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(VoxelData left, VoxelData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(VoxelData left, VoxelData right)
    {
        return !(left == right);
    }
}