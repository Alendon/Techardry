using System.Runtime.InteropServices;
using Silk.NET.Maths;

namespace Techardry.Voxels;

[StructLayout(LayoutKind.Explicit)]
public struct VoxelRenderData
{
    [FieldOffset(0)]
    public uint Color;

    [FieldOffset(sizeof(int))]
    //Third component represents array index
    public Vector3D<uint> TextureStart;
    
    [FieldOffset(sizeof(int) * 4)]
    public Vector2D<uint> TextureSize;
}