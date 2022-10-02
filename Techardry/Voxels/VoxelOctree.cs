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

    internal const int InitialNodeCapacity = 32;
    internal const int InitialDataCapacity = 32;

    internal uint NodeCapacity
    {
        get => (uint) Nodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2((int) Math.Max(value, InitialNodeCapacity));
            ResizeNodes(alignedSize);
        }
    }

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
        ref var node = ref GetOrCreateNode(position, depth);

        if (!node.IsLeaf)
        {
            DeleteChildren(ref node);
            
            //Just to be safe. The original node could been moved.
            node = ref GetOrCreateNode(position, depth);
        }

        if (data.Id == BlockIDs.Air)
        {
            if (!node.IsEmpty)
            {
                DeleteData(ref node);
            }
        }
        else
        {
            SetData(ref node, data);
        }

        MergeUpwards(ref node);
    }

    private void MergeUpwards(ref Node node)
    {
        List<uint> parentOfDeletedNodes = new();

        while (node.ParentIndex != InvalidIndex)
        {
            //If the parent is not a leaf, we can stop.
            //If the node is in the list of parentOfDeletedNodes, we know it will be a leaf.
            ref var parent = ref GetParentNode(ref node);

            if(!parent.IsLeaf && !parentOfDeletedNodes.Contains(parent.Index))
                break;
            
            
            
            VoxelData? compareVoxel = node.IsEmpty ? null : Data.voxels[node.DataIndex];
            
            bool allSame = true;
            
            for (byte i = 0; i < ChildCount && allSame; i++)
            {
                ref var child = ref GetChildNode(ref parent, i);
                if (compareVoxel is null)
                {
                    allSame = child.IsEmpty;
                    continue;
                }
                
                allSame = !child.IsEmpty && compareVoxel.Value == Data.voxels[child.DataIndex];
            }

            if (allSame)
            {
                DeleteChildren(ref parent, parentOfDeletedNodes);
            }
            else
            {
                break;
            }
        }
        
        CompactDeletedNodes(parentOfDeletedNodes);
    }

    public ref Node GetOrCreateNode(Vector3 pos, int depth)
    {
        pos = new Vector3(pos.X % Dimensions, pos.Y % Dimensions, pos.Z % Dimensions);

        ref var node = ref GetRootNode();

        while (node.Depth != depth)
        {
            if (node.IsLeaf)
            {
                SplitNode(ref node);
            }

            node = ref GetChildNode(ref node, pos);
        }

        return ref node;
    }

    private void CompactDeletedNodes(List<uint> parents)
    {
        foreach (var deleteParentIndex in parents)
        {
            ref var deleteParent = ref Nodes[deleteParentIndex];
            ref var replaceParent = ref GetParentNode(ref GetNode(NodeCount - 1));

            if (deleteParent.Index == replaceParent.Index)
            {
                deleteParent.SetChildIndex(InvalidIndex);
                NodeCount -= ChildCount;
                continue;
            }

            for (byte i = 0; i < ChildCount; i++)
            {
                ref var deleteChild = ref GetChildNode(ref deleteParent, i);
                ref var replaceChild = ref GetChildNode(ref replaceParent, i);

                if (!replaceChild.IsEmpty)
                {
                    Data.ownerNodes[replaceChild.DataIndex] = deleteChild.Index;
                }

                if (!replaceChild.IsLeaf)
                {
                    //Inform the children of the parent move.
                    for (byte j = 0; j < ChildCount; j++)
                    {
                        GetChildNode(ref replaceChild, j).ParentIndex = deleteChild.Index;
                    }
                }

                deleteChild = replaceChild;
                replaceChild = default;
            }

            replaceParent.SetChildIndex(deleteParent.GetChildIndex(0));

            deleteParent.SetChildIndex(InvalidIndex);
            NodeCount -= ChildCount;
        }
    }

    private void DeleteChildren(ref Node node)
    {
        var parents = new List<uint>();
        DeleteChildren(ref node, parents);
        CompactDeletedNodes(parents);
    }

    private void DeleteChildren(ref Node node, List<uint> parents)
    {
        if (node.IsLeaf)
            return;

        parents.Add(node.Index);

        for (byte i = 0; i < ChildCount; i++)
        {
            ref var child = ref GetChildNode(ref node, i);

            DeleteChildren(ref child, parents);
            DeleteData(ref child);
        }

        node.SetChildIndex(InvalidIndex);
    }

    private ref Node GetChildNode(ref Node node, byte position)
    {
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

    private void SplitNode(ref Node node)
    {
        Debug.Assert(node.IsLeaf);

        var firstChildIndex = NodeCount;

        node.SetChildIndex(firstChildIndex);
        NodeCount += ChildCount;

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

            if (node.IsEmpty) continue;

            //First Node inherits data from parent
            if (i == 0)
            {
                childNode.DataIndex = node.DataIndex;

                Data.ownerNodes[childNode.DataIndex] = childNode.Index;
                continue;
            }

            childNode.DataIndex = DataCount++;
            Data.ownerNodes[childNode.DataIndex] = childNode.Index;

            SetData(ref childNode, Data.voxels[node.DataIndex]);
        }

        node.DataIndex = InvalidIndex;
    }

    private void SetData(ref Node node, VoxelData dataVoxel)
    {
        if (node.IsEmpty) return;

        if (dataVoxel.Id == BlockIDs.Air)
        {
            DeleteData(ref node);
            return;
        }

        Data.voxels[node.DataIndex] = dataVoxel;
        Data.physicsData[node.DataIndex] = dataVoxel.GetPhysicsData();
        Data.renderData[node.DataIndex] = dataVoxel.GetRenderData();
    }

    private void DeleteData(ref Node node)
    {
        Logger.AssertAndThrow(node.IsLeaf, "Can only delete data from leaf nodes", "VoxelOctree");
        if (node.IsEmpty) return;

        var indexToDelete = node.DataIndex;
        var lastIndex = --DataCount;
        var lastData = Data.voxels[lastIndex];

        Data.voxels[indexToDelete] = lastData;
        Data.physicsData[indexToDelete] = lastData.GetPhysicsData();
        Data.renderData[indexToDelete] = lastData.GetRenderData();

        Data.ownerNodes[indexToDelete] = Data.ownerNodes[lastIndex];
        Nodes[Data.ownerNodes[indexToDelete]].DataIndex = indexToDelete;

        node.DataIndex = InvalidIndex;

        Data.voxels[lastIndex] = default;
        Data.physicsData[lastIndex] = default;
        Data.renderData[lastIndex] = default;
        Data.ownerNodes[lastIndex] = default;
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
        private uint Location;

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