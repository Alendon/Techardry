using System.Numerics;

namespace Techardry.Utils;

public static class MathHelper
{
    public static bool BoxIntersect((Vector3 Min, Vector3 Max) box, (Vector3 Origin, Vector3 Direction) ray,
        out (float T, Vector3 Normal) hitResult)
    {
        //This implementation is directly copied from the "BepuPhysics.Collidables.Box.RayTest" method.
        //https://github.com/bepu/bepuphysics2/blob/master/BepuPhysics/Collidables/Box.cs

        var halfExtent = (box.Max - box.Min) / 2;
        var position = box.Min + halfExtent;
        var offset = ray.Origin - position;

        var offsetToTScale =
            new Vector3(ray.Direction.X < 0 ? 1 : -1, ray.Direction.Y < 0 ? 1 : -1, ray.Direction.Z < 0 ? 1 : -1) /
            Vector3.Max(new Vector3(1e-15f), Vector3.Abs(ray.Direction));
        
        var negativeT = (offset - halfExtent) * offsetToTScale;
        var positiveT = (offset + halfExtent) * offsetToTScale;
        var entryT = Vector3.Min(negativeT, positiveT);
        var exitT = Vector3.Max(negativeT, positiveT);
        
        var earliestExit = exitT.X < exitT.Y ? exitT.X : exitT.Y;
        if(exitT.Z < earliestExit)
            earliestExit = exitT.Z;
        
        if(earliestExit < 0)
        {
            hitResult = default;
            return false;
        }

        float latestEntry;
        if (entryT.X > entryT.Y)
        {
            if (entryT.X > entryT.Z)
            {
                latestEntry = entryT.X;
                hitResult.Normal = Vector3.UnitX;
            }
            else
            {
                latestEntry = entryT.Z;
                hitResult.Normal = Vector3.UnitZ;
            }
        }
        else
        {
            if (entryT.Y > entryT.Z)
            {
                latestEntry = entryT.Y;
                hitResult.Normal = Vector3.UnitY;
            }
            else
            {
                latestEntry = entryT.Z;
                hitResult.Normal = Vector3.UnitZ;
            }
        }

        if (earliestExit < latestEntry)
        {
            hitResult = default;
            return false;
        }
        hitResult.T = latestEntry < 0 ? 0 : latestEntry;
        
        if(Vector3.Dot(hitResult.Normal, offset) < 0)
            hitResult.Normal = -hitResult.Normal;
        return true;
    }
}