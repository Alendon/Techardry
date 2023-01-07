using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MintyCore.Utils;
using MintyCore.Utils.Maths;
using Techardry.Identifications;
using TechardryMath = Techardry.Utils.MathHelper;

namespace Techardry.Voxels;

[DebuggerTypeProxy(typeof(OctreeDebugView))]
public class VoxelOctree
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

    public const uint InvalidIndex = uint.MaxValue;

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

    internal Node[] Nodes;

    private const int InitialNodeCapacity = 32;
    private const int InitialDataCapacity = 32;

    internal uint NodeCapacity
    {
        get => (uint) Nodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2((int) Math.Max(value, InitialNodeCapacity));
            ResizeNodes(alignedSize);
        }
    }

    /// <summary>
    /// Getter/Setter for the node count
    /// Does automatically adjust the node capacity
    /// </summary>
    internal uint NodeCount
    {
        get => _nodeCount;
        set
        {
            if (value > NodeCapacity)
                NodeCapacity = value;

            if (value * 4 < NodeCapacity)
                NodeCapacity = value * 2;

            _nodeCount = value;
        }
    }


    internal ( uint[] ownerNodes, VoxelData[] voxels, VoxelPhysicsData[] physicsData,
        VoxelRenderData[] renderData) Data;

    internal uint DataCapacity
    {
        get => (uint) Data.ownerNodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2((int) Math.Max(value, InitialDataCapacity));
            ResizeData(alignedSize);
        }
    }

    internal uint DataCount
    {
        get => _dataCount;
        set
        {
            if (value > DataCapacity)
                DataCapacity = value;

            if (value * 4 < DataCapacity)
                DataCapacity = value * 2;

            _dataCount = value;
        }
    }

    private uint _nodeCount;
    private uint _dataCount;


    public VoxelOctree()
    {
        Nodes = Array.Empty<Node>();
        Data = (Array.Empty<uint>(), Array.Empty<VoxelData>(), Array.Empty<VoxelPhysicsData>(),
            Array.Empty<VoxelRenderData>());

        NodeCapacity = InitialNodeCapacity;
        DataCapacity = InitialDataCapacity;

        NodeCount = 1;
        Nodes[0] = new Node
        {
            Depth = RootNodeDepth,
            Index = 0,
            DataIndex = InvalidIndex,
            ParentIndex = InvalidIndex,
        };
        Nodes[0].SetChildIndex(InvalidIndex);
    }

    public void Insert(VoxelData data, Vector3 position, int depth)
    {
        ValidateTree();

        ref var node = ref GetOrCreateNode(position, depth);

        ValidateTree();

        if (!node.IsLeaf)
        {
            node = ref DeleteChildren(ref node);

            ValidateTree();
        }

        SetData(ref node, data);

        ValidateTree();

        if (position == new Vector3(1, 7, 13))
        {
        }

        MergeUpwards(ref node);

        ValidateTree();
    }

    [Conditional("DEBUG")]
    private void ValidateTree()
    {
        ValidateTreeInner(ref GetRootNode());
        
        for (var i = NodeCount; i < Nodes.Length; i++)
        {
            Logger.AssertAndThrow(Nodes[i] == (Node) default, "Node array not cleared", "VoxelOctree");
        }
        
        for (var i = 0; i < NodeCount; i++)
        {
            ref var node = ref Nodes[i];
            Logger.AssertAndThrow(node.Index == i, "Node index mismatch", "VoxelOctree");
        }

        for (var i = 0; i < DataCount; i++)
        {
            var nodeIndex = Data.ownerNodes[i];
            ref var node = ref Nodes[nodeIndex];
            Logger.AssertAndThrow(node.DataIndex == i, "Data owner node index mismatch", "VoxelOctree");
        }
    }

    [Conditional("DEBUG")]
    private void ValidateTreeInner(ref Node parentNode)
    {
        // check if the parent node is a not a leaf and has data
        if (parentNode.IsLeaf) return;

        Logger.AssertAndThrow(parentNode.IsEmpty, "Parent node is not a leaf but has data.", "VoxelOctree");

        for (byte i = 0; i < ChildCount; i++)
        {
            var childIndex = parentNode.GetChildIndex(i);
            ref var child = ref GetChildNode(ref parentNode, i);

            Logger.AssertAndThrow(childIndex == child.Index, "Child index does not match.", "VoxelOctree");
            Logger.AssertAndThrow(parentNode.Index == child.ParentIndex, "Parent index does not match.", "VoxelOctree");
            Logger.AssertAndThrow(parentNode.Depth + 1 == child.Depth, "Child depth does not match.", "VoxelOctree");

            ValidateTreeInner(ref child);
        }
    }

    /// <summary>
    /// Merge nodes upwards based on the given Leaf Node
    /// </summary>
    private void MergeUpwards(ref Node node)
    {
        //A node which is not a leaf can not be merged up
        if (!node.IsLeaf)
            return;

        //if the parent index is invalid the current node is the root node.
        //therefor there is no additional merging
        if (node.ParentIndex == InvalidIndex)
            return;

        var compareVoxel = GetVoxelData(ref node);

        ref var parentNode = ref GetParentNode(ref node);

        int itCount = 0;
        while (true)
        {
            for (byte childIndex = 0; childIndex < ChildCount; childIndex++)
            {
                ref var childNode = ref GetChildNode(ref parentNode, childIndex);

                //check if the child node is a leaf and the associated data is equal
                //if not we can stop merging
                if (!childNode.IsLeaf || GetVoxelData(ref childNode) != compareVoxel)
                    return;
            }

            //merge the nodes

            //first delete all children of the node. This should be a single invocation as all children must be leaf
            parentNode = ref DeleteChildren(ref parentNode);
            ValidateTree();

            //Set the data to the node
            SetData(ref parentNode, compareVoxel);

            ValidateTree();

            //if the parent index is invalid the current node is the root node
            if (parentNode.ParentIndex == InvalidIndex)
                return;

            parentNode = ref GetParentNode(ref parentNode);

            if (parentNode.IsLeaf)
                return;

            itCount++;
        }
    }

    public ref Node GetOrCreateNode(Vector3 pos, int depth)
    {
        pos = new Vector3(pos.X % Dimensions, pos.Y % Dimensions, pos.Z % Dimensions);

        ref var node = ref GetRootNode();

        while (node.Depth != depth)
        {
            if (node.IsLeaf)
            {
                node = ref SplitNode(ref node);
            }

            node = ref GetChildNode(ref node, pos);
        }

        return ref node;
    }

    /// <summary>
    /// Delete all children of the given node recursively
    /// </summary>
    /// <param name="node">Parent node to delete children from</param>
    /// <returns>New reference of the parent node</returns>
    private ref Node DeleteChildren(ref Node node)
    {
        if (node.IsLeaf)
        {
            return ref node;
        }

        //First step: Delete children of current nodes children (Recursion) 
        for (byte childIndex = 0; childIndex < ChildCount; childIndex++)
        {
            ref var child = ref GetChildNode(ref node, childIndex);
            child = ref DeleteChildren(ref child);
            node = ref GetParentNode(ref child);
        }

        //Second step: Delete Data of current children
        for (byte childIndex = 0; childIndex < ChildCount; childIndex++)
        {
            ref var child = ref GetChildNode(ref node, childIndex);
            DeleteData(ref child);
        }

        //Third step: Delete the actual children

        //Move the last nodes to the location of the ones to delete
        ref var replaceParent = ref GetParentNode(ref Nodes[NodeCount - 1]);
        var toReplaceIndex = node.GetChildIndex(0);

        if (Unsafe.AreSame(ref replaceParent, ref node))
        {
            //The replacer node is the one to delete
            //Early exit
            NodeCount -= ChildCount;
            node.SetChildIndex(InvalidIndex);

            Nodes.AsSpan((int) NodeCount, ChildCount).Clear();

            //array could be resized here
            return ref Nodes[node.Index];
        }

        var firstChildIndex = node.GetChildIndex(0);
        node.SetChildIndex(InvalidIndex);

        for (byte childIndex = 0; childIndex < ChildCount; childIndex++)
        {
            ref var childNode = ref GetNode(firstChildIndex + childIndex);
            ref var replacerChildNode = ref GetChildNode(ref replaceParent, childIndex);

            var index = childNode.Index;

            childNode = replacerChildNode;

            //update the node index
            childNode.Index = index;

            //update data owner index
            if (!childNode.IsEmpty)
            {
                Data.ownerNodes[childNode.DataIndex] = childNode.Index;
            }

            //Update the children to reference to the new parent location
            if (!childNode.IsLeaf)
            {
                for (byte grandChildIndex = 0; grandChildIndex < ChildCount; grandChildIndex++)
                {
                    ref var grandChildren = ref GetChildNode(ref childNode, grandChildIndex);
                    grandChildren.ParentIndex = childNode.Index;
                }
            }

            if (Unsafe.AreSame(ref replacerChildNode, ref node))
            {
                node = ref childNode;
            }
        }

        replaceParent.SetChildIndex(toReplaceIndex);

        NodeCount -= ChildCount;
        Nodes.AsSpan((int) NodeCount, ChildCount).Clear();
        
        //array could be resized here
        return ref Nodes[node.Index];
    }

    private void FillPathToNode(ref Node node, Span<byte> path)
    {
        Logger.AssertAndThrow(path.Length == node.Depth, "Path length and node depth must be the same", "VoxelOctree");

        ref Node parentNode = ref GetParentNode(ref node);

        while (true)
        {
            path[parentNode.Depth] = node.ParentChildIndex;

            if (parentNode.ParentIndex == InvalidIndex)
                break;

            node = ref parentNode;
            parentNode = ref GetParentNode(ref parentNode);
        }
    }

    private ref Node GetNodeByPath(Span<byte> path)
    {
        ref Node node = ref GetRootNode();

        foreach (var childIndex in path)
        {
            node = ref GetChildNode(ref node, childIndex);
        }

        return ref node;
    }


    private ref Node GetChildNode(ref Node node, byte position)
    {
        Debug.Assert(node.Depth != 0 || node.ParentIndex == InvalidIndex);

        return ref Nodes[node.GetChildIndex(position)];
    }

    private ref Node GetParentNode(ref Node node)
    {
        return ref Nodes[node.ParentIndex];
    }

    private ref Node GetChildNode(ref Node node, Vector3 position)
    {
        var childIndex = GetChildIndex(position, node.Depth);
        return ref Nodes[node.GetChildIndex(childIndex)];
    }

    private ref Node SplitNode(ref Node node)
    {
        Debug.Assert(node.IsLeaf);

        var voxelData = GetVoxelData(ref node);
        DeleteData(ref node);

        var firstChildIndex = NodeCount;

        node.SetChildIndex(firstChildIndex);
        NodeCount += ChildCount;
        
        //array could be resized here
        node = ref Nodes[node.Index];

        var childDepth = (byte) (node.Depth + 1);

        node.GetLocationData(out var location, out var size);
        var halfSize = size / 2;


        for (byte i = 0; i < ChildCount; i++)
        {
            ref var childNode = ref Nodes[firstChildIndex + i];
            childNode = new Node()
            {
                Depth = childDepth,
                Index = firstChildIndex + i,
                DataIndex = InvalidIndex,
                ParentIndex = node.Index,
                ParentChildIndex = i
            };
            childNode.SetChildIndex(InvalidIndex);

            var childLocation = location + halfSize * GetChildOffset(i);

            childNode.SetLocationData(childLocation);

            SetData(ref childNode, voxelData);
        }

        return ref node;
    }

    private void SetData(ref Node node, VoxelData dataVoxel)
    {
        if (dataVoxel.Id == BlockIDs.Air)
        {
            DeleteData(ref node);
            return;
        }

        if (node.IsEmpty)
        {
            node.DataIndex = DataCount++;
            Data.ownerNodes[node.DataIndex] = node.Index;
        }

        Data.voxels[node.DataIndex] = dataVoxel;
        Data.physicsData[node.DataIndex] = dataVoxel.GetPhysicsData();
        Data.renderData[node.DataIndex] = dataVoxel.GetRenderData();
    }

    internal VoxelData GetVoxelData(ref Node node)
    {
        Logger.AssertAndThrow(node.IsLeaf, "Node is not a leaf", "VoxelOctree");
        return node.IsEmpty ? new VoxelData(BlockIDs.Air) : Data.voxels[node.DataIndex];
    }

    private void DeleteData(ref Node node)
    {
        Logger.AssertAndThrow(node.IsLeaf, "Can only delete data from leaf nodes", "VoxelOctree");
        if (node.IsEmpty) return;

        ref var replaceDataNode = ref GetNode(Data.ownerNodes[DataCount - 1]);

        Logger.AssertAndThrow(replaceDataNode.DataIndex == DataCount - 1, "Index mismatch", "VoxelOctree");

        Data.voxels[node.DataIndex] = Data.voxels[replaceDataNode.DataIndex];
        Data.physicsData[node.DataIndex] = Data.physicsData[replaceDataNode.DataIndex];
        Data.renderData[node.DataIndex] = Data.renderData[replaceDataNode.DataIndex];
        Data.ownerNodes[node.DataIndex] = Data.ownerNodes[replaceDataNode.DataIndex];

        Data.voxels[replaceDataNode.DataIndex] = default;
        Data.physicsData[replaceDataNode.DataIndex] = default;
        Data.renderData[replaceDataNode.DataIndex] = default;
        Data.ownerNodes[replaceDataNode.DataIndex] = default;

        replaceDataNode.DataIndex = node.DataIndex;
        node.DataIndex = InvalidIndex;

        DataCount--;
    }

    internal void ResizeNodes(int newCapacity)
    {
        var oldNodes = Nodes;

        Nodes = new Node[newCapacity];
        oldNodes.AsSpan(0, (int) NodeCount).CopyTo(Nodes);
    }

    internal void ResizeData(int newCapacity)
    {
        var oldOwners = Data.ownerNodes;
        var oldVoxels = Data.voxels;
        var oldPhysicsData = Data.physicsData;
        var oldRenderData = Data.renderData;

        Data = new ValueTuple<uint[], VoxelData[], VoxelPhysicsData[], VoxelRenderData[]>(
            new uint[newCapacity], new VoxelData[newCapacity], new VoxelPhysicsData[newCapacity],
            new VoxelRenderData[newCapacity]);

        oldOwners.AsSpan(0, (int) DataCount).CopyTo(Data.ownerNodes.AsSpan());
        oldVoxels.AsSpan(0, (int) DataCount).CopyTo(Data.voxels.AsSpan());
        oldPhysicsData.AsSpan(0, (int) DataCount).CopyTo(Data.physicsData.AsSpan());
        oldRenderData.AsSpan(0, (int) DataCount).CopyTo(Data.renderData.AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ref Node GetRootNode()
    {
        return ref Nodes[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ref Node GetNode(uint index)
    {
        Debug.Assert(index < NodeCount);
        return ref Nodes[index];
    }

    internal static byte GetChildIndex(Vector3 position, int depth)
    {
        float sizeCurrentLayer = Dimensions / MathF.Pow(2, depth);
        float halfSizeCurrentLayer = sizeCurrentLayer / 2;

        var adjustedX = position.X % sizeCurrentLayer;
        var lowerX = adjustedX < halfSizeCurrentLayer;

        var adjustedY = position.Y % sizeCurrentLayer;
        var lowerY = adjustedY < halfSizeCurrentLayer;

        var adjustedZ = position.Z % sizeCurrentLayer;
        var lowerZ = adjustedZ < halfSizeCurrentLayer;

        return (byte) ((lowerX ? 0 : 4) + (lowerY ? 0 : 2) + (lowerZ ? 0 : 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    // ReSharper disable once UnusedMember.Local
    internal static Vector3 GetChildOffset(byte childIndex)
    {
        return childIndex switch
        {
            0b0000 => new Vector3(0, 0, 0),
            0b0001 => new Vector3(0, 0, 1),
            0b0010 => new Vector3(0, 1, 0),
            0b0011 => new Vector3(0, 1, 1),
            0b0100 => new Vector3(1, 0, 0),
            0b0101 => new Vector3(1, 0, 1),
            0b0110 => new Vector3(1, 1, 0),
            0b0111 => new Vector3(1, 1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(childIndex), childIndex, null)
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Node
    {
        /*
         *  IMPORTANT!
         *  Any changes to the layout of this struct needs to be resembled in the voxel render shader
         *  Only use integer types to ensure alignment
         */

        private uint FirstChildIndex;

        public uint DataIndex;
        public uint Index;
        public uint ParentIndex;
        private uint AdditionalData;
        internal uint Location;

        public void GetLocationData(out Vector3 position, out float size)
        {
            var localDimensions = LocalDimensions();

            size = Dimensions / (float) localDimensions;

            var location = (int) Location;

            var x = location % localDimensions;
            var y = location / localDimensions % localDimensions;
            var z = location / (localDimensions * localDimensions);

            position = new Vector3(x, y, z);

            position *= size;
        }

        public void SetLocationData(Vector3 position)
        {
            var localDimensions = LocalDimensions();
            var size = Dimensions / (float) localDimensions;

            var x = (int) (position.X / size);
            var y = (int) (position.Y / size);
            var z = (int) (position.Z / size);

            Location = (uint) (x + y * localDimensions + z * localDimensions * localDimensions);
        }

        public float GetSize()
        {
            return Dimensions / (float) LocalDimensions();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private int LocalDimensions() => 1 << Depth;

        //internal uint _data;

        private const uint ParentChildIndexMask = 0b0000_0000_0000_0111u;
        private const uint DepthMask = 0b0000_0000_1111_0000u;
        private const uint RemainingMask = 0b1111_1111_0000_0000u;

        public byte ParentChildIndex
        {
            get => (byte) (AdditionalData & ParentChildIndexMask);
            set => AdditionalData = (AdditionalData & ~ParentChildIndexMask) | value;
        }

        public bool IsLeaf => FirstChildIndex == InvalidIndex;

        public byte Depth
        {
            get => (byte) ((AdditionalData & DepthMask) >> 4);
            set => AdditionalData = (AdditionalData & ~DepthMask) | ((uint) value << 4);
        }

        public bool IsEmpty => DataIndex == InvalidIndex;

        public uint GetChildIndex(uint childIndex)
        {
            Debug.Assert(childIndex is >= 0 and < 8);
            return FirstChildIndex + childIndex;
        }

        public void SetChildIndex(uint firstChildIndex)
        {
            FirstChildIndex = firstChildIndex;
        }

        public bool Equals(Node other)
        {
            return FirstChildIndex == other.FirstChildIndex && DataIndex == other.DataIndex && Index == other.Index &&
                   ParentIndex == other.ParentIndex && AdditionalData == other.AdditionalData &&
                   Location == other.Location;
        }

        public override bool Equals(object? obj)
        {
            return obj is Node other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FirstChildIndex, DataIndex, Index, ParentIndex, AdditionalData, Location);
        }

        public static bool operator ==(Node left, Node right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Node left, Node right)
        {
            return !left.Equals(right);
        }
    }

    public class OctreeDebugView
    {
        public NodeDebugView? RootNode { get; }
        internal VoxelOctree _octree;

        public OctreeDebugView(VoxelOctree octree)
        {
            _octree = octree;
            ref var root = ref octree.GetRootNode();

            RootNode = new NodeDebugView();
            FillNodeInfo(RootNode, ref root);
        }

        internal void FillNodeInfo(NodeDebugView target, ref Node node)
        {
            target.Depth = node.Depth;
            target.VoxelData = _octree.Data.voxels[node.DataIndex];

            if (node.IsLeaf)
            {
                return;
            }

            target.Children = new NodeDebugView[ChildCount];
            for (byte i = 0; i < ChildCount; i++)
            {
                target.Children[i] = new NodeDebugView();
                ref var child = ref _octree.GetNode(node.GetChildIndex(i));
                FillNodeInfo(target.Children[i], ref child);
            }
        }

        public class NodeDebugView
        {
            public NodeDebugView[]? Children;
            public int Depth;
            public bool SharesDataWithParent;

            public VoxelData VoxelData;
        }
    }
}