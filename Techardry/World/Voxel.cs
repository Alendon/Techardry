namespace Techardry.World;

public struct Voxel : IEquatable<Voxel>
{
    public int Id;
    
    public bool Equals(Voxel other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is Voxel other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id;
    }
}