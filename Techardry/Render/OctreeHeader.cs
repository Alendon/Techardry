using System.Numerics;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Techardry.Render;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct OctreeHeader
{
    private const int Mat4X4Size = 4 * 4 * sizeof(float);
    private const int Mat3X3Size = 3 * 3 * sizeof(float);

    [UsedImplicitly] [FieldOffset(0)] public TreeType TreeType;

    [UsedImplicitly] [FieldOffset(sizeof(TreeType))]
    public Matrix4x4 InverseTransform;

    [UsedImplicitly] [FieldOffset(sizeof(TreeType) + Mat4X4Size)]
    public Matrix4x4 Transform;

    [UsedImplicitly] [FieldOffset(sizeof(TreeType) + 2 * Mat4X4Size)]
    public fixed float TransposedNormalMatrix[9];

    [UsedImplicitly] [FieldOffset(sizeof(TreeType) + 2 * Mat4X4Size + Mat3X3Size)]
    public uint NodeCount;
}