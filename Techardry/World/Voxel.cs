namespace Techardry.World;

public readonly struct Voxel : IEquatable<Voxel>
{
    public readonly int Id;

    public Voxel(int id)
    {
        Id = id;
    }

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