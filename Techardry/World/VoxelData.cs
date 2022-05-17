using System.Diagnostics;
using SixLabors.ImageSharp;

namespace Techardry.World;

[DebuggerDisplay("{Id}")]
public readonly struct VoxelData : IEquatable<VoxelData>
{
    public readonly int Id;

    public VoxelData(int id)
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
            Color = Id switch
            {
                8 => Color.SaddleBrown,
                7 => Color.Green,
                6 => Color.Blue,
                5 => Color.Red,
                4 => Color.Yellow,
                3 => Color.Purple,
                2 => Color.Orange,
                _ => Color.White
            }
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
        return Id;
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