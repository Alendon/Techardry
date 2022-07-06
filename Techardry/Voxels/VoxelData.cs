using System.Diagnostics;
using System.Numerics;
using MintyCore.Utils;
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
            NotEmpty = Id != BlockIDs.Air ? 1 : 0,
            Color = BlockHandler.GetBlockColor(Id).ToVector4(),
            TextureStart = Vector3.Zero,
            TextureSize = Vector2.Zero
        };

        var textureId = BlockHandler.GetBlockTexture(Id);


        if (!TextureAtlasHandler.TryGetAtlasLocation(TextureAtlasIDs.BlockTexture, textureId, out var info))
            return renderData;
        
        renderData.TextureStart = new Vector3(info.Position.X, info.Position.Y, info.ArrayIndex);
        renderData.TextureSize = new Vector2(info.Size.X, info.Size.Y);
        
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