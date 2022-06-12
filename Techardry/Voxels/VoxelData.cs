using System.Diagnostics;
using MintyCore.Utils;
using Techardry.Blocks;
using Techardry.Identifications;

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
        var color = BlockHandler.GetBlockColor(Id);
        return new VoxelRenderData()
        {
            Color = color.ToVector4(),
            NotEmpty = Id != BlockIDs.Air ? 1 : 0
        };
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