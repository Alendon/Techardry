using JetBrains.Annotations;

namespace Techardry.Utils;

[PublicAPI]
public struct Int3 : IEquatable<Int3>
{
    public int X;
    public int Y;
    public int Z;
    
    public Int3(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Int3(int v)
    {
        X = v;
        Y = v;
        Z = v;
    }
    
    public static bool operator ==(Int3 left, Int3 right)
    {
        return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
    }
    
    public static bool operator !=(Int3 left, Int3 right)
    {
        return left.X != right.X || left.Y != right.Y || left.Z != right.Z;
    }
    
    public static Int3 operator +(Int3 left, Int3 right)
    {
        return new Int3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }
    
    public static Int3 operator -(Int3 left, Int3 right)
    {
        return new Int3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }
    
    public static Int3 operator *(Int3 left, int right)
    {
        return new Int3(left.X * right, left.Y * right, left.Z * right);
    }
    
    public static Int3 operator /(Int3 left, int right)
    {
        return new Int3(left.X / right, left.Y / right, left.Z / right);
    }
    
    public static Int3 operator %(Int3 left, int right)
    {
        return new Int3(left.X % right, left.Y % right, left.Z % right);
    }
    
    public static Int3 operator +(Int3 left, int right)
    {
        return new Int3(left.X + right, left.Y + right, left.Z + right);
    }
    
    public static Int3 operator -(Int3 left, int right)
    {
        return new Int3(left.X - right, left.Y - right, left.Z - right);
    }
    
    public static Int3 operator *(Int3 left, Int3 right)
    {
        return new Int3(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
    }
    
    public static Int3 operator /(Int3 left, Int3 right)
    {
        return new Int3(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
    }
    
    public static Int3 operator -(Int3 value)
    {
        return new Int3(-value.X, -value.Y, -value.Z);
    }
    
    public static Int3 Abs(Int3 value)
    {
        return new Int3(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z));
    }
    
    public static Int3 Min(Int3 value1, Int3 value2)
    {
        return new Int3(Math.Min(value1.X, value2.X), Math.Min(value1.Y, value2.Y), Math.Min(value1.Z, value2.Z));
    }
    
    public static Int3 Max(Int3 value1, Int3 value2)
    {
        return new Int3(Math.Max(value1.X, value2.X), Math.Max(value1.Y, value2.Y), Math.Max(value1.Z, value2.Z));
    }

    public bool Equals(Int3 other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override bool Equals(object? obj)
    {
        return obj is Int3 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }
}