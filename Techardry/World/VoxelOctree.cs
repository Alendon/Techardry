using System.Numerics;
using System.Runtime.InteropServices;
using BepuUtilities.Memory;

namespace Techardry.World;

public unsafe class VoxelOctree : IDisposable
{
    public const int Dimensions = 32;

    /// <summary>
    /// Depth where the size of one voxel is 1.
    /// </summary>
    public static readonly int SizeOneDepth = (int) Math.Log2(Dimensions);

    /// <summary>
    /// How often a single voxel can be subdivided.
    /// 0 => no subdivision. A Voxel is always 1x1x1.
    /// 1 => 0.5 x 0.5 x 0.5
    /// 2 => 0.25 x 0.25 x 0.25
    /// etc..
    /// </summary>
    public const int MaxSplitCount = 4;

    /// <summary>
    /// Maximum depth of the octree.
    /// </summary>
    public static readonly int MaxDepth = SizeOneDepth + MaxSplitCount;

    /// <summary>
    /// The minimum size of a voxel. Determined by the MaxSplitCount.
    /// </summary>
    public static readonly float MinimumVoxelSize = 1f / MathF.Pow(2, MaxSplitCount);

    public Childs[] TreeLayout;
    public Voxel[] Voxels;

    public VoxelOctree()
    {
        TreeLayout = new Childs[16];
        Voxels = new Voxel[32];
    }

    public void Insert(Voxel voxel, Vector3 position, int subdivision = 0)
    {
        //Check that the voxel is within the bounds of the octree.

        var span = TreeLayout.AsSpan();
        
        var currentNode = TreeLayout[0];

        //Target depth where the voxel will be stored.
        var targetDepth = SizeOneDepth + subdivision;

        for (int i = 0; i <= targetDepth; i++)
        {
            //Calculate the index of the child node. This is based on the position of the voxel.
            float halfSizeCurrentLevel = Dimensions / MathF.Pow(2, i);
            var lowerX = position.X / 2f < halfSizeCurrentLevel;
            var lowerY = position.Y / 2f < halfSizeCurrentLevel;
            var lowerZ = position.Z / 2f < halfSizeCurrentLevel;

            var index = (lowerX ? 0 : 1) + (lowerY ? 0 : 2) + (lowerZ ? 0 : 4);

            if (i == targetDepth)
            {
                //This is the target depth to store the voxel
                
                //If the current node is a branch node, we need to invalidate all children.
                //And mark there data positions as holes if they are not at the end.

                
                //Check if the current node is a leaf node. If yes, just override the value of the node.
                //Maybe move the value position to fill holes.
                
                //Now check if the siblings all have the same value. If yes, merge them.
                break;
            }
            //If the current node is not a leaf (eg a branch), directly go to the child node.
            if (!currentNode.IsLeaf(index))
            {
                currentNode = TreeLayout[index];
                continue;
            }

            //If the current node is a leaf node, it must be changed to a branch node where each child has the value of the current node.
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public struct Childs
    {
        public fixed int Indices[8];
        public byte IsLeafFlag;
        
        public bool IsLeaf(int index)
        {
            return (IsLeafFlag & (1 << index)) != 0;
        }
        
        public void SetLeaf(int index, bool isLeaf)
        {
            if (isLeaf)
            {
                IsLeafFlag |= (byte) (1 << index);
            }
            else
            {
                IsLeafFlag &= (byte) ~(1 << index);
            }
        }
    }
}