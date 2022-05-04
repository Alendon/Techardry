using System.Numerics;
using System.Runtime.InteropServices;
using BepuUtilities.Memory;
using JetBrains.Annotations;

namespace Techardry.World;

public unsafe class VoxelOctree : IDisposable
{
    /// <summary>
    /// The maximum dimension of the tree in element it can store.
    ///
    /// 
    /// 2 ^ <see cref="MaximumTotalDivision"/>
    /// </summary>
    public const int MaximumTotalDimension = 1024;

    /// <summary>
    /// How often the tree can be subdivided.
    /// This results from the logarithm of the maximum total dimension with the base 2.
    /// </summary>
    public const int MaximumTotalDivision = 10;

    public const int MaximumLevelCount = MaximumTotalDivision + 1;

    public const int Dimensions = 16;

    /// <summary>
    /// The level of the root node.
    /// Starts at -1 as the root node has no data attached to it.
    /// The first level with data is 0.
    /// </summary>
    public const int RootNodeLevel = -1;

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
    public const int MaxSplitCount = 6;

    /// <summary>
    /// Maximum depth of the octree.
    /// </summary>
    public static readonly int MaxDepth = SizeOneDepth + MaxSplitCount;

    /// <summary>
    /// The minimum size of a voxel. Determined by the MaxSplitCount.
    /// </summary>
    public static readonly float MinimumVoxelSize = 1f / MathF.Pow(2, MaxSplitCount);

    public Node[] TreeLayout;


    public Voxel[] Voxels;

    private Stack<int> _layoutHoles = new();
    private Stack<int> _dataHoles = new();

    private SortedSet<int> _layoutHolesSorted = new();
    private SortedSet<int> _dataHolesSorted = new();

    private (int left, int right) _lastFreeLayoutIndex;
    private (int left, int right) _lastFreeDataIndex;


    /// <summary>
    /// The lod of the octree.
    /// Used for optimizing the internal octree layout for rendering.
    /// Must be in range of [0, <see cref="MaximumLevelCount"/>].
    /// 0 means everything is rendered.
    /// <see cref="MaximumLevelCount"/> means only the data referenced in the root node is rendered.
    /// </summary>
    [ValueRange(0, MaximumLevelCount)]
    public int Lod
    {
        get => MaximumLevelCount - inverseLod;
        set => inverseLod = MaximumLevelCount - value;
    }

    /// <summary>
    /// The inverse lod of the octree.
    /// The "normal" lod has the highest value if the level to be rendered is 0.
    /// This inverse normalize this.
    /// </summary>
    private int inverseLod;

    public VoxelOctree()
    {
        TreeLayout = new Node[16];
        Voxels = new Voxel[32];

        Span<int> indices = stackalloc int[8] {-1, -1, -1, -1, -1, -1, -1, -1};
        //Create the root node.
        TreeLayout[0] = new Node(indices, -1, byte.MaxValue, RootNodeLevel);

        _lastFreeLayoutIndex = (1, TreeLayout.Length - 1);
        _lastFreeDataIndex = (1, Voxels.Length - 1);
    }

    /// <summary>
    /// Sort the tree by the current lod level.
    /// 
    /// </summary>
    public void SortByLod()
    {
        //Generate lod data
        //Apply the lod to their respective parents

        AssignLod();

        //compact the layout
    }

    public void Compact()
    {
        throw new NotImplementedException();
    }

    private void AssignLod()
    {
        Span<(Node node, int currentCheckedIndex)> nodeStack =
            stackalloc (Node, int)[MaximumLevelCount];
        nodeStack[0] = (TreeLayout[0], -1);

        var currentDepth = 0;

        //The count of the same occurrences of the same value node.
        //Used inside the loop. Only has 7 elements as the last element is never checked against another.
        Span<int> sameOccurrences = stackalloc int[7];

        while (true)
        {
            ref var currentNode = ref nodeStack[currentDepth].node;
            ref var currentCheckedIndex = ref nodeStack[currentDepth].currentCheckedIndex;
            currentCheckedIndex++;

            //The end of the root node is reached. The lod is determined for each node.
            if (currentCheckedIndex == 8 && currentDepth == 0)
            {
                break;
            }

            //All children are checked
            if (currentCheckedIndex == 8)
            {
                ref var parent = ref nodeStack[currentDepth - 1].node;
                ref var parentCheckedIndex = ref nodeStack[currentDepth - 1].currentCheckedIndex;

                sameOccurrences.Clear();

                //Count the occurrences of each node
                //Only iterate over seven nodes as the last one would only compare to itself.
                for (int i = 0; i < 7; i++)
                {
                    //First check if the node is empty.
                    var isEmpty = parent.LodIndices[i] == -1;
                    if (isEmpty)
                    {
                        //if its empty compare if the other nodes are empty.
                        for (int j = i + 1; j < 8; j++)
                        {
                            if (parent.LodIndices[j] == -1)
                            {
                                sameOccurrences[i]++;
                            }
                        }

                        continue;
                    }

                    //Get the lod index of the parents child i (the sibling of the current node)
                    ref var compareBaseVoxel = ref Voxels[parent.LodIndices[i]];

                    //We start at i + 1 because its useless to compare the current node with itself.
                    for (int j = i + 1; j < 8; j++)
                    {
                        //Compare the lod index with the rest of the parent lod child indices
                        ref var compareVoxel = ref Voxels[parent.LodIndices[j]];

                        if (compareBaseVoxel.Equals(compareVoxel))
                        {
                            sameOccurrences[i]++;
                        }
                    }
                }

                //Get the node with the most same occurrences
                //Or just the first one if they are all the same
                //TODO: Include a "no lod check" option for voxels which should not be used for lod.

                int maxIndex = 0;
                int maxOccurrences = sameOccurrences[0];
                for (int i = 0; i < 7; i++)
                {
                    if (sameOccurrences[i] > maxOccurrences)
                    {
                        maxIndex = i;
                        maxOccurrences = sameOccurrences[i];
                    }
                }

                //maxIndex is the index of the node with the most same occurrences
                parent.LodIndices[parentCheckedIndex] = currentNode.LodIndices[maxIndex];

                //Traverse up
                currentDepth--;
                continue;
            }


            //if the current node is a leaf, just set the lod index to the current data index.
            if (currentNode.IsLeaf(currentCheckedIndex))
            {
                currentNode.LodIndices[currentCheckedIndex] = currentNode.Indices[currentCheckedIndex];
            }
            //If the current node is therefor a branch, traverse into it.
            else
            {
                currentDepth++;
                nodeStack[currentDepth] = (TreeLayout[currentNode.Indices[currentCheckedIndex]], -1);
            }
        }
    }

    /// <summary>
    /// Just insert a voxel
    /// No checks are done.
    /// </summary>
    /// <param name="voxel"></param>
    /// <param name="position"></param>
    /// <param name="level"></param>
    public void Insert(Voxel voxel, Vector3 position, [ValueRange(MaximumTotalDivision)] int level)
    {
        //Ensure that enough space is available to add the voxel at the specified layer
        //There must be at least 1 + layer elements available in the tree layout array.
        //And at least 1 + (layer * 8) elements available in the voxels array.
        //EnsureCapacity(layer);

        //start with the root node
        ref var currentNode = ref TreeLayout[0];
        int currentNodeIndex = 0;
        var currentLevel = 0;
        while (currentLevel != level)
        {
            //calculate the index of the child node

            var childIndex = PositionToIndex(position, currentLevel);

            //if the children at the index is a leaf, change it to a branch
            if (currentNode.IsLeaf(childIndex))
            {
                ConvertChildToBranch(ref currentNode, currentNodeIndex, childIndex, currentLevel);
            }

            //traverse into the child node

            currentLevel++;
        }

        //Traverse back towards the root node and check if the data or the lod can be merged.
    }

    /// <summary>
    /// Changes the node to a branch node.
    /// </summary>
    /// <param name="node"></param>
    private void ConvertChildToBranch(ref Node node, int nodeIndex, int childIndex, int level)
    {
        var dataIndex = node.Indices[childIndex];

        ref var childNode = ref CreateEmptyNode(level, out var childNodeIndex);
        
        childNode.ParentIndex = nodeIndex;
        childNode.IsLeafFlag = byte.MaxValue;

        node.Indices[childIndex] = childNodeIndex;
        node.SetLeaf(childIndex, false);
        
        //Apply the data to the child node
        childNode.Indices[0] = dataIndex;
        childNode.LodIndices[0] = node.LodIndices[childIndex];
        
        for (var i = 1; i < 8; i++)
        {
            var newDataIndex = ReserveDataIndex(IsIncludedInLod(level + 1));
            childNode.Indices[i] = newDataIndex;
            childNode.LodIndices[i] = newDataIndex;
            
            Voxels[newDataIndex] = Voxels[dataIndex];
        }
    }

    private ref Node CreateEmptyNode(int level, out int nodeIndex)
    {
        //if the node is contained in the lod, it will be aligned to the left side of the internal array.
        var leftAligned = IsIncludedInLod(level);
        nodeIndex = ReserveNodeIndex(leftAligned);
        
        ref var result = ref TreeLayout[nodeIndex];
        result = default;
        return ref result;
    }

    /// <summary>
    /// Check if the given level is rendered with the current set lod.
    /// </summary>
    /// <param name="level"></param>
    /// <returns>True if included</returns>
    private bool IsIncludedInLod(int level)
    {
        return level <= inverseLod;
    }

    private int ReserveNodeIndex(bool leftAligned)
    {
        if (leftAligned)
        {
            var potentialHole = _layoutHolesSorted.Min;
            
            //if the potential hole is 0, the holes collection is empty.
            //if the potential hole is greater than the last free index, the hole is a right aligned hole.
            if (potentialHole == 0 || potentialHole > _lastFreeLayoutIndex.left)
            {
                _lastFreeLayoutIndex.left += 1;
                return _lastFreeLayoutIndex.left;
            }

            _layoutHolesSorted.Remove(potentialHole);
            return potentialHole;
        }
        else
        {
            var potentialHole = _layoutHolesSorted.Max;
            
            //if the potential hole is 0, the holes collection is empty.
            //if the potential hole is smaller than the last free index, the hole is a left aligned hole.
            if (potentialHole == 0 || potentialHole < _lastFreeLayoutIndex.right)
            {
                _lastFreeLayoutIndex.right -= 1;
                return _lastFreeLayoutIndex.right;
            }

            _layoutHolesSorted.Remove(potentialHole);
            return potentialHole;
        }
    }

    private int ReserveDataIndex(bool leftAligned)
    {
        if (leftAligned)
        {
            var potentialHole = _dataHolesSorted.Min;
            
            //if the potential hole is 0, the holes collection is empty.
            //if the potential hole is greater than the last free index, the hole is a right aligned hole.
            if (potentialHole == 0 || potentialHole > _lastFreeDataIndex.left)
            {
                _lastFreeDataIndex.left += 1;
                return _lastFreeDataIndex.left;
            }
            
            _dataHolesSorted.Remove(potentialHole);
            return potentialHole;
        }
        else
        {
            var potentialHole = _dataHolesSorted.Max;
            
            //if the potential hole is 0, the holes collection is empty.
            //if the potential hole is smaller than the last free index, the hole is a left aligned hole.
            if (potentialHole == 0 || potentialHole < _lastFreeDataIndex.right)
            {
                _lastFreeDataIndex.right -= 1;
                return _lastFreeDataIndex.right;
            }
            
            _dataHolesSorted.Remove(potentialHole);
            return potentialHole;
        }
    }

    private static int PositionToIndex(Vector3 position, int currentLayer)
    {
        float sizeCurrentLayer = Dimensions / MathF.Pow(2, currentLayer);
        float halfSizeCurrentLayer = sizeCurrentLayer / 2;

        var adjustedX = position.X % sizeCurrentLayer;
        var lowerX = adjustedX < halfSizeCurrentLayer;

        var adjustedY = position.Y % sizeCurrentLayer;
        var lowerY = adjustedY < halfSizeCurrentLayer;

        var adjustedZ = position.Z % sizeCurrentLayer;
        var lowerZ = adjustedZ < halfSizeCurrentLayer;

        return (lowerX ? 0 : 1) + (lowerY ? 0 : 2) + (lowerZ ? 0 : 4);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private struct DataNode
    {
        public int NodeIndex;

        /// <summary>
        /// The child index at the parent (0-7).
        /// </summary>
        public byte ParentIndex;
    }

    public struct Node
    {
        public Node(Span<int> indices, int parentIndex, byte isLeafFlag, int level)
        {
            for (int i = 0; i < 8; i++)
            {
                Indices[i] = indices[i];
                LodIndices[i] = -1;
            }

            ParentIndex = parentIndex;
            IsLeafFlag = isLeafFlag;
        }

        public fixed int Indices[8];
        public fixed int LodIndices[8];

        public int ParentIndex;
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