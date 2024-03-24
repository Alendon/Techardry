using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using BepuUtilities;

namespace Techardry.Render;

public class MasterBvhTree
{
    internal readonly Node[] Nodes;
    private readonly IList<BoundingBox> _localTreesBoundingBoxes;
    internal readonly int[] TreeIndices;
    private int _nodesUsed = 2;


    private const float FloatTolerance = 0.0001f;
    private const int BinCount = 8;

    public MasterBvhTree(IEnumerable<BoundingBox> localTreesBoundingBoxes)
    {
        _localTreesBoundingBoxes = localTreesBoundingBoxes as BoundingBox[] ??
                                   localTreesBoundingBoxes as IList<BoundingBox> ?? localTreesBoundingBoxes.ToArray();
        Nodes = new Node[_localTreesBoundingBoxes.Count * 2 - 1];
        TreeIndices = new int[_localTreesBoundingBoxes.Count];

        for (var i = 0; i < _localTreesBoundingBoxes.Count; i++)
        {
            TreeIndices[i] = i;
        }

        Nodes[0] = new Node
        {
            treeCount = _localTreesBoundingBoxes.Count,
            leftFirst = 0
        };

        UpdateBounds(0);
        NaiveSubdivide(0);
    }

    private void BinnedSubdivide(int nodeIndex)
    {
        ref var node = ref Nodes[nodeIndex];

        var splitCost = FindBestSplitPlane(ref node, out var axis, out var splitPosition);
        var noSplitCost = CalculateNodeCost(ref node);
        if (splitCost >= noSplitCost)
            return;

        var i = node.leftFirst;
        var j = i + node.treeCount - 1;
        while (i <= j)
        {
            if (GetCenter(_localTreesBoundingBoxes[TreeIndices[i]])[axis] < splitPosition)
                i++;
            else
            {
                (TreeIndices[i], TreeIndices[j]) = (TreeIndices[j], TreeIndices[i]);
                j--;
            }
        }

        var leftCount = i - node.leftFirst;
        if (leftCount == 0 || leftCount == node.treeCount) return;

        var leftChildIndex = _nodesUsed++;
        var rightChildIndex = _nodesUsed++;

        Nodes[leftChildIndex] = new()
        {
            leftFirst = node.leftFirst,
            treeCount = leftCount
        };

        Nodes[rightChildIndex] = new()
        {
            leftFirst = i,
            treeCount = node.treeCount - leftCount
        };

        node.leftFirst = leftChildIndex;
        node.treeCount = 0;

        UpdateBounds(leftChildIndex);
        UpdateBounds(rightChildIndex);

        BinnedSubdivide(leftChildIndex);
        BinnedSubdivide(rightChildIndex);
    }

    private void NaiveSubdivide(int nodeIndex)
    {
        ref var node = ref Nodes[nodeIndex];

        if (node.treeCount <= 2) return;
        
        var extent = node.Bounds.Max - node.Bounds.Min;
        var axis = 0;
        if (extent.Y > extent.X)
            axis = 1;
        if (extent.Z > extent[axis])
            axis = 2;
        
        var splitPos = node.Bounds.Min + new Vector3(extent[axis] * 0.5f);
        
        var i = node.leftFirst;
        var j = i + node.treeCount - 1;

        while (i <= j)
        {
            if(GetCenter(_localTreesBoundingBoxes[TreeIndices[i]])[axis] < splitPos[axis])
                i++;
            else
            {
                (TreeIndices[i], TreeIndices[j]) = (TreeIndices[j], TreeIndices[i]);
                j--;
            }
        }
        
        var leftCount = i - node.leftFirst;
        if (leftCount == 0 || leftCount == node.treeCount) return;
        
        var leftChildIndex = _nodesUsed++;
        var rightChildIndex = _nodesUsed++;
        
        Nodes[leftChildIndex] = new()
        {
            leftFirst = node.leftFirst,
            treeCount = leftCount
        };
        
        Nodes[rightChildIndex] = new()
        {
            leftFirst = i,
            treeCount = node.treeCount - leftCount
        };
        
        node.leftFirst = leftChildIndex;
        node.treeCount = 0;
        
        UpdateBounds(leftChildIndex);
        UpdateBounds(rightChildIndex);
        
        NaiveSubdivide(leftChildIndex);
        NaiveSubdivide(rightChildIndex);
    }

    private void UpdateBounds(int nodeIndex)
    {
        ref var node = ref Nodes[nodeIndex];
        node.Bounds = new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue));

        for (var i = 0; i < node.treeCount; i++)
        {
            var boundingBox = _localTreesBoundingBoxes[TreeIndices[node.leftFirst + i]];
            BoundingBox.CreateMerged(boundingBox, node.Bounds, out node.Bounds);
        }
    }

    private float FindBestSplitPlane(ref Node node, out int axis, out float splitPosition)
    {
        axis = -1;
        splitPosition = float.NaN;

        float bestCost = float.MaxValue;

        var bins = (stackalloc Bin[BinCount]);

        var leftArea = (stackalloc float[BinCount - 1]);
        var rightArea = (stackalloc float[BinCount - 1]);
        var leftCount = (stackalloc int[BinCount - 1]);
        var rightCount = (stackalloc int[BinCount - 1]);

        for (int a = 0; a < 3; a++)
        {
            var boundsMin = float.MaxValue;
            var boundsMax = float.MinValue;

            for (int i = 0; i < node.treeCount; i++)
            {
                var center = GetCenter(_localTreesBoundingBoxes[TreeIndices[node.leftFirst + i]]);
                boundsMin = float.Min(boundsMin, center[a]);
                boundsMax = float.Max(boundsMax, center[a]);
            }

            if (Math.Abs(boundsMin - boundsMax) < FloatTolerance) continue;

            bins.Clear();

            var scale = BinCount / (boundsMax - boundsMin);
            for (int i = 0; i < node.treeCount; i++)
            {
                var treeBoundingBox = _localTreesBoundingBoxes[TreeIndices[node.leftFirst + i]];
                var binIndex = int.Min(BinCount - 1, (int)((GetCenter(treeBoundingBox)[a] - boundsMin) * scale));
                bins[binIndex].Count++;

                BoundingBox.CreateMerged(treeBoundingBox, bins[binIndex].Bounds,
                    out bins[binIndex].Bounds);
            }

            leftArea.Clear();
            rightArea.Clear();
            leftCount.Clear();
            rightCount.Clear();

            BoundingBox leftBounds = default;
            BoundingBox rightBounds = default;
            int leftSum = 0, rightSum = 0;

            for (int i = 0; i < BinCount - 1; i++)
            {
                leftSum += bins[i].Count;
                leftCount[i] = leftSum;
                BoundingBox.CreateMerged(bins[i].Bounds, leftBounds, out leftBounds);
                leftArea[i] = GetArea(leftBounds);

                rightSum += bins[BinCount - 1 - i].Count;
                rightCount[BinCount - 2 - i] = rightSum;
                BoundingBox.CreateMerged(bins[BinCount - 1 - i].Bounds, rightBounds, out rightBounds);
                rightArea[BinCount - 2 - i] = GetArea(rightBounds);
            }

            scale = (boundsMax - boundsMin) / BinCount;
            for (int i = 0; i < BinCount - 1; i++)
            {
                float planeCost = leftCount[i] * leftArea[i] + rightCount[i] * rightArea[i];
                if (planeCost < bestCost)
                {
                    axis = a;
                    splitPosition = boundsMin + (i + 1) * scale;
                    bestCost = planeCost;
                }
            }
        }

        return bestCost;
    }

    private static float CalculateNodeCost(ref Node node)
    {
        var area = GetArea(node.Bounds);
        return area * node.treeCount;
    }

    private static float GetArea(BoundingBox rightBounds)
    {
        var e = rightBounds.Max - rightBounds.Min;
        return 2 * (e.X * e.Y + e.X * e.Z + e.Y * e.Z);
    }

    private static Vector3 GetCenter(BoundingBox bounds)
    {
        return (bounds.Max + bounds.Min) * 0.5f;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Node
    {
        [FieldOffset(0)] public BoundingBox Bounds;

        [FieldOffset(32)] public int leftFirst;

        [FieldOffset(32 + sizeof(int))] public int treeCount;
    }

    struct Bin
    {
        public BoundingBox Bounds;
        public int Count;
    }
}