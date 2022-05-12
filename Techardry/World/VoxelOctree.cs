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
            UpdateLod(oldLod);
        }
    }

    private Node[] _nodes;

    private const int InitialNodeCapacity = 32;
    private const int InitialDataCapacity = 32;

    private int NodeCapacity
    {
        get => _nodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2(Math.Max(value, InitialNodeCapacity));
            ResizeNodes(alignedSize);
        }
    }

    private (int left, int right) NodeCount;


    private (int[] ownerNodes, Voxel[] voxels) _data;

    private int DataCapacity
    {
        get => _data.ownerNodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2(Math.Max(value, InitialDataCapacity));
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

        _nodes = Array.Empty<Node>();
        _data = (Array.Empty<int>(), Array.Empty<Voxel>());

        NodeCapacity = InitialNodeCapacity;
        DataCapacity = InitialDataCapacity;

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
        };
    }

    private static Voxel GetDefaultVoxel()
    {
        return new Voxel(1);
    }

    private void ResizeNodes(int newCapacity)
    {
        var oldNodes = _nodes;

        var oldLeftNodes = oldNodes.AsSpan(0, NodeCount.left);
        var oldRightNodes = oldNodes.AsSpan(oldNodes.Length - NodeCount.right, NodeCount.right);
        
        _nodes = new Node[newCapacity];
        
        var newLeftNodes = _nodes.AsSpan(0, NodeCount.left);
        var newRightNodes = _nodes.AsSpan(_nodes.Length - NodeCount.right, NodeCount.right);
        
        oldLeftNodes.CopyTo(newLeftNodes);
        oldRightNodes.CopyTo(newRightNodes);
    }

    private void ResizeData(int newCapacity)
    {
        var oldOwners = _data.ownerNodes;
        var oldVoxels = _data.voxels;
        
        var oldLeftOwners = oldOwners.AsSpan(0, DataCount.left);
        var oldRightOwners = oldOwners.AsSpan(oldOwners.Length - DataCount.right, DataCount.right);
        
        var oldLeftVoxels = oldVoxels.AsSpan(0, DataCount.left);
        var oldRightVoxels = oldVoxels.AsSpan(oldVoxels.Length - DataCount.right, DataCount.right);
        
        _data = new(new int[newCapacity], new Voxel[newCapacity]);
        
        var newLeftOwners = _data.ownerNodes.AsSpan(0, DataCount.left);
        var newRightOwners = _data.ownerNodes.AsSpan(_data.ownerNodes.Length - DataCount.right, DataCount.right);
        
        var newLeftVoxels = _data.voxels.AsSpan(0, DataCount.left);
        var newRightVoxels = _data.voxels.AsSpan(_data.voxels.Length - DataCount.right, DataCount.right);
        
        oldLeftOwners.CopyTo(newLeftOwners);
        oldRightOwners.CopyTo(newRightOwners);
        
        oldLeftVoxels.CopyTo(newLeftVoxels);
        oldRightVoxels.CopyTo(newRightVoxels);
    }

    private void UpdateLod(int oldLod)
    {
        UpdateLod(ref GetRootNode(), oldLod);
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

        if ((NodeCount.left + NodeCount.right) * 4 <= NodeCapacity && NodeCapacity > InitialNodeCapacity)
        {
            NodeCapacity = NodeCount.left + NodeCount.right;
        }
        if((DataCount.left + DataCount.right) * 4 <= DataCapacity && DataCapacity > InitialDataCapacity)
        {
            DataCapacity = DataCount.left + DataCount.right;
        }
    }

    private void MergeDataUpwards(ref Node node)
    {
        if(node.Depth == RootNodeDepth)
        {
            return;
        }
        
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
        parent.DataIndex = CreateData(GetDataAlignment(ref parent), parent.Index);
        WriteVoxel(ref parent, data);
        MergeDataUpwards(ref parent);
    }

    private void MergeLodUpwards(ref Node child)
    {
        if (child.Depth == RootNodeDepth)
            return;

        ref var parent = ref GetParentNode(ref child);

        int oldLodDataIndex = parent.DataIndex;
        int oldLodNodeIndex = -1;
        bool oldLodLeftAligned = false;

        //Find the data with the most occurrences.
        int maxCount = 0;
        int maxIndex = 0;

        for (int i = 0; i < ChildCount; i++)
        {
            child = ref GetChildNode(ref parent, i);
            ref readonly var firstVoxel = ref GetVoxel(ref child);

            if (child.DataIndex == oldLodDataIndex)
            {
                oldLodNodeIndex = child.Index;
                oldLodLeftAligned = IsLeftAligned(ref child);
            }

            var count = 0;
            for (int j = 0; j < ChildCount; j++)
            {
                ref var compareChild = ref GetChildNode(ref parent, j);
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

        if (oldLodNodeIndex != -1)
        {
            ref var oldLodNode = ref GetNode(oldLodNodeIndex, oldLodLeftAligned);
            UpdateShareWithParent(ref oldLodNode, false);
        }

        UpdateShareWithParent(ref GetChildNode(ref parent, maxIndex), true);

        parent.DataIndex = GetChildNode(ref parent, maxIndex).DataIndex;

        MergeLodUpwards(ref parent);
    }

    private ref Node UpdateLod(ref Node node, int oldLod)
    {
        //In this method we cannot use the existing GetChildNode method because it will return the child node computed with the new lod.
        //But the nodes gets updated from the parent to the children
        
        var oldNodeLeftAligned = IsLeftAligned(ref node, oldLod);
        var newNodeLeftAligned = IsLeftAligned(ref node);
        
        var moveNode = oldNodeLeftAligned != newNodeLeftAligned;

        if (node.IsLeaf)
        {
            var oldDataLeftAligned = GetDataAlignment(ref node, oldLod);
            var newDataLeftAligned = GetDataAlignment(ref node);
            var moveData = oldDataLeftAligned != newDataLeftAligned;

            if (moveData)
            {
                var data = GetVoxel(node.DataIndex, oldDataLeftAligned);
                DeleteData(oldDataLeftAligned, node.DataIndex, node.Index);
                node.DataIndex = CreateData(newDataLeftAligned, node.Index);

                WriteVoxel(ref node, data);

                ref var upwardsNode = ref node;
                while (upwardsNode.SharesDataWithParent)
                {
                    upwardsNode = ref GetParentNode(ref upwardsNode);
                    upwardsNode.DataIndex = node.DataIndex;
                }
            }
        }
        
        if (moveNode)
        {
            var oldNode = node;

            DeleteNode(node.Index, oldNodeLeftAligned, out _);
            
            var newIndex = newNodeLeftAligned ? NodeCount.left++ : NodeCount.right++;
            node = ref GetNode(newIndex, newNodeLeftAligned);
            node = oldNode;
            node.Index = newIndex;

            ref var parent = ref GetNode(oldNode.ParentIndex, IsLeftAligned(oldNode.Depth - 1));
            parent.ChildIndices[node.ParentChildIndex] = node.Index;
            
            if(!node.IsLeaf)
            {
                for (int i = 0; i < ChildCount; i++)
                {
                    ref var childNode = ref GetNode(node.ChildIndices[i], IsLeftAligned(node.Depth + 1, oldLod));
                    childNode.ParentIndex = node.Index;
                }
            }
        }
        
        
        if (!node.IsLeaf)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                ref var childNode = ref GetNode(node.ChildIndices[i], IsLeftAligned(node.Depth + 1, oldLod));
                childNode = ref UpdateLod(ref childNode, oldLod);
                node = ref GetParentNode(ref childNode);
            }
        }

        return ref node;
    }
    
    private void UpdateShareWithParent(ref Node node, bool shareWithParent)
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
        return ref GetNode(parent.ChildIndices[childIndex], IsLeftAligned(parent.Depth + 1));
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

        bool nodeLeftAligned = IsLeftAligned(ref node);
        int nodeIndex = node.Index;

        for (int i = 0; i < ChildCount; i++)
        {
            var childIndex = node.ChildIndices[i];

            ref var childNode = ref GetNode(node.ChildIndices[i], IsLeftAligned(node.Depth + 1));
            childNode = ref DeleteChildrenAndData(ref childNode, false);
            node = ref GetParentNode(ref childNode);

            DeleteNode(childIndex, IsLeftAligned(ref node), out (int from, int to) movedNode);

            if (movedNode.from != nodeIndex) continue;
            nodeIndex = movedNode.to;
            node = ref GetNode(nodeIndex, nodeLeftAligned);
        }

        node.IsLeaf = true;

        if (firstCall && node.SharesDataWithParent)
        {
            DeleteData(ref node);
            node.DataIndex = InvalidIndex;

            ref var lodReset = ref node;
            while (lodReset.SharesDataWithParent)
            {
                lodReset.SharesDataWithParent = false;
                lodReset.DataIndex = InvalidIndex;
                lodReset = ref GetParentNode(ref lodReset);
            }
        }

        return ref node;
    }

    private void DeleteData(ref Node node)
    {
        DeleteData(GetDataAlignment(ref node), node.DataIndex, node.Index);
    }
    
    private void DeleteData(bool dataLeftAligned, int dataIndex, int ownerIndex)
    {
        var actualDataIndex = dataLeftAligned ? dataIndex : DataCapacity - dataIndex - 1;
        var actualOwnerIndex = dataLeftAligned ? ownerIndex : NodeCapacity - ownerIndex - 1;

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

    private bool IsLeftAligned(ref Node node)
    {
        return IsLeftAligned(node.Depth, inverseLod);
    }

    private bool IsLeftAligned(ref Node node, int lodLevel)
    {
        return IsLeftAligned(node.Depth, lodLevel);
    }

    private bool IsLeftAligned(int depth)
    {
        return IsLeftAligned(depth, inverseLod);
    }

    private bool IsLeftAligned(int depth, int lodLevel)
    {
        return lodLevel >= depth;
    }

    private bool HasChild(ref Node parent)
    {
        return !parent.IsLeaf;
    }

    private ref Node GetChildNode(ref Node parent, Vector3 position)
    {
        var childIndex = GetChildIndex(position, parent.Depth);
        var leftAligned = IsLeftAligned(parent.Depth + 1);
        return ref GetNode(parent.ChildIndices[childIndex], leftAligned);
    }

    private ref Node GetParentNode(ref Node child)
    {
        var parentIndex = child.ParentIndex;
        var leftAligned = IsLeftAligned(child.Depth - 1);
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
                ParentChildIndex = i
            };

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
        if(DataCount.left + DataCount.right >= DataCapacity)
        {
            DataCapacity *= 2;
        }
        
        
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

    private bool GetDataAlignment(ref Node node, int lod)
    {
        if (!node.SharesDataWithParent)
        {
            return IsLeftAligned(ref node, lod);
        }

        return GetDataAlignment(ref GetParentNode(ref node), lod);
    }
    
    private bool GetDataAlignment(ref Node node)
    {
        if (!node.SharesDataWithParent)
        {
            return IsLeftAligned(ref node);
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
            if(NodeCount.left + NodeCount.right + 8 >= NodeCapacity)
            {
                var parentIndex = parent.Index;
                var parentLeftAligned = GetDataAlignment(ref parent);
                
                NodeCapacity *= 2;
                parent = ref GetNode(parentIndex, parentLeftAligned);
            }

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
    }

    public class OctreeDebugView
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
            target.LeftAlignedState = _octree.IsLeftAligned(ref node);
            target.SharesDataWithParent = node.SharesDataWithParent;

            if (node.IsLeaf)
            {
                return;
            }

            target.Children = new NodeDebugView[ChildCount];
            for (byte i = 0; i < ChildCount; i++)
            {
                target.Children[i] = new NodeDebugView();
                ref var child = ref _octree.GetNode(node.ChildIndices[i], _octree.IsLeftAligned(node.Depth + 1));
                FillNodeInfo(target.Children[i], ref child);
            }
        }

        public class NodeDebugView
        {
            public NodeDebugView[]? Children;
            public int Depth;
            public bool SharesDataWithParent;
            public bool LeftAlignedState;

            public Voxel Voxel;
        }
    }
}