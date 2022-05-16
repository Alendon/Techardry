using System.Numerics;
using BepuPhysics;
using BepuUtilities;
using MintyCore.Physics;

namespace Techardry.Utils;

public static class MathHelper
{
    public static bool BoxIntersect((Vector3 Min, Vector3 Max) box, (Vector3 Origin, Vector3 Direction) ray,
        out (float T, Vector3 Normal) result)
    {
        result = (0, Vector3.Zero);
        
        var halfWidth = (box.Max.X - box.Min.X) / 2f;
        var halfHeight = (box.Max.Y - box.Min.Y) / 2f;
        var halfLength = (box.Max.Z - box.Min.Z) / 2f;
        
        var half = new Vector3(halfWidth, halfHeight, halfLength);

        var position =  box.Max - half;


        Vector3 v = ray.Origin - position;
        Vector3 result1 = v;
        Vector3 result2 = ray.Direction;
        Vector3 vector3_1 =
            new Vector3( result2.X < 0.0f ? 1f : -1f,  result2.Y < 0.0f ? 1f : -1f,
                 result2.Z < 0.0f ? 1f : -1f) / Vector3.Max(new Vector3(1E-15f), Vector3.Abs(result2));
        Vector3 vector3_2 = half;
        Vector3 vector3_3 = (result1 - vector3_2) * vector3_1;
        Vector3 vector3_4 = (result1 + vector3_2) * vector3_1;
        Vector3 vector3_5 = Vector3.Min(vector3_3, vector3_4);
        Vector3 vector3_6 = Vector3.Max(vector3_3, vector3_4);
        float num1 =  vector3_6.X <  vector3_6.Y ? vector3_6.X : vector3_6.Y;
        if ( vector3_6.Z <  num1)
            num1 = vector3_6.Z;
        if ( num1 < 0.0f)
        {
            return false;
        }

        float num2;
        if ( vector3_5.X >  vector3_5.Y)
        {
            if ( vector3_5.X >  vector3_5.Z)
            {
                num2 = vector3_5.X;
                result.Normal = Vector3.UnitX;
            }
            else
            {
                num2 = vector3_5.Z;
                result.Normal = Vector3.UnitZ;
            }
        }
        else if ( vector3_5.Y >  vector3_5.Z)
        {
            num2 = vector3_5.Y;
            result.Normal = Vector3.UnitY;
        }
        else
        {
            num2 = vector3_5.Z;
            result.Normal = Vector3.UnitZ;
        }

        if ( num1 <  num2)
        {
            return false;
        }

        result.T =  num2 < 0.0f ? 0.0f : num2;
        if ( Vector3.Dot(result.Normal, v) < 0.0f)
            result.Normal = -result.Normal;
        return true;
    }
}