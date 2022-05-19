using System.Diagnostics;
using MintyCore.Utils;
using SixLabors.ImageSharp;
using Techardry.Blocks;

namespace Techardry.World;

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
        return new VoxelRenderData()
        {
            Color = BlockHandler.GetBlockColor(Id)
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