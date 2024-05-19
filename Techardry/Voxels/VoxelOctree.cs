using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MintyCore.Utils;
using MintyCore.Utils.Maths;
using Techardry.Blocks;
using Techardry.Identifications;
using Techardry.Render;

namespace Techardry.Voxels;

//TODO add removal of unused data

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
    public const int MaximumTotalDivision = 10; // Math.Log2(MaximumTotalDimension);

    public const int MaximumLevelCount = MaximumTotalDivision + 1; // 10 Divisions + 1 Root

    public const int Dimensions = 16;

    public const int ChildCount = 8;

    public const int InvalidIndex = -1;

    public const int RootNodeIndex = 0;

    /// <summary>
    /// Depth where the size of one voxel is 1.
    /// </summary>
    public static readonly int SizeOneDepth = (int)Math.Log2(Dimensions);

    /// <summary>
    /// How often a single voxel can be subdivided.
    /// 0 => no subdivision. A Voxel is always 1x1x1.
    /// 1 => 0.5 x 0.5 x 0.5
    /// 2 => 0.25 x 0.25 x 0.25
    /// etc..
    /// </summary>
    public const int MaxSplitCount = 6; // MaximumTotalDivision - SizeOneDepth;

    /// <summary>
    /// Maximum depth of the octree.
    /// </summary>
    public static readonly int MaxDepth = SizeOneDepth + MaxSplitCount;

    /// <summary>
    /// The minimum size of a voxel. Determined by the MaxSplitCount.
    /// </summary>
    public static readonly float MinimumVoxelSize = 1f / MathF.Pow(2, MaxSplitCount);

    internal Node[] Nodes;
    internal int[] ParentNodeIndices;

    private const int InitialNodeCapacity = 32;
    private const int InitialDataCapacity = 32;

    internal uint NodeCapacity
    {
        get => (uint)Nodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2((int)Math.Max(value, InitialNodeCapacity));
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
            
            _nodeCount = value;
        }
    }

    private Dictionary<VoxelData, int> _dataIndexMap = new();

    internal (VoxelData[] voxels, VoxelPhysicsData[] physicsData,
        VoxelRenderData[] renderData) Data;

    internal int DataCapacity
    {
        get => Data.voxels.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2(Math.Max(value, InitialDataCapacity));
            ResizeData(alignedSize);
        }
    }

    internal int DataCount
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
    private int _dataCount;
    private ITextureAtlasHandler textureAtlasHandler;
    private IBlockHandler blockHandler;

    public uint Version { get; private set; }

    /// <summary>
    /// Determines how often the tree is compacted, based on the version number.
    /// </summary>
    private const uint CompactRate = 32;

    private uint lastCompactedVersion;
    public bool CompactingEnabled { get; set; } = true;


    public VoxelOctree(ITextureAtlasHandler textureAtlasHandler, IBlockHandler blockHandler)
    {
        this.textureAtlasHandler = textureAtlasHandler;
        this.blockHandler = blockHandler;
        Nodes = [];
        Data = ([], [], []);
        ParentNodeIndices = [InvalidIndex];

        NodeCapacity = InitialNodeCapacity;
        DataCapacity = InitialDataCapacity;

        NodeCount = 1;
        Nodes[0] = new Node();

        var dataIndex = GetOrCreateDataIndex(new VoxelData(BlockIDs.Air));
        Debug.Assert(dataIndex == 0);

        Nodes[0].SetDataIndex(dataIndex);
    }

    public void Insert(VoxelData data, Vector3 position, int depth)
    {
        var deletionOccured = false;
        ref var node = ref GetOrCreateNode(position, depth);

        if (!node.IsLeaf())
        {
            node = ref DeleteChildren(ref node);
            deletionOccured = true;
        }

        SetData(ref node, data);

        ValidateTree();

        Compact(false);

        Version += deletionOccured ? 10u : 1u;
    }


    public void Serialize(DataWriter writer)
    {
        writer.EnterRegion("octree");

        DataWriter.ValueRef<int> count = writer.AddValueRef<int>();
        int countValue = 0;

        for (int i = 0; i < NodeCount; i++)
        {
            ref var node = ref Nodes[i];

            if (node.IsEmpty() || !node.IsLeaf()) continue;

            //skip nodes that are not connected to the root node
            //this means that they were deleted and not compacted yet
            // i!=0 because the 0 node is the root node, which has an invalid parent index
            if (i != 0 && ParentNodeIndices[i] == InvalidIndex) continue;

            GetNodeLocationData(ref node, out var position, out _);
            var depth = NodeGetDepth(ref node);
            var data = GetVoxelData(ref node);

            writer.Put(depth);
            writer.Put(position);
            data.Serialize(writer);

            countValue++;
        }

        count.SetValue(countValue);

        writer.ExitRegion();
    }

    public static bool TryDeserialize(DataReader reader, [NotNullWhen(true)] out VoxelOctree? octree,
        ITextureAtlasHandler textureAtlasHandler, IBlockHandler blockHandler)
    {
        reader.EnterRegion();

        if (!reader.TryGetInt(out int count))
        {
            octree = null;
            return false;
        }

        octree = new VoxelOctree(textureAtlasHandler, blockHandler);

        for (int i = 0; i < count; i++)
        {
            if (!reader.TryGetByte(out var depth)
                || !reader.TryGetVector3(out var position)
                || !VoxelData.Deserialize(reader, out var data))
            {
                return false;
            }

            octree.Insert(data, position, depth);
        }

        reader.ExitRegion();
        return true;
    }

    [Conditional("DEBUG_OCTREE")]
    private void ValidateTree()
    {
        for (var i = NodeCount; i < Nodes.Length; i++)
        {
            if (Nodes[i] != default)
                throw new Exception("Node array not cleared");
        }

        //check the parent node indices
        //after the root node, all indices should be repeated 8 times (the children in memory)
        //also each parent index (except invalid) should only be present in 1 group of 8 children

        for (var i = 1; i < NodeCount; i += ChildCount)
        {
            //if the remaining nodes are less than 8 stop
            if (i + ChildCount > NodeCount) break;

            var parentIndex = ParentNodeIndices[i];


            for (var j = 0; j < ChildCount; j++)
            {
                if (ParentNodeIndices[i + j] != parentIndex)
                    throw new Exception("Parent index not repeated 8 times");
            }

            if (parentIndex == InvalidIndex) continue;

            ref var parent = ref Nodes[parentIndex];
            if (parent.IsLeaf())
                throw new Exception("Parent node is a leaf");

            if (parent.GetChildIndex(0) != i)
                throw new Exception("Parent index not matching first child index");
        }
    }


    /// <summary>
    /// Merge nodes upwards based on the given Leaf Node
    /// </summary>
    public void Compact(bool force)
    {
        if (!force && (lastCompactedVersion + CompactRate > Version || !CompactingEnabled)) return;

        lastCompactedVersion = Version;

        MergeUpwards(ref GetRootNode());

        var newNodes = ArrayPool<Node>.Shared.Rent((int)NodeCount);
        var newParentNodeIndices = ArrayPool<int>.Shared.Rent((int)NodeCount);

        newNodes.AsSpan().Clear();
        newParentNodeIndices.AsSpan().Clear();

        var newCount = 1;
        newParentNodeIndices[0] = InvalidIndex;
        CompactInternal(0, 0);

        ArrayPool<Node>.Shared.Return(Nodes);
        ArrayPool<int>.Shared.Return(ParentNodeIndices);
        
        if(newCount * 4 < newNodes.Length)
        {
            var smallerNodes = ArrayPool<Node>.Shared.Rent(newCount);
            var smallerParentNodeIndices = ArrayPool<int>.Shared.Rent(newCount);
            
            newNodes.AsSpan(0, newCount).CopyTo(smallerNodes);
            newParentNodeIndices.AsSpan(0, newCount).CopyTo(smallerParentNodeIndices);
            
            ArrayPool<Node>.Shared.Return(newNodes);
            ArrayPool<int>.Shared.Return(newParentNodeIndices);
            
            newNodes = smallerNodes;
            newParentNodeIndices = smallerParentNodeIndices;
        }

        Nodes = newNodes;
        ParentNodeIndices = newParentNodeIndices;
        _nodeCount = (uint)newCount;

        ValidateTree();

        void CompactInternal(int oldNodeIndex, int newNodeIndex)
        {
            ref var oldNode = ref Nodes[oldNodeIndex];

            if (oldNode.IsLeaf())
            {
                //if it is a leaf node, just copy it
                newNodes[newNodeIndex] = oldNode;
                return;
            }

            ref var newNode = ref newNodes[newNodeIndex];

            var childBaseIndex = (uint)newCount;
            newNode.SetChildIndex(childBaseIndex);

            for (byte i = 0; i < ChildCount; i++)
            {
                newParentNodeIndices[newCount++] = newNodeIndex;
            }

            for (byte i = 0; i < ChildCount; i++)
            {
                var oldChildIndex = oldNode.GetChildIndex(i);
                CompactInternal((int)oldChildIndex, (int)childBaseIndex + i);
            }
        }
    }

    private void MergeUpwards(ref Node currentNode)
    {
        if (currentNode.IsLeaf()) return;

        for (var i = 0u; i < ChildCount; i++)
        {
            MergeUpwards(ref GetNode(currentNode.GetChildIndex(i)));
        }

        ref var firstChild = ref GetNode(currentNode.GetChildIndex(0));
        if (!firstChild.IsLeaf()) return;

        var firstChildData = GetVoxelData(ref firstChild);

        for (var i = 1u; i < ChildCount; i++)
        {
            ref var child = ref GetNode(currentNode.GetChildIndex(i));
            if (!child.IsLeaf()) return;

            if (GetVoxelData(ref child) != firstChildData) return;
        }

        currentNode = DeleteChildren(ref currentNode);
        SetData(ref currentNode, firstChildData);
    }

    public ref Node GetOrCreateNode(Vector3 pos, int targetDepth)
    {
        ref var node = ref GetRootNode();

        int depth = 0;
        while (depth != targetDepth)
        {
            if (node.IsLeaf())
            {
                node = ref SplitNode(ref node);
            }

            node = ref GetChildNode(ref node, pos, depth);
            depth++;
        }

        return ref node;
    }

    public ref Node GetNode(Vector3 pos, int maxDepth)
    {
        ref var node = ref GetRootNode();

        int depth = 0;
        while (depth <= maxDepth)
        {
            if (node.IsLeaf())
            {
                break;
            }

            node = ref GetChildNode(ref node, pos, depth);
            depth++;
        }

        return ref node;
    }

    private ref Node DeleteChildren(ref Node node)
    {
        if (node.IsLeaf()) return ref node;

        for (byte i = 0; i < ChildCount; i++)
        {
            var childIndex = node.GetChildIndex(i);
            ref var childNode = ref Nodes[childIndex];

            DeleteChildren(ref childNode);
            ParentNodeIndices[childIndex] = InvalidIndex;
        }

        //just empty the node, the garbage will be collected at the next compaction
        node = default;

        return ref node;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetParentNode(ref Node node)
    {
        return ref Nodes[NodeParentIndex(ref node)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetChildNode(ref Node node, Vector3 position, int depth)
    {
        var childIndex = GetChildIndex(position, depth);
        return ref Nodes[node.GetChildIndex(childIndex)];
    }

    private ref Node SplitNode(ref Node node)
    {
        Debug.Assert(node.IsLeaf());

        var voxelData = GetVoxelData(ref node);

        var firstChildIndex = NodeCount;

        node.SetChildIndex(firstChildIndex);
        var nodeIndex = NodeIndex(ref node);

        NodeCount += ChildCount;

        //array could be resized here
        node = ref Nodes[nodeIndex];

        for (byte i = 0; i < ChildCount; i++)
        {
            ref var childNode = ref Nodes[firstChildIndex + i];
            childNode = new Node();

            ParentNodeIndices[firstChildIndex + i] = nodeIndex;

            SetData(ref childNode, voxelData);
        }

        return ref node;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int NodeIndex(ref Node node)
    {
        var span = Nodes.AsSpan();
        ref var firstNode = ref span[0];

        var byteOffset = Unsafe.ByteOffset(ref firstNode, ref node);

        //validate that the difference is a multiple of the size of a node
        //and that the node is located inside the array
        Debug.Assert(byteOffset % Unsafe.SizeOf<Node>() == 0);

        var elementOffset = byteOffset / Unsafe.SizeOf<Node>();

        Debug.Assert(elementOffset >= 0);
        Debug.Assert(elementOffset < Nodes.Length);

        return elementOffset.ToInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int NodeParentIndex(ref Node node)
    {
        return ParentNodeIndices[NodeIndex(ref node)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private byte NodeParentChildIndex(ref Node node)
    {
        ref var parent = ref GetParentNode(ref node);
        ref var firstChild = ref Nodes[parent.GetChildIndex(0)];

        var byteOffset = Unsafe.ByteOffset(ref firstChild, ref node);
        Debug.Assert(byteOffset % Unsafe.SizeOf<Node>() == 0);

        var elementOffset = byteOffset / Unsafe.SizeOf<Node>();
        Debug.Assert(elementOffset.ToInt32() >= 0 && elementOffset.ToInt32() < ChildCount);

        return (byte)elementOffset.ToInt32();
    }

    private int GetOrCreateDataIndex(VoxelData data)
    {
        if (_dataIndexMap.TryGetValue(data, out var index))
            return index;

        index = DataCount++;

        _dataIndexMap.Add(data, index);

        Data.voxels[index] = data;
        Data.physicsData[index] = data.GetPhysicsData();
        Data.renderData[index] = data.GetRenderData(textureAtlasHandler, blockHandler);

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SetData(ref Node node, VoxelData data)
    {
        node.SetDataIndex(GetOrCreateDataIndex(data));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal VoxelData GetVoxelData(ref Node node)
    {
        return Data.voxels[node.GetDataIndex()];
    }

    private void ResizeNodes(int newCapacity)
    {
        var oldNodes = Nodes;
        Nodes = ArrayPool<Node>.Shared.Rent(newCapacity);
        Nodes.AsSpan().Clear();
        oldNodes.AsSpan(0, (int)NodeCount).CopyTo(Nodes);

        if (oldNodes.Length > 0)
            ArrayPool<Node>.Shared.Return(oldNodes);

        var oldParentIndices = ParentNodeIndices;
        ParentNodeIndices = ArrayPool<int>.Shared.Rent(newCapacity);
        ParentNodeIndices.AsSpan().Clear();
        oldParentIndices.AsSpan(0, (int)NodeCount).CopyTo(ParentNodeIndices);
        
        //Fill the rest with invalid indices
        ParentNodeIndices.AsSpan((int)NodeCount).Fill(InvalidIndex);

        if (oldParentIndices.Length > 1)
            ArrayPool<int>.Shared.Return(oldParentIndices);
    }

    private void ResizeData(int newCapacity)
    {
        var oldVoxels = Data.voxels;
        var oldPhysicsData = Data.physicsData;
        var oldRenderData = Data.renderData;

        Data = new ValueTuple<VoxelData[], VoxelPhysicsData[], VoxelRenderData[]>(new VoxelData[newCapacity],
            new VoxelPhysicsData[newCapacity], new VoxelRenderData[newCapacity]);

        oldVoxels.AsSpan(0, DataCount).CopyTo(Data.voxels.AsSpan());
        oldPhysicsData.AsSpan(0, DataCount).CopyTo(Data.physicsData.AsSpan());
        oldRenderData.AsSpan(0, DataCount).CopyTo(Data.renderData.AsSpan());
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

    /// <summary>
    /// Calculate the location and size of a node inside the octree
    /// </summary>
    public void GetNodeLocationData(ref Node node, out Vector3 position, out float size)
    {
        //early exit if the node is the root node
        position = Vector3.Zero;
        size = Dimensions;
        
        if (NodeIndex(ref node) == RootNodeIndex) return;
        
        //split the calculation into 2 separate parts
        //the first part calculates the related child indices, forming a path to the node
        //the second part calculates the location and size of the node based on the path

        Span<Vector3> pathToNode = stackalloc Vector3[MaxDepth];

        var written = 0;
        for (var i = 0; i < MaxDepth; i++)
        {
            var parentChildIndex = NodeParentChildIndex(ref node);
            pathToNode[written++] = GetChildOffset(parentChildIndex);

            var parentIndex = NodeParentIndex(ref node);
            if (parentIndex == RootNodeIndex) break;

            node = ref Nodes[parentIndex];
        }

        for (var i = written - 1; i >= 0; i--)
        {
            size /= 2;
            position += pathToNode[i] * size;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public byte NodeGetDepth(ref Node node)
    {
        byte depth = 0;

        var currentIndex = NodeIndex(ref node);

        while (currentIndex != RootNodeIndex)
        {
            currentIndex = ParentNodeIndices[currentIndex];
            depth++;
        }

        return depth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float NodeGetSize(ref Node node)
    {
        return (float)Dimensions / (1 << NodeGetDepth(ref node));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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

        return (byte)((lowerX ? 0 : 4) + (lowerY ? 0 : 2) + (lowerZ ? 0 : 1));
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

        /// <summary>
        /// The child or data index
        /// if the highest bit is not set it is a data index / leaf node
        /// if the highest bit is set it is the first child index
        /// This allows the default value of 0 to correspond to an empty node
        /// </summary>
        private uint DataChildIndex;


        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool IsLeaf() => DataChildIndex < 0x80000000;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool IsEmpty() => DataChildIndex == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void SetChildIndex(uint firstChildIndex)
        {
            DataChildIndex = firstChildIndex | 0x80000000;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public uint GetChildIndex(uint childIndex)
        {
            Debug.Assert(childIndex < 8);
            Debug.Assert(!IsLeaf());

            return (DataChildIndex & 0x7FFFFFFF) + childIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void SetDataIndex(int dataIndex)
        {
            Debug.Assert(dataIndex >= 0);
            DataChildIndex = (uint)dataIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public uint GetDataIndex()
        {
            Debug.Assert(IsLeaf());
            return DataChildIndex;
        }

        public bool Equals(Node other)
        {
            return DataChildIndex == other.DataChildIndex;
        }

        public override bool Equals(object? obj)
        {
            return obj is Node other && Equals(other);
        }

        public override int GetHashCode()
        {
            return DataChildIndex.GetHashCode();
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

        public ulong TotalAllocatedMemory => (ulong)_octree.NodeCount * (ulong)Unsafe.SizeOf<Node>() +
                                             (ulong)_octree.DataCount * (ulong)Unsafe.SizeOf<VoxelData>();

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
            if (node.IsLeaf())
            {
                target.VoxelData = _octree.GetVoxelData(ref node);
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
            public VoxelData? VoxelData;
        }
    }
}