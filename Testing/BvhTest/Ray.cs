using System.Numerics;

namespace Testing.BvhTest;

public struct Ray
{
    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = direction;
        InverseDirection = Vector3.One / direction;
        T = float.MaxValue;
    }

    public Vector3 Origin;
    public Vector3 Direction;
    public Vector3 InverseDirection;

    public float T;
}