using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using MintyCore.Utils.Maths;
using TechardryMath = Techardry.Utils.MathHelper;

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


    private ((int index, bool leftAligned)[] ownerNodes, VoxelData[] voxels, VoxelPhysicsData[] physicsData,
        VoxelRenderData[] renderData) _data;

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
        _data = (Array.Empty<(int, bool)>(), Array.Empty<VoxelData>(), Array.Empty<VoxelPhysicsData>(),
            Array.Empty<VoxelRenderData>());

        NodeCapacity = InitialNodeCapacity;
        DataCapacity = InitialDataCapacity;

        DataCount = (1, 0);
        SetData(0, true, GetDefaultVoxel());
        _data.ownerNodes[0] = (0, true);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static VoxelData GetDefaultVoxel()
    {
        return new VoxelData(0);
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
        var oldPhysicsData = _data.physicsData;
        var oldRenderData = _data.renderData;

        var oldLeftOwners = oldOwners.AsSpan(0, DataCount.left);
        var oldRightOwners = oldOwners.AsSpan(oldOwners.Length - DataCount.right, DataCount.right);

        var oldLeftVoxels = oldVoxels.AsSpan(0, DataCount.left);
        var oldRightVoxels = oldVoxels.AsSpan(oldVoxels.Length - DataCount.right, DataCount.right);
        
        var oldLeftPhysicsData = oldPhysicsData.AsSpan(0, DataCount.left);
        var oldRightPhysicsData = oldPhysicsData.AsSpan(oldPhysicsData.Length - DataCount.right, DataCount.right);
        
        var oldLeftRenderData = oldRenderData.AsSpan(0, DataCount.left);
        var oldRightRenderData = oldRenderData.AsSpan(oldRenderData.Length - DataCount.right, DataCount.right);

        _data = new ValueTuple<ValueTuple<int, bool>[], VoxelData[], VoxelPhysicsData[], VoxelRenderData[]>(
            new (int, bool)[newCapacity], new VoxelData[newCapacity], new VoxelPhysicsData[newCapacity],
            new VoxelRenderData[newCapacity]);

        var newLeftOwners = _data.ownerNodes.AsSpan(0, DataCount.left);
        var newRightOwners = _data.ownerNodes.AsSpan(_data.ownerNodes.Length - DataCount.right, DataCount.right);

        var newLeftVoxels = _data.voxels.AsSpan(0, DataCount.left);
        var newRightVoxels = _data.voxels.AsSpan(_data.voxels.Length - DataCount.right, DataCount.right);
        
        var newLeftPhysics = _data.physicsData.AsSpan(0, DataCount.left);
        var newRightPhysics = _data.physicsData.AsSpan(_data.physicsData.Length - DataCount.right, DataCount.right);
        
        var newLeftRender = _data.renderData.AsSpan(0, DataCount.left);
        var newRightRender = _data.renderData.AsSpan(_data.renderData.Length - DataCount.right, DataCount.right);

        oldLeftOwners.CopyTo(newLeftOwners);
        oldRightOwners.CopyTo(newRightOwners);

        oldLeftVoxels.CopyTo(newLeftVoxels);
        oldRightVoxels.CopyTo(newRightVoxels);
        
        oldLeftPhysicsData.CopyTo(newLeftPhysics);
        oldRightPhysicsData.CopyTo(newRightPhysics);
        
        oldLeftRenderData.CopyTo(newLeftRender);
        oldRightRenderData.CopyTo(newRightRender);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void UpdateLod(int oldLod)
    {
        UpdateLod(ref GetRootNode(), oldLod);
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

        if ((NodeCount.left + NodeCount.right) * 4 <= NodeCapacity && NodeCapacity > InitialNodeCapacity)
        {
            NodeCapacity = NodeCount.left + NodeCount.right;
        }

        if ((DataCount.left + DataCount.right) * 4 <= DataCapacity && DataCapacity > InitialDataCapacity)
        {
            DataCapacity = DataCount.left + DataCount.right;
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
            NodeIndex = GetAbsoluteNodeIndex(ref GetRootNode()),
            Center = center,
            HalfScale = halfScale,
            RemainingChildrenToCheck = -1,
            T = rootResult.T
        };

        Span<(int childIndex, float T)> childTMin = stackalloc (int, float)[ChildCount];

        while (stackPos >= 0)
        {
            //Pop the current node off the stack.
            var nodeIndex = stack[stackPos].NodeIndex;
            center = stack[stackPos].Center;
            halfScale = stack[stackPos].HalfScale;
            var T = stack[stackPos].T;
            var remainingChildrenToCheck = stack[stackPos].RemainingChildrenToCheck;
            int childrenToCheck = -1;
            float childT = float.MaxValue;

            var childHalfScale = halfScale / 2;

            ref var currentNode = ref _nodes[nodeIndex];

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
                for (int childIndex = 0; childIndex < ChildCount; childIndex++)
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

            if (childrenToCheck < 0 || childrenToCheck >= ChildCount)
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
                NodeIndex = GetAbsoluteNodeIndex(ref GetChildNode(ref currentNode, childrenToCheck)),
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
            NodeIndex = GetAbsoluteNodeIndex(ref GetRootNode()),
            Center = center,
            HalfScale = halfScale,
            RemainingChildrenToCheck = -1
        };

        Span<(int childIndex, float tMin)> childTMin = stackalloc (int, float)[ChildCount];

        while (stackPos >= 0)
        {
            //Pop the current node off the stack.
            var nodeIndex = stack[stackPos].NodeIndex;
            center = stack[stackPos].Center;
            halfScale = stack[stackPos].HalfScale;
            var remainingChildrenToCheck = stack[stackPos].RemainingChildrenToCheck;
            int childrenToCheck = -1;

            var childHalfScale = halfScale / 2;

            ref var currentNode = ref _nodes[nodeIndex];

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
                for (int childIndex = 0; childIndex < ChildCount; childIndex++)
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

            if (childrenToCheck < 0 || childrenToCheck >= ChildCount)
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
                NodeIndex = GetAbsoluteNodeIndex(ref GetChildNode(ref currentNode, childrenToCheck)),
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
        public int NodeIndex;
        public int RemainingChildrenToCheck;
        public fixed int ChildSortOrder[ChildCount];
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

        for (int i = 0; i < ChildCount && allEqual; i++)
        {
            ref var compareChild = ref GetChildNode(ref parent, i);
            ref readonly var compareVoxel = ref GetVoxel(ref compareChild);
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
        
        child = ref GetChildNode(ref parent, maxIndex);
        UpdateShareWithParent(ref child, true);

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

                SetData(ref node, data);

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
            parent.SetChildIndex(node.ParentChildIndex, node.Index);

            if (!node.IsLeaf)
            {
                for (int i = 0; i < ChildCount; i++)
                {
                    ref var childNode = ref GetNode(node.GetChildIndex(i), IsLeftAligned(node.Depth + 1, oldLod));
                    childNode.ParentIndex = node.Index;
                }
            }
        }


        if (!node.IsLeaf)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                ref var childNode = ref GetNode(node.GetChildIndex(i), IsLeftAligned(node.Depth + 1, oldLod));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetChildNode(ref Node parent, int childIndex)
    {
        return ref GetNode(parent.GetChildIndex(childIndex), IsLeftAligned(parent.Depth + 1));
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
            int originalIndex = parent.Index;
            bool originalLeftAligned = IsLeftAligned(ref parent);

            ref var workingNode = ref parent;
            while (!workingNode.IsLeaf)
            {
                bool foundChildNode = false;
                for (int i = 0; i < ChildCount; i++)
                {
                    var childNodeIndex = workingNode.GetChildIndex(i);
                    if (childNodeIndex >= 0)
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
                workingParent.SetChildIndex(workingNode.ParentChildIndex, -1, true);
                workingParent.Invalid = true;

                DeleteNode(workingNode.Index, IsLeftAligned(ref workingNode), out var movedNode);
                success = true;

                if (movedNode.from == originalIndex)
                {
                    return ref GetNode(movedNode.to, originalLeftAligned);
                }

                return ref parent;
            }

            success = false;
            return ref parent;
        }

        node.DataIndex = CreateData(GetDataAlignment(ref node), node.Index);
        SetData(ref node, oldData);

        node.IsLeaf = true;
        node.Invalid = false;

        return ref node;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void DeleteData(ref Node node)
    {
        DeleteData(GetDataAlignment(ref node), node.DataIndex, node.Index);
    }
    
    private void DeleteData(bool dataLeftAligned, int dataIndex, int ownerIndex)
    {
        var actualDataIndex = dataLeftAligned ? dataIndex : DataCapacity - dataIndex - 1;
        var actualOwnerIndex = dataLeftAligned ? ownerIndex : NodeCapacity - ownerIndex - 1;

        var dataOwner = _data.ownerNodes[actualDataIndex];

        if (dataOwner.index != ownerIndex || dataOwner.leftAligned != dataLeftAligned) return;

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
        _data.physicsData[actualDataIndex] = _data.physicsData[replaceIndex];
        _data.renderData[actualDataIndex] = _data.renderData[replaceIndex];

        _data.ownerNodes[actualDataIndex] = _data.ownerNodes[replaceIndex];

        ref var oldOwner = ref GetNode(ownerIndex, dataLeftAligned);
        oldOwner.DataIndex = -1;


        
        ref var toInform = ref GetNode(_data.ownerNodes[actualDataIndex].index, _data.ownerNodes[actualDataIndex].leftAligned);
        bool informParent;
        do
        {
            toInform.DataIndex = actualDataIndex;
            informParent = toInform.SharesDataWithParent;
            if (informParent)
            {
                toInform = ref GetParentNode(ref toInform);
            }
        } while (informParent);
    }

    private void DeleteNode(int index, bool leftAligned, out (int from, int to) movedNode)
    {
        int newNodeCount;
        if (leftAligned)
            newNodeCount = --NodeCount.left;
        else
            newNodeCount = --NodeCount.right;

        //If the deleted node is the last one, we don't need to move anything.
        if (newNodeCount <= index)
        {
            movedNode = (-1, -1);
            return;
        }

        //Move the last node to the deleted node's position.
        var indexToMove = newNodeCount;
        ref var nodeMoveDestination = ref GetNode(index, leftAligned);
        nodeMoveDestination = GetNode(indexToMove, leftAligned);
        nodeMoveDestination.Index = index;

        ref var clearNode = ref GetNode(indexToMove, leftAligned);
        clearNode = new()
        {
            Depth = 255,
            Invalid = true,
            Index = -1,
            ParentIndex = -1,
            IsLeaf = false,
            DataIndex = -1
        };
        for (int i = 0; i < ChildCount; i++)
        {
            clearNode.SetChildIndex(i, -1, true);
        }

        if (!nodeMoveDestination.IsLeaf)
        {
            //Notify children of the move
            for (int i = 0; i < ChildCount; i++)
            {
                //The node child does not exist anymore
                if (nodeMoveDestination.GetChildIndex(i) == -1) continue;

                ref var child = ref GetChildNode(ref nodeMoveDestination, i);
                child.ParentIndex = index;
            }
        }

        if (nodeMoveDestination.IsLeaf)
        {
            SetDataOwner(ref nodeMoveDestination);
        }


        ref var parentNode = ref GetNode(nodeMoveDestination.ParentIndex, leftAligned);
        parentNode.SetChildIndex(nodeMoveDestination.ParentChildIndex, index);


        movedNode = (indexToMove, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsLeftAligned(ref Node node)
    {
        return IsLeftAligned(node.Depth, inverseLod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsLeftAligned(ref Node node, int lodLevel)
    {
        return IsLeftAligned(node.Depth, lodLevel);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsLeftAligned(int depth)
    {
        return IsLeftAligned(depth, inverseLod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsLeftAligned(int depth, int lodLevel)
    {
        return lodLevel >= depth;
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
        var leftAligned = IsLeftAligned(parent.Depth + 1);
        return ref GetNode(parent.GetChildIndex(childIndex), leftAligned);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetParentNode(ref Node child)
    {
        var parentIndex = child.ParentIndex;
        var leftAligned = IsLeftAligned(child.Depth - 1);
        return ref GetNode(parentIndex, leftAligned);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
            parent.SetChildIndex(i, childIndex);

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

    private int CreateData(bool leftAligned, int ownerIndex)
    {
        if (DataCount.left + DataCount.right >= DataCapacity)
        {
            DataCapacity *= 2;
        }


        var dataIndex = leftAligned ? DataCount.left++ : DataCount.right++;
        SetDataOwner(dataIndex, leftAligned, ownerIndex);
        return dataIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
        ownerRef = (actualOwnerIndex, leftAligned);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref readonly VoxelData GetVoxel(ref Node node)
    {
        return ref GetVoxel(node.DataIndex, GetDataAlignment(ref node));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref readonly VoxelData GetVoxel(int index, bool leftAligned)
    {
        return ref GetVoxelRef(index, leftAligned);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SetData(ref Node node, in VoxelData voxelData)
    {
        SetData(node.DataIndex, GetDataAlignment(ref node), voxelData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SetData(int index, bool leftAligned, in VoxelData voxelData)
    {
        GetVoxelRef(index, leftAligned) = voxelData;
        GetVoxelPhysicsDataRef(index, leftAligned) = voxelData.GetPhysicsData();
        GetVoxelRenderDataRef(index, leftAligned) = voxelData.GetRenderData();
    }

    public ref VoxelData GetVoxelRef(ref Node node)
    {
        return ref GetVoxelRef(node.DataIndex, GetDataAlignment(ref node));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref VoxelData GetVoxelRef(int index, bool leftAligned)
    {
        return ref leftAligned ? ref _data.voxels[index] : ref _data.voxels[DataCapacity - index - 1];
    }

    public ref VoxelPhysicsData GetVoxelPhysicsDataRef(ref Node node)
    {
        return ref GetVoxelPhysicsDataRef(node.DataIndex, GetDataAlignment(ref node));
    }

    private ref VoxelPhysicsData GetVoxelPhysicsDataRef(int index, bool leftAligned)
    {
        return ref leftAligned ? ref _data.physicsData[index] : ref _data.physicsData[DataCapacity - index - 1];
    }

    public ref VoxelRenderData GetVoxelRenderDataRef(ref Node node)
    {
        return ref GetVoxelRenderDataRef(node.DataIndex, GetDataAlignment(ref node));
    }

    private ref VoxelRenderData GetVoxelRenderDataRef(int index, bool leftAligned)
    {
        return ref leftAligned ? ref _data.renderData[index] : ref _data.renderData[DataCapacity - index - 1];
    }

    private ref Node GetOrCreateChild(ref Node parent, Vector3 position)
    {
        if (!HasChild(ref parent))
        {
            if (NodeCount.left + NodeCount.right + 8 >= NodeCapacity)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ref Node GetRootNode()
    {
        return ref _nodes[0];
    }

    /// <summary>
    /// Check if the given depth is rendered with the current set lod.
    /// </summary>
    /// <param name="depth"></param>
    /// <returns>True if included</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsIncludedInLod(int depth)
    {
        return depth <= inverseLod;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int GetAbsoluteNodeIndex(ref Node node)
    {
        return GetAbsoluteNodeIndex(node.Index, IsLeftAligned(ref node));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int GetAbsoluteNodeIndex(int nodeIndex, bool isLeftAligned)
    {
        return isLeftAligned ? nodeIndex : NodeCapacity - nodeIndex - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int GetAbsoluteDataIndex(ref Node node)
    {
        return GetAbsoluteDataIndex(node.DataIndex, GetDataAlignment(ref node));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int GetAbsoluteDataIndex(int dataIndex, bool leftAligned)
    {
        return leftAligned ? dataIndex : DataCapacity - dataIndex - 1;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vector3 GetChildOffset(int childIndex)
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

    public struct Node
    {
        private fixed int ChildIndices[8];

        public bool Invalid;

        public int GetChildIndex(int childIndex)
        {
            Debug.Assert(childIndex is >= 0 and < 8);
            return ChildIndices[childIndex];
        }

        public void SetChildIndex(int childIndex, int value, bool allowMinusOne = false)
        {
            Debug.Assert(childIndex is >= 0 and < 8);
            if (!allowMinusOne)
            {
                Debug.Assert(value >= 0);
            }

            ChildIndices[childIndex] = value;
        }

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
            target.VoxelData = _octree.GetVoxel(ref node);
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
                ref var child = ref _octree.GetNode(node.GetChildIndex(i), _octree.IsLeftAligned(node.Depth + 1));
                FillNodeInfo(target.Children[i], ref child);
            }
        }

        public class NodeDebugView
        {
            public NodeDebugView[]? Children;
            public int Depth;
            public bool SharesDataWithParent;
            public bool LeftAlignedState;

            public VoxelData VoxelData;
        }
    }
}