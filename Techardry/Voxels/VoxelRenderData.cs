using System.Numerics;
using System.Runtime.InteropServices;

namespace Techardry.Voxels;

[StructLayout(LayoutKind.Explicit)]
public struct VoxelRenderData
{
    [FieldOffset(0)]
    public uint Color;

    [FieldOffset(sizeof(int))]
    //Third component represents array index
    public Vector2 TextureStart;
    
    [FieldOffset(sizeof(int) * 3)]
    public Vector2 TextureSize;
}