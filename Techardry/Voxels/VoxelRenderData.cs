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
}