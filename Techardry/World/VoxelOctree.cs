using System.Diagnostics;
using System.Numerics;
using JetBrains.Annotations;
using MintyCore.Utils.Maths;

namespace Techardry.World;

[DebuggerTypeProxy(typeof(OctreeDebugView))]
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

    public const int ChildCount = 8;

    public const int InvalidIndex = -1;

    public const byte RootNodeDepth = 0;

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
        set
        {
            var oldLod = inverseLod;
            inverseLod = MaximumLevelCount - value;
            UpdateLod(oldLod, inverseLod);
        }
    }

    private Node[] _nodes;

    private int NodeCapacity
    {
        get => _nodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2(value);
            ResizeNodes(alignedSize);
        }
    }

    private (int left, int right) NodeCount;


    private (int[] ownerNodes, Voxel[] voxels) _data;

    private int DataCapacity
    {
        get => _nodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2(value);
            ResizeData(alignedSize);
        }
    }

    private (int left, int right) DataCount;


    /// <summary>
    /// The inverse lod of the octree.
    /// The "normal" lod has the highest value if the level to be rendered is 0.
    /// This inverse normalize this.
    /// </summary>
    private int inverseLod;


    public VoxelOctree([ValueRange(0, MaximumLevelCount)] int inverseLod)
    {
        this.inverseLod = inverseLod;
        _nodes = new Node[128];
        _data = new(new int[256], new Voxel[256]);

        DataCount = (1, 0);
        _data.voxels[0] = GetDefaultVoxel();
        _data.ownerNodes[0] = 0;

        NodeCount = (1, 0);
        _nodes[0] = new Node
        {
            Depth = RootNodeDepth,
            Index = 0,
            DataIndex = 0,
            SharesDataWithParent = false,
            IsLeaf = true,
            ParentIndex = InvalidIndex,
            LeftAlignedState = LeftAligned.Current
        };
    }

    private static Voxel GetDefaultVoxel()
    {
        return new Voxel(1);
    }

    private void ResizeNodes(int newCapacity)
    {
        throw new NotImplementedException();
    }

    private void ResizeData(int newCapacity)
    {
        throw new NotImplementedException();
    }

    private void UpdateLod(int oldLod, int newLod)
    {
        throw new NotImplementedException();
    }

    public void Remove(Vector3 position, [ValueRange(0, MaximumTotalDivision)] int depth)
    {
        Insert(GetDefaultVoxel(), position, depth);
    }

    public void Insert(in Voxel voxel, Vector3 position, [ValueRange(0, MaximumTotalDivision)] int depth)
    {
        //Traverse the tree to the correct depth.

        ref var node = ref GetRootNode();

        while (node.Depth != depth)
        {
            node = ref GetOrCreateChild(ref node, position);
        }

        node = ref DeleteChildrenAndData(ref node);
        node.DataIndex = CreateData(GetDataAlignment(ref node), node.Index);
        WriteVoxel(ref node, voxel);

        MergeDataUpwards(ref node);
    }

    private void MergeDataUpwards(ref Node node)
    {
        ref var parent = ref GetParentNode(ref node);

        ref readonly var nodeVoxel = ref GetVoxel(ref node);
        bool allEqual = true;

        for (int i = 0; i < ChildCount && allEqual; i++)
        {
            ref var compareChild = ref GetChildNode(ref node, i);
            ref readonly var compareVoxel = ref GetVoxel(ref compareChild);
            allEqual &= nodeVoxel.Equals(compareVoxel);
        }

        if (!allEqual)
        {
            MergeLodUpwards(ref node);
            return;
        }

        var data = nodeVoxel;

        parent = ref DeleteChildrenAndData(ref parent);
        WriteVoxel(ref parent, data);
        MergeDataUpwards(ref parent);
    }

    private void MergeLodUpwards(ref Node node)
    {
        if (node.Depth == RootNodeDepth)
            return;

        ref var parent = ref GetParentNode(ref node);
        var oldVoxelIndex = node.DataIndex;
        var oldVoxelAlignment = GetDataAlignment(ref node);
        ref readonly var oldLodVoxel = ref GetVoxel(oldVoxelIndex, oldVoxelAlignment);

        //Find the data with the most occurrences.
        int maxCount = 0;
        int maxIndex = oldVoxelIndex;

        for (int i = 0; i < ChildCount; i++)
        {
            if (GetVoxel(ref GetChildNode(ref parent, i)).Equals(oldLodVoxel))
            {
                maxCount++;
            }
        }

        for (int i = 0; i < ChildCount; i++)
        {
            ref var child = ref GetChildNode(ref node, i);
            ref readonly var firstVoxel = ref GetVoxel(ref child);

            var count = 0;
            for (int j = 0; j < ChildCount; j++)
            {
                ref var compareChild = ref GetChildNode(ref node, j);
                ref readonly var compareVoxel = ref GetVoxel(ref compareChild);

                if (firstVoxel.Equals(compareVoxel))
                {
                    count++;
                }
            }

            if (count <= maxCount) continue;
            maxCount = count;
            maxIndex = i;
        }

        if (oldVoxelIndex == maxIndex)
        {
            MoveVoxelByLod(ref node, true);
            return;
        }

        MoveVoxelByLod(ref node, false);
        MoveVoxelByLod(ref GetChildNode(ref parent, maxIndex), true);
        MergeLodUpwards(ref parent);
    }

    private void MoveVoxelByLod(ref Node node, bool shareWithParent)
    {
        var oldLeftAlignedState = GetDataAlignment(ref node);
        var data = GetVoxel(ref node);

        node.SharesDataWithParent = shareWithParent;
        var newLeftAlignedState = GetDataAlignment(ref node);

        if (oldLeftAlignedState == newLeftAlignedState)
            return;

        DeleteData(ref node);

        node.DataIndex = CreateData(newLeftAlignedState, node.Index);
        SetData(ref node, data);

        ref var upwardsNode = ref node;
        while (upwardsNode.SharesDataWithParent)
        {
            upwardsNode = ref GetParentNode(ref upwardsNode);
            upwardsNode.DataIndex = node.DataIndex;
        }
    }


    private ref Node GetChildNode(ref Node parent, int childIndex)
    {
        return ref GetNode(parent.ChildIndices[childIndex], (parent.LeftAlignedState & LeftAligned.Child) != 0);
    }

    /// <summary>
    /// Delete all children of a node.
    /// </summary>
    /// <param name="node"></param>
    /// <returns>Reference to the original node, as it may be moved in data</returns>
    private ref Node DeleteChildrenAndData(ref Node node, bool firstCall = true)
    {
        if (node.IsLeaf)
        {
            DeleteData(ref node);
            node.DataIndex = InvalidIndex;
            return ref node;
        }

        bool nodeLeftAligned = (node.LeftAlignedState & LeftAligned.Current) != 0;
        int nodeIndex = node.Index;

        for (int i = 0; i < ChildCount; i++)
        {
            var childIndex = node.ChildIndices[i];

            ref var childNode = ref GetNode(node.ChildIndices[i], (node.LeftAlignedState & LeftAligned.Child) != 0);
            childNode = ref DeleteChildrenAndData(ref childNode, false);
            node = ref GetParentNode(ref childNode);

            DeleteNode(childIndex, (node.LeftAlignedState & LeftAligned.Child) != 0, out (int from, int to) movedNode);

            if (movedNode.from != nodeIndex) continue;
            nodeIndex = movedNode.to;
            node = ref GetNode(nodeIndex, nodeLeftAligned);
        }

        node.IsLeaf = true;

        if (firstCall && node.SharesDataWithParent)
        {
            node.DataIndex = CreateData((node.LeftAlignedState & LeftAligned.Current) != 0, node.Index);
            node.SharesDataWithParent = false;
            SetData(ref node,GetDefaultVoxel());
            MergeLodUpwards(ref node);
        }

        return ref node;
    }

    private void DeleteData(ref Node node)
    {
        var dataLeftAligned = GetDataAlignment(ref node);
        var dataIndex = node.DataIndex;

        var actualDataIndex = dataLeftAligned ? dataIndex : DataCapacity - dataIndex - 1;
        var actualOwnerIndex = dataLeftAligned ? node.Index : NodeCapacity - node.Index - 1;

        var dataOwner = _data.ownerNodes[actualDataIndex];

        if (dataOwner != actualOwnerIndex) return;

        int replaceIndex;
        if (dataLeftAligned)
        {
            replaceIndex = --DataCount.left;
        }
        else
        {
            replaceIndex = --DataCount.right;
            replaceIndex = DataCapacity - replaceIndex - 1;
        }

        _data.voxels[actualDataIndex] = _data.voxels[replaceIndex];
        _data.ownerNodes[actualDataIndex] = _data.ownerNodes[replaceIndex];

        var ownerToInformIndex = _data.ownerNodes[actualDataIndex];
        ref var ownerToInform = ref GetNode(ownerToInformIndex, dataLeftAligned);
        do
        {
            ownerToInform.DataIndex = actualDataIndex;
            ownerToInform = ref GetParentNode(ref ownerToInform);
        } while (ownerToInform.SharesDataWithParent);
    }

    private void DeleteNode(int index, bool leftAligned, out (int from, int to) movedNode)
    {
        int newNodeCount;
        if (leftAligned)
            newNodeCount = --NodeCount.left;
        else
            newNodeCount = --NodeCount.right;

        //If the deleted node is the last one, we don't need to move anything.
        if (newNodeCount == index)
        {
            movedNode = (-1, -1);
            return;
        }

        //Move the last node to the deleted node's position.
        var indexToMove = newNodeCount;
        GetNode(index, leftAligned) = GetNode(indexToMove, leftAligned);
        movedNode = (indexToMove, index);
    }

    private bool HasChild(ref Node parent)
    {
        return !parent.IsLeaf;
    }

    private ref Node GetChildNode(ref Node parent, Vector3 position)
    {
        var childIndex = GetChildIndex(position, parent.Depth);
        var leftAligned = (parent.LeftAlignedState & LeftAligned.Child) != 0;
        return ref GetNode(parent.ChildIndices[childIndex], leftAligned);
    }

    private ref Node GetParentNode(ref Node child)
    {
        var parentIndex = child.ParentIndex;
        var leftAligned = (child.LeftAlignedState & LeftAligned.Parent) != 0;
        return ref GetNode(parentIndex, leftAligned);
    }

    private ref Node GetNode(int index, bool leftAligned)
    {
        return ref leftAligned ? ref _nodes[index] : ref _nodes[NodeCapacity - index - 1];
    }

    private void CreateChildren(ref Node parent)
    {
        byte childrenDepth = (byte) (parent.Depth + 1);
        var childrenLeftAligned = IsIncludedInLod(childrenDepth);

        parent.LeftAlignedState = childrenLeftAligned
            ? parent.LeftAlignedState | LeftAligned.Child
            : parent.LeftAlignedState & ~LeftAligned.Child;
        parent.IsLeaf = false;

        for (byte i = 0; i < ChildCount; i++)
        {
            var childIndex = childrenLeftAligned ? NodeCount.left++ : NodeCount.right++;
            parent.ChildIndices[i] = childIndex;

            ref var child = ref GetNode(childIndex, childrenLeftAligned);
            child = new()
            {
                Depth = childrenDepth,
                Index = childIndex,
                IsLeaf = true,
                ParentIndex = parent.Index,
                ParentChildIndex = i,
                LeftAlignedState = 0
            };
            if ((parent.LeftAlignedState & LeftAligned.Current) != 0)
            {
                child.LeftAlignedState |= LeftAligned.Parent;
            }

            if (childrenLeftAligned)
            {
                child.LeftAlignedState |= LeftAligned.Current;
            }

            //The first children "inherit" the parents data.
            if (i == 0)
            {
                child.DataIndex = parent.DataIndex;
                child.SharesDataWithParent = true;
                SetDataOwner(ref child);
                continue;
            }

            child.SharesDataWithParent = false;
            child.DataIndex = CreateData(childrenLeftAligned, childIndex);
            SetData(ref child, GetVoxel(ref parent));
        }
    }

    private void SetData(ref Node node, in Voxel voxel)
    {
        var leftAligned = GetDataAlignment(ref node);
        SetData(node.DataIndex, leftAligned, voxel);
    }

    private void SetData(int dataIndex, bool leftAligned, in Voxel voxel)
    {
        var actualIndex = leftAligned ? dataIndex : DataCapacity - dataIndex - 1;
        WriteVoxel(dataIndex, leftAligned, voxel);
    }

    private int CreateData(bool leftAligned, int ownerIndex)
    {
        var dataIndex = leftAligned ? DataCount.left++ : DataCount.right++;
        SetDataOwner(dataIndex, leftAligned, ownerIndex);
        return dataIndex;
    }

    private void SetDataOwner(ref Node node)
    {
        var dataAlignment = GetDataAlignment(ref node);
        SetDataOwner(node.DataIndex, dataAlignment, node.Index);
    }

    private void SetDataOwner(int dataIndex, bool leftAligned, int ownerIndex)
    {
        var actualDataIndex = leftAligned ? dataIndex : DataCapacity - dataIndex - 1;
        ref var ownerRef = ref _data.ownerNodes[actualDataIndex];

        var actualOwnerIndex = leftAligned ? ownerIndex : NodeCapacity - ownerIndex - 1;
        ownerRef = actualOwnerIndex;
    }

    private bool GetDataAlignment(ref Node node)
    {
        if (!node.SharesDataWithParent)
        {
            return (node.LeftAlignedState & LeftAligned.Current) != 0;
        }

        return GetDataAlignment(ref GetParentNode(ref node));
    }

    private ref readonly Voxel GetVoxel(ref Node node)
    {
        return ref GetVoxel(node.DataIndex, GetDataAlignment(ref node));
    }

    private ref readonly Voxel GetVoxel(int index, bool leftAligned)
    {
        return ref GetVoxelRef(index, leftAligned);
    }

    private void WriteVoxel(ref Node node, in Voxel voxel)
    {
        WriteVoxel(node.DataIndex, GetDataAlignment(ref node), voxel);
    }

    private void WriteVoxel(int index, bool leftAligned, in Voxel voxel)
    {
        GetVoxelRef(index, leftAligned) = voxel;
    }

    private ref Voxel GetVoxelRef(int index, bool leftAligned)
    {
        return ref leftAligned ? ref _data.voxels[index] : ref _data.voxels[DataCapacity - index - 1];
    }
    
    private ref Node GetOrCreateChild(ref Node parent, Vector3 position)
    {
        if (!HasChild(ref parent))
        {
            CreateChildren(ref parent);
        }

        return ref GetChildNode(ref parent, position);
    }

    private ref Node GetRootNode()
    {
        return ref _nodes[0];
    }

    /// <summary>
    /// Check if the given depth is rendered with the current set lod.
    /// </summary>
    /// <param name="depth"></param>
    /// <returns>True if included</returns>
    private bool IsIncludedInLod(int depth)
    {
        return depth <= inverseLod;
    }


    private static byte GetChildIndex(Vector3 position, int depth)
    {
        float sizeCurrentLayer = Dimensions / MathF.Pow(2, depth);
        float halfSizeCurrentLayer = sizeCurrentLayer / 2;

        var adjustedX = position.X % sizeCurrentLayer;
        var lowerX = adjustedX < halfSizeCurrentLayer;

        var adjustedY = position.Y % sizeCurrentLayer;
        var lowerY = adjustedY < halfSizeCurrentLayer;

        var adjustedZ = position.Z % sizeCurrentLayer;
        var lowerZ = adjustedZ < halfSizeCurrentLayer;

        return (byte) ((lowerX ? 0 : 1) + (lowerY ? 0 : 2) + (lowerZ ? 0 : 4));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        DisposeCore();
    }

    private void DisposeCore()
    {
    }

    private struct Node
    {
        public fixed int ChildIndices[8];
        public int DataIndex;
        public int Index;

        public int ParentIndex;
        public byte ParentChildIndex;
        public bool IsLeaf;
        public bool SharesDataWithParent;
        public byte Depth;

        public LeftAligned LeftAlignedState;
    }

    [Flags]
    private enum LeftAligned : byte
    {
        Parent = 1 << 0,
        Current = 1 << 1,
        Child = 1 << 2
    }

    private class OctreeDebugView
    {
        public NodeDebugView? RootNode { get; }
        private VoxelOctree _octree;

        public OctreeDebugView(VoxelOctree octree)
        {
            _octree = octree;
            ref var root = ref octree.GetRootNode();

            RootNode = new NodeDebugView();
            FillNodeInfo(RootNode, ref root);
        }

        private void FillNodeInfo(NodeDebugView target, ref Node node)
        {
            target.Depth = node.Depth;
            target.Voxel = _octree.GetVoxel(ref node);
            target.LeftAlignedState = node.LeftAlignedState;
            target.SharesDataWithParent = node.SharesDataWithParent;

            if (node.IsLeaf)
            {
                return;
            }

            target.Children = new NodeDebugView[ChildCount];
            for (byte i = 0; i < ChildCount; i++)
            {
                target.Children[i] = new NodeDebugView();
                ref var child = ref _octree.GetNode(node.ChildIndices[i],
                    (node.LeftAlignedState & LeftAligned.Child) != 0);
                FillNodeInfo(target.Children[i], ref child);
            }
        }

        public class NodeDebugView
        {
            public NodeDebugView[]? Children;
            public int Depth;
            public bool SharesDataWithParent;
            public LeftAligned LeftAlignedState;

            public Voxel Voxel;
        }
    }
}