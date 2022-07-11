using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using MintyCore.Utils.Maths;
using Techardry.Identifications;
using TechardryMath = Techardry.Utils.MathHelper;

namespace Techardry.Voxels;

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

    private uint NodeCapacity
    {
        get => (uint) Nodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2((int) Math.Max(value, InitialNodeCapacity));
            ResizeNodes(alignedSize);
        }
    }

    internal uint NodeCount;


    internal ( uint[] ownerNodes, VoxelData[] voxels, VoxelPhysicsData[] physicsData,
        VoxelRenderData[] renderData) Data;

    private uint DataCapacity
    {
        get => (uint) Data.ownerNodes.Length;
        set
        {
            var alignedSize = MathHelper.CeilPower2((int) Math.Max(value, InitialDataCapacity));
            ResizeData(alignedSize);
        }
    }

    internal uint DataCount;


    public VoxelOctree()
    {
        Nodes = Array.Empty<Node>();
        Data = (Array.Empty<uint>(), Array.Empty<VoxelData>(), Array.Empty<VoxelPhysicsData>(),
            Array.Empty<VoxelRenderData>());

        NodeCapacity = InitialNodeCapacity;
        DataCapacity = InitialDataCapacity;

        DataCount = 1;
        SetData(0, GetDefaultVoxel());
        Data.ownerNodes[0] = 0;

        NodeCount = 1;
        Nodes[0] = new Node
        {
            Depth = RootNodeDepth,
            Index = 0,
            DataIndex = 0,
            SharesDataWithParent = false,
            IsLeaf = true,
            ParentIndex = InvalidIndex,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static VoxelData GetDefaultVoxel()
    {
        return new VoxelData(BlockIDs.Air);
    }

    private void ResizeNodes(int newCapacity)
    {
        var oldNodes = Nodes;

        Nodes = new Node[newCapacity];
        oldNodes.AsSpan(0, (int) NodeCount).CopyTo(Nodes);
    }

    private void ResizeData(int newCapacity)
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

    public VoxelResult GetVoxel(Vector3 position)
    {
        ref var searchNode = ref GetRootNode();

        while (!searchNode.IsLeaf)
        {
            searchNode = ref GetChildNode(ref searchNode, position);
        }

        return new VoxelResult(GetVoxel(ref searchNode), GetVoxelPhysicsDataRef(ref searchNode),
            GetVoxelRenderDataRef(ref searchNode), searchNode.Depth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Remove(Vector3 position, [ValueRange(0, MaximumTotalDivision)] int depth)
    {
        Insert(GetDefaultVoxel(), position, depth);
    }

    public void Insert(in VoxelData voxelData, Vector3 position, [ValueRange(0, MaximumTotalDivision)] int depth)
    {
        //Traverse the tree to the correct depth.

        ref var node = ref GetRootNode();

        while (node.Depth != depth)
        {
            node = ref GetOrCreateChild(ref node, position);
        }

        node = ref DeleteChildren(ref node);
        SetData(ref node, voxelData);

        MergeDataUpwards(ref node);

        if ((NodeCount) * 4 <= NodeCapacity && NodeCapacity > InitialNodeCapacity)
        {
            NodeCapacity = NodeCount;
        }

        if ((DataCount) * 4 <= DataCapacity && DataCapacity > InitialDataCapacity)
        {
            DataCapacity = DataCount;
        }
    }

    public bool ConeTrace(Vector3 origin, Vector3 direction, float coneAngle, out Node node, out Vector3 normal)
    {
        node = GetRootNode();
        normal = Vector3.Zero;

        var halfScale = new Vector3(Dimensions) / 2;
        var center = halfScale;
        var minBox = center - halfScale;
        var maxBox = center + halfScale;

        Span<StackEntry> stack = stackalloc StackEntry[MaximumLevelCount];
        int stackPos = 0;

        if (!TechardryMath.BoxIntersect((minBox, maxBox), (origin, direction), out var rootResult))
        {
            return false;
        }

        stack[stackPos] = new StackEntry()
        {
            NodeIndex = GetRootNode().Index,
            Center = center,
            HalfScale = halfScale,
            RemainingChildrenToCheck = -1,
            T = rootResult.T
        };

        Span<(uint childIndex, float T)> childTMin = stackalloc (uint, float)[ChildCount];

        while (stackPos >= 0)
        {
            //Pop the current node off the stack.
            var nodeIndex = stack[stackPos].NodeIndex;
            center = stack[stackPos].Center;
            halfScale = stack[stackPos].HalfScale;
            var T = stack[stackPos].T;
            var remainingChildrenToCheck = stack[stackPos].RemainingChildrenToCheck;
            uint childrenToCheck = InvalidIndex;
            float childT = float.MaxValue;

            var childHalfScale = halfScale / 2;

            ref var currentNode = ref Nodes[nodeIndex];

            //Check if the current node is a leaf or the maximum depth has been reached.
            if (currentNode.IsLeaf || currentNode.Depth > GetMaxDepthForCone(T))
            {
                var voxelData = GetVoxel(ref currentNode);

                //if the node is not empty its a hit.
                if (!voxelData.Equals(GetDefaultVoxel()))
                {
                    //Todo this could be optimized
                    TechardryMath.BoxIntersect((center - halfScale, center + halfScale), (origin, direction),
                        out var result);
                    normal = result.Normal;
                    node = currentNode;
                    return true;
                }

                //Remove the current node from the stack.
                stackPos--;
                continue;
            }

            //Traverse to the next child.

            //Check if all children have been checked.
            if (remainingChildrenToCheck == 0)
            {
                //Remove the current node from the stack.
                stackPos--;
                continue;
            }

            //Get the next child to check.
            if (remainingChildrenToCheck > 0)
            {
                childrenToCheck = stack[stackPos].ChildSortOrder[--remainingChildrenToCheck];
            }

            if (remainingChildrenToCheck == -1)
            {
                remainingChildrenToCheck = 0;
                childTMin.Clear();

                var childDepth = currentNode.Depth + 1;

                //Calculate the tMin for each child.
                for (uint childIndex = 0; childIndex < ChildCount; childIndex++)
                {
                    ref var childNode = ref GetChildNode(ref currentNode, childIndex);

                    var childVoxel = GetVoxel(ref childNode);

                    if (childNode.IsLeaf && childVoxel.Equals(GetDefaultVoxel()))
                    {
                        continue;
                    }


                    var childCenter = center + childHalfScale * GetChildOffset(childIndex);

                    //make the box slightly bigger to avoid floating point errors.
                    var childBox = (childCenter - childHalfScale, childCenter + childHalfScale);

                    if (!TechardryMath.BoxIntersect(childBox, (origin, direction), out var childRayResult))
                    {
                        continue;
                    }

                    var maxDepth = GetMaxDepthForCone(childRayResult.T);
                    if (maxDepth < childDepth)
                    {
                        continue;
                    }

                    childTMin[remainingChildrenToCheck] = (childIndex, childRayResult.T);
                    remainingChildrenToCheck++;
                }

                //Sort the children by tMin.
                var childTMinSorted = childTMin;

                //Sort the children by tMin in descending order.
                childTMinSorted = childTMinSorted.Slice(0, remainingChildrenToCheck);
                childTMinSorted.Sort((a, b) => -a.T.CompareTo(b.T));


                //Write the sorted child indices to the stack.
                for (int i = 0; i < childTMinSorted.Length; i++)
                {
                    stack[stackPos].ChildSortOrder[i] = childTMinSorted[i].childIndex;
                    stack[stackPos].ChildT[i] = childTMinSorted[i].T;
                }

                childrenToCheck = stack[stackPos].ChildSortOrder[--remainingChildrenToCheck];
                childT = stack[stackPos].ChildT[remainingChildrenToCheck];
            }

            if (childrenToCheck == InvalidIndex || childrenToCheck >= ChildCount)
            {
                //Remove the current node from the stack.
                stackPos--;
                continue;
            }

            //Update the remaining children to check.
            stack[stackPos].RemainingChildrenToCheck = remainingChildrenToCheck;


            //Push the children to check onto the stack.
            stackPos++;
            stack[stackPos] = new()
            {
                NodeIndex = GetChildNode(ref currentNode, childrenToCheck).Index,
                RemainingChildrenToCheck = -1,
                Center = center + childHalfScale * GetChildOffset(childrenToCheck),
                HalfScale = childHalfScale,
                T = childT
            };
        }

        return false;

        int GetMaxDepthForCone(float T)
        {
            float coneRadius = MathF.Tan(coneAngle / 2) * T;
            var powerOfTwo = (int) MathF.Ceiling(MathF.Log2(coneRadius));

            return coneRadius > 0.5f ? SizeOneDepth - 1 : SizeOneDepth;

            //if power of two is 0 we know that the voxel size is 1
            //Therefore we can just add the depth where 1 is the voxel size.
            //powerOfTwo = Math.Clamp(powerOfTwo, SizeOneDepth - MaximumTotalDivision, SizeOneDepth + MaximumTotalDivision);
            //return -(powerOfTwo - SizeOneDepth);
        }

        float CeilPowerOfTwo(float x)
        {
            return MathF.Pow(2, MathF.Ceiling(MathF.Log(x, 2)));
        }
    }

    //TODO Make Raycast internal
    public bool Raycast(Vector3 origin, Vector3 direction, out Node node, out Vector3 normal,
        int maxDepth = MaximumTotalDivision)
    {
        node = GetRootNode();
        normal = Vector3.Zero;

        var halfScale = new Vector3(Dimensions) / 2;
        var center = halfScale;
        var minBox = center - halfScale;
        var maxBox = center + halfScale;

        Span<StackEntry> stack = stackalloc StackEntry[maxDepth + 1];
        int stackPos = 0;

        if (!TechardryMath.BoxIntersect((minBox, maxBox), (origin, direction), out _))
        {
            return false;
        }

        stack[stackPos] = new StackEntry()
        {
            NodeIndex = (GetRootNode().Index),
            Center = center,
            HalfScale = halfScale,
            RemainingChildrenToCheck = -1
        };

        Span<(uint childIndex, float tMin)> childTMin = stackalloc (uint, float)[ChildCount];

        while (stackPos >= 0)
        {
            //Pop the current node off the stack.
            var nodeIndex = stack[stackPos].NodeIndex;
            center = stack[stackPos].Center;
            halfScale = stack[stackPos].HalfScale;
            var remainingChildrenToCheck = stack[stackPos].RemainingChildrenToCheck;
            uint childrenToCheck = InvalidIndex;

            var childHalfScale = halfScale / 2;

            ref var currentNode = ref Nodes[nodeIndex];

            //Check if the current node is a leaf or the maximum depth has been reached.
            if (currentNode.IsLeaf || currentNode.Depth + 1 > maxDepth)
            {
                var voxelData = GetVoxel(ref currentNode);

                //if the node is not empty its a hit.
                if (!voxelData.Equals(GetDefaultVoxel()))
                {
                    //Todo this could be optimized
                    TechardryMath.BoxIntersect((center - halfScale, center + halfScale), (origin, direction),
                        out var result);
                    normal = result.Normal;
                    node = currentNode;
                    return true;
                }

                //Remove the current node from the stack.
                stackPos--;
                continue;
            }

            //Traverse to the next child.

            //Check if all children have been checked.
            if (remainingChildrenToCheck == 0)
            {
                //Remove the current node from the stack.
                stackPos--;
                continue;
            }

            //Get the next child to check.
            if (remainingChildrenToCheck > 0)
            {
                childrenToCheck = stack[stackPos].ChildSortOrder[--remainingChildrenToCheck];
            }

            if (remainingChildrenToCheck == -1)
            {
                remainingChildrenToCheck = 0;
                childTMin.Clear();

                //Calculate the tMin for each child.
                for (uint childIndex = 0; childIndex < ChildCount; childIndex++)
                {
                    ref var childNode = ref GetChildNode(ref currentNode, childIndex);

                    var childVoxel = GetVoxel(ref childNode);

                    if (childNode.IsLeaf && childVoxel.Equals(GetDefaultVoxel()))
                    {
                        continue;
                    }


                    var childCenter = center + childHalfScale * GetChildOffset(childIndex);

                    //make the box slightly bigger to avoid floating point errors.
                    var childBox = (childCenter - childHalfScale, childCenter + childHalfScale);

                    if (!TechardryMath.BoxIntersect(childBox, (origin, direction), out var childRayResult))
                    {
                        continue;
                    }

                    childTMin[remainingChildrenToCheck] = (childIndex, childRayResult.T);
                    remainingChildrenToCheck++;
                }

                //Sort the children by tMin.
                var childTMinSorted = childTMin;

                //Sort the children by tMin in descending order.
                childTMinSorted = childTMinSorted.Slice(0, remainingChildrenToCheck);
                childTMinSorted.Sort((a, b) => -a.tMin.CompareTo(b.tMin));


                //Write the sorted child indices to the stack.
                for (int i = 0; i < childTMinSorted.Length; i++)
                {
                    stack[stackPos].ChildSortOrder[i] = childTMinSorted[i].childIndex;
                }

                childrenToCheck = stack[stackPos].ChildSortOrder[--remainingChildrenToCheck];
            }

            if (childrenToCheck == InvalidIndex || childrenToCheck >= ChildCount)
            {
                //Remove the current node from the stack.
                stackPos--;
                continue;
            }

            //Update the remaining children to check.
            stack[stackPos].RemainingChildrenToCheck = remainingChildrenToCheck;


            //Push the children to check onto the stack.
            stackPos++;
            stack[stackPos] = new()
            {
                NodeIndex = GetChildNode(ref currentNode, childrenToCheck).Index,
                RemainingChildrenToCheck = -1,
                Center = center + childHalfScale * GetChildOffset(childrenToCheck),
                HalfScale = childHalfScale,
            };
        }

        return false;
    }

    struct StackEntry
    {
        public Vector3 Center;
        public Vector3 HalfScale;
        public uint NodeIndex;
        public int RemainingChildrenToCheck;
        public fixed uint ChildSortOrder[ChildCount];
        public fixed float ChildT[ChildCount];
        public float T;
    }

    private void MergeDataUpwards(ref Node node)
    {
        if (node.Depth == RootNodeDepth)
        {
            return;
        }

        ref var parent = ref GetParentNode(ref node);

        ref readonly var nodeVoxel = ref GetVoxel(ref node);
        bool allEqual = true;

        for (uint i = 0; i < ChildCount && allEqual; i++)
        {
            ref var compareChild = ref GetChildNode(ref parent, i);
            ref readonly var compareVoxel = ref GetVoxel(ref compareChild);
            allEqual &= compareChild.IsLeaf;
            allEqual &= nodeVoxel.Equals(compareVoxel);
        }

        if (!allEqual)
        {
            MergeLodUpwards(ref node);
            return;
        }

        var data = nodeVoxel;

        parent = ref DeleteChildren(ref parent);
        SetData(ref parent, data);
        MergeDataUpwards(ref parent);
    }

    private void MergeLodUpwards(ref Node child)
    {
        if (child.Depth == RootNodeDepth)
            return;

        ref var parent = ref GetParentNode(ref child);

        uint oldLodDataIndex = parent.DataIndex;
        uint oldLodNodeIndex = InvalidIndex;

        //Find the data with the most occurrences.
        int maxCount = 0;
        uint maxIndex = 0;

        for (uint i = 0; i < ChildCount; i++)
        {
            child = ref GetChildNode(ref parent, i);
            ref readonly var firstVoxel = ref GetVoxel(ref child);

            if (child.DataIndex == oldLodDataIndex)
            {
                oldLodNodeIndex = child.Index;
            }

            var count = 0;
            for (uint j = 0; j < ChildCount; j++)
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

        if (oldLodNodeIndex != InvalidIndex)
        {
            ref var oldLodNode = ref GetNode(oldLodNodeIndex);
            UpdateShareWithParent(ref oldLodNode, false);
        }

        child = ref GetChildNode(ref parent, maxIndex);
        UpdateShareWithParent(ref child, true);

        parent.DataIndex = GetChildNode(ref parent, maxIndex).DataIndex;

        MergeLodUpwards(ref parent);
    }

    private void UpdateShareWithParent(ref Node node, bool shareWithParent)
    {
        node.SharesDataWithParent = shareWithParent;

        ref var upwardsNode = ref node;
        while (upwardsNode.SharesDataWithParent)
        {
            upwardsNode = ref GetParentNode(ref upwardsNode);
            upwardsNode.DataIndex = node.DataIndex;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetChildNode(ref Node parent, uint childIndex)
    {
        return ref GetNode(parent.GetChildIndex(childIndex));
    }

    /// <summary>
    /// Delete all children of a node.
    /// </summary>
    /// <param name="node"></param>
    /// <returns>Reference to the original node, as it may be moved in data</returns>
    private ref Node DeleteChildren(ref Node node)
    {
        if (node.IsLeaf)
        {
            return ref node;
        }
        var oldData = GetVoxel(ref node);

        bool notDone;
        do
        {
            ref var nodePtr = ref DeleteFirstChild(ref node, out notDone);
            node = ref Unsafe.AsRef<Node>(Unsafe.AsPointer(ref nodePtr));
        } while (notDone);

        ref Node DeleteFirstChild(ref Node parent, out bool success)
        {
            var originalIndex = parent.Index;

            ref var workingNode = ref parent;
            while (!workingNode.IsLeaf)
            {
                bool foundChildNode = false;
                for (uint i = 0; i < ChildCount; i++)
                {
                    var childNodeIndex = workingNode.GetChildIndex(i);
                    if (childNodeIndex != InvalidIndex)
                    {
                        workingNode = ref GetChildNode(ref workingNode, i);
                        foundChildNode = true;
                        break;
                    }
                }

                if (!foundChildNode)
                {
                    workingNode.IsLeaf = true;
                }
            }

            DeleteData(ref workingNode);

            if (!Unsafe.AreSame(ref workingNode, ref parent))
            {
                ref var workingParent = ref GetParentNode(ref workingNode);
                workingParent.SetChildIndex(workingNode.ParentChildIndex, uint.MaxValue, true);

                DeleteNode(workingNode.Index, out var movedNode);
                success = true;

                if (movedNode.from == originalIndex)
                {
                    return ref GetNode(movedNode.to);
                }

                return ref parent;
            }

            success = false;
            return ref parent;
        }

        node.DataIndex = CreateData(node.Index);
        SetData(ref node, oldData);

        node.IsLeaf = true;

        return ref node;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void DeleteData(ref Node node)
    {
        DeleteData(node.DataIndex, node.Index);
    }

    private void DeleteData(uint dataIndex, uint ownerIndex)
    {
        var dataOwner = Data.ownerNodes[dataIndex];

        if (dataOwner != ownerIndex) return;

        var replaceIndex = --DataCount;

        Data.voxels[dataIndex] = Data.voxels[replaceIndex];
        Data.physicsData[dataIndex] = Data.physicsData[replaceIndex];
        Data.renderData[dataIndex] = Data.renderData[replaceIndex];

        Data.ownerNodes[dataIndex] = Data.ownerNodes[replaceIndex];

        ref var oldOwner = ref GetNode(ownerIndex);
        oldOwner.DataIndex = InvalidIndex;


        ref var toInform = ref GetNode(Data.ownerNodes[dataIndex]);
        bool informParent;
        do
        {
            toInform.DataIndex = dataIndex;
            informParent = toInform.SharesDataWithParent;
            if (informParent)
            {
                toInform = ref GetParentNode(ref toInform);
            }
        } while (informParent);
    }

    private void DeleteNode(uint index, out (uint from, uint to) movedNode)
    {
        var newNodeCount = --NodeCount;

        //If the deleted node is the last one, we don't need to move anything.
        if (newNodeCount <= index)
        {
            movedNode = (InvalidIndex, InvalidIndex);
            return;
        }

        //Move the last node to the deleted node's position.
        var indexToMove = newNodeCount;
        ref var nodeMoveDestination = ref GetNode(index);
        nodeMoveDestination = GetNode(indexToMove);
        nodeMoveDestination.Index = index;

        ref var clearNode = ref GetNode(indexToMove);
        clearNode = default;

        if (!nodeMoveDestination.IsLeaf)
        {
            //Notify children of the move
            for (uint i = 0; i < ChildCount; i++)
            {
                //The node child does not exist anymore
                if (nodeMoveDestination.GetChildIndex(i) == InvalidIndex) continue;

                ref var child = ref GetChildNode(ref nodeMoveDestination, i);
                child.ParentIndex = index;
            }
        }

        if (nodeMoveDestination.IsLeaf)
        {
            SetDataOwner(ref nodeMoveDestination);
        }


        ref var parentNode = ref GetNode(nodeMoveDestination.ParentIndex);
        parentNode.SetChildIndex(nodeMoveDestination.ParentChildIndex, index);


        movedNode = (indexToMove, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool HasChild(ref Node parent)
    {
        return !parent.IsLeaf;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetChildNode(ref Node parent, Vector3 position)
    {
        var childIndex = GetChildIndex(position, parent.Depth);
        return ref GetNode(parent.GetChildIndex(childIndex));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetParentNode(ref Node child)
    {
        var parentIndex = child.ParentIndex;
        return ref GetNode(parentIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetNode(uint index)
    {
        return ref Nodes[index];
    }

    private void CreateChildren(ref Node parent)
    {
        byte childrenDepth = (byte) (parent.Depth + 1);

        parent.IsLeaf = false;

        for (byte i = 0; i < ChildCount; i++)
        {
            var childIndex = NodeCount++;
            parent.SetChildIndex(i, childIndex);

            ref var child = ref GetNode(childIndex);
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
            child.DataIndex = CreateData(childIndex);
            SetData(ref child, GetVoxel(ref parent));
        }
    }

    private uint CreateData(uint ownerIndex)
    {
        if (DataCount >= DataCapacity)
        {
            DataCapacity *= 2;
        }


        var dataIndex = DataCount++;
        SetDataOwner(dataIndex, ownerIndex);
        return dataIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SetDataOwner(ref Node node)
    {
        SetDataOwner(node.DataIndex, node.Index);
    }

    private void SetDataOwner(uint dataIndex, uint ownerIndex)
    {
        Data.ownerNodes[dataIndex] = ownerIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref readonly VoxelData GetVoxel(ref Node node)
    {
        return ref GetVoxel(node.DataIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref readonly VoxelData GetVoxel(uint index)
    {
        return ref GetVoxelRef(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SetData(ref Node node, in VoxelData voxelData)
    {
        SetData(node.DataIndex, voxelData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SetData(uint index, in VoxelData voxelData)
    {
        GetVoxelRef(index) = voxelData;
        GetVoxelPhysicsDataRef(index) = voxelData.GetPhysicsData();
        GetVoxelRenderDataRef(index) = voxelData.GetRenderData();

        var empty = voxelData.Id == BlockIDs.Air;
        
        ref var node = ref GetNode(Data.ownerNodes[index]);
        node.IsEmpty = empty;
        while (node.SharesDataWithParent)
        {
            node = ref GetParentNode(ref node);
            node.IsEmpty = empty;
        }
    }

    public ref VoxelData GetVoxelRef(ref Node node)
    {
        return ref GetVoxelRef(node.DataIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref VoxelData GetVoxelRef(uint index)
    {
        return ref Data.voxels[index];
    }

    public ref VoxelPhysicsData GetVoxelPhysicsDataRef(ref Node node)
    {
        return ref GetVoxelPhysicsDataRef(node.DataIndex);
    }

    private ref VoxelPhysicsData GetVoxelPhysicsDataRef(uint index)
    {
        return ref Data.physicsData[index];
    }

    public ref VoxelRenderData GetVoxelRenderDataRef(ref Node node)
    {
        return ref GetVoxelRenderDataRef(node.DataIndex);
    }

    private ref VoxelRenderData GetVoxelRenderDataRef(uint index)
    {
        return ref Data.renderData[index];
    }

    private ref Node GetOrCreateChild(ref Node parent, Vector3 position)
    {
        if (!HasChild(ref parent))
        {
            if (NodeCount + 8 >= NodeCapacity)
            {
                var parentIndex = parent.Index;

                NodeCapacity *= 2;
                parent = ref GetNode(parentIndex);
            }

            CreateChildren(ref parent);
        }

        return ref GetChildNode(ref parent, position);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetRootNode()
    {
        return ref Nodes[0];
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

        return (byte) ((lowerX ? 0 : 4) + (lowerY ? 0 : 2) + (lowerZ ? 0 : 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector3 GetChildOffset(uint childIndex)
    {
        return childIndex switch
        {
            0b0000 => new Vector3(-1, +1, -1),
            0b0001 => new Vector3(-1, +1, +1),
            0b0010 => new Vector3(-1, -1, -1),
            0b0011 => new Vector3(-1, -1, +1),
            0b0100 => new Vector3(+1, +1, -1),
            0b0101 => new Vector3(+1, +1, +1),
            0b0110 => new Vector3(+1, -1, -1),
            0b0111 => new Vector3(+1, -1, +1),
            _ => throw new ArgumentOutOfRangeException(nameof(childIndex), childIndex, null)
        };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        DisposeCore();
    }

    private void DisposeCore()
    {
    }

    public readonly struct VoxelResult
    {
        public readonly VoxelData VoxelData;
        public readonly VoxelPhysicsData VoxelPhysicsData;
        public readonly VoxelRenderData VoxelRenderData;
        public readonly int Depth;


        public VoxelResult(VoxelData voxelData, VoxelPhysicsData voxelPhysicsData, VoxelRenderData voxelRenderData,
            int depth)
        {
            VoxelData = voxelData;
            VoxelPhysicsData = voxelPhysicsData;
            VoxelRenderData = voxelRenderData;
            Depth = depth;
        }

        public float VoxelSize => Dimensions / MathF.Pow(2, Depth);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Node
    {
        /*
         *  IMPORTANT!
         *  Any changes to the layout of this struct needs to be resembled in the voxel render shader
         *  Only use integer types to ensure alignment
         */

        private fixed uint ChildIndices[8];

        public uint DataIndex;
        public uint Index;
        public uint ParentIndex;

        //Use a seperate data field to be able to control the alignment of the data
        //This is needed as the internal array gets copied to the gpu and the alignment needs to be preserved
        //The data layout is as follows (counting from the right):
        //The first byte is the parent child index
        //The second byte is the depth
        //The third byte is the leaf indicator
        //The fourth byte is the data share indicator
        //There is still empty space which can be utilised for other purposes

        //private uint _data;

        private uint _parentChildIndex;
        private uint _leaf;
        private uint _shareData;
        private uint _depth;
        private uint _isEmpty;

        public byte ParentChildIndex
        {
            get => (byte) _parentChildIndex;
            set => _parentChildIndex = value;
        }

        public bool IsLeaf
        {
            get => _leaf != 0;
            set => _leaf = value ? 1u : 0;
        }

        public bool SharesDataWithParent
        {
            get => _shareData != 0;
            set => _shareData = value ? 1u : 0;
        }

        public byte Depth
        {
            get => (byte) _depth;
            set => _depth = value;
        }
        
        public bool IsEmpty
        {
            get => _isEmpty != 0;
            set => _isEmpty = value ? 1u : 0;
        }

        public uint GetChildIndex(uint childIndex)
        {
            Debug.Assert(childIndex is >= 0 and < 8);
            return ChildIndices[childIndex];
        }

        public void SetChildIndex(int childIndex, uint value, bool allowInvalidIndex = false)
        {
            Debug.Assert(childIndex is >= 0 and < 8);
            if (!allowInvalidIndex)
            {
                Debug.Assert(value != InvalidIndex);
            }

            ChildIndices[childIndex] = value;
        }
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
            target.VoxelData = _octree.GetVoxel(ref node);
            target.SharesDataWithParent = node.SharesDataWithParent;

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