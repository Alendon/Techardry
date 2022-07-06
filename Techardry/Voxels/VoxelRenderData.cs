using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace Techardry.Voxels;

[StructLayout(LayoutKind.Explicit)]
public struct VoxelRenderData
{
    [FieldOffset(0)]
    public Vector4 Color;

    [FieldOffset(sizeof(float) * 4)]
    public int NotEmpty;

    [FieldOffset(sizeof(float) * 5)]
    //Third component represents array index
    public Vector3 TextureStart;
    
    [FieldOffset(sizeof(float) * 8)]
    public Vector2 TextureSize;
}