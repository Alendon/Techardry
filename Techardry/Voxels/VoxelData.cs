using System.Diagnostics;
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
            TextureStart = Vector3D<uint>.Zero,
            TextureSize = Vector2D<uint>.Zero
        };

        var textureId = BlockHandler.GetBlockTexture(Id);


        if (!TextureAtlasHandler.TryGetAtlasLocation(TextureAtlasIDs.BlockTexture, textureId, out var info))
            return renderData;
        
        renderData.TextureStart = new Vector3D<uint>(info.Position.X, info.Position.Y, info.ArrayIndex);
        renderData.TextureSize = new Vector2D<uint>(info.Size.X, info.Size.Y);
        
        return renderData;
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