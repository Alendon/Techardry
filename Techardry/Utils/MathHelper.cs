using System.Numerics;
using MintyCore.Physics;

namespace Techardry.Utils;

public static class MathHelper
{
    public static bool BoxIntersect((Vector3 Min, Vector3 Max) box, (Vector3 Origin, Vector3 Direction) ray,
        out (float TMin, float TMax, Vector3 normal) hitResult)
    {
        var inverseDirection = Vector3.One / ray.Direction;

        float t1 = (box.Min.X - ray.Origin.X) * inverseDirection.X;
        float t2 = (box.Max.X - ray.Origin.X) * inverseDirection.X;
        float t3 = (box.Min.Y - ray.Origin.Y) * inverseDirection.Y;
        float t4 = (box.Max.Y - ray.Origin.Y) * inverseDirection.Y;
        float t5 = (box.Min.Z - ray.Origin.Z) * inverseDirection.Z;
        float t6 = (box.Max.Z - ray.Origin.Z) * inverseDirection.Z;

        float tmin = Math.Max(Math.Max(Math.Min(t1, t2), Math.Min(t3, t4)), Math.Min(t5, t6));
        float tmax = Math.Min(Math.Min(Math.Max(t1, t2), Math.Max(t3, t4)), Math.Max(t5, t6));

        if (tmax < 0)
        {
            hitResult = (0, 0, Vector3.Zero);
            return false;
        }

        if (tmin > tmax)
        {
            hitResult = (0, 0, Vector3.Zero);
            return false;
        }

        hitResult = (tmin, tmax, Vector3.One);

        if (tmin.Equals(t1))
        {
            hitResult.normal = Vector3.UnitX;
        }
        else if (tmin.Equals(t2))
        {
            hitResult.normal = -Vector3.UnitX;
        }
        else if (tmin.Equals(t3))
        {
            hitResult.normal = Vector3.UnitY;
        }
        else if (tmin.Equals(t4))
        {
            hitResult.normal = -Vector3.UnitY;
        }
        else if (tmin.Equals(t5))
        {
            hitResult.normal = Vector3.UnitZ;
        }
        else if (tmin.Equals(t6))
        {
            hitResult.normal = -Vector3.UnitZ;
        }

        return true;
    }
}