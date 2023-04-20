using System.Numerics;
using System.Runtime.CompilerServices;

namespace Testing.BvhTest;

public struct Triangle
{
    public Vector3 V0;
    public Vector3 V1;
    public Vector3 V2;

    public Vector3 Center => (V0 + V1 + V2) / 3;
}