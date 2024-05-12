using System.Numerics;
using System.Runtime.InteropServices;
using MintyCore.Graphics.Utils;

namespace Techardry.Render;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 3 * 4 * sizeof(uint))]
public struct BvhTriangle
{
    public Vector3 Vertex0;
    public uint Uv0;
    
    public Vector3 Vertex1;
    public uint Uv1;
    
    public Vector3 Vertex2;
    public uint Uv2;
}