using System.Runtime.InteropServices;
using BepuUtilities;
using Silk.NET.Vulkan;

namespace Techardry.Render;

public class MasterBvhTree
{
    private Node[] _nodes;
    private BoundingBox[] _localTrees;
    private int[] _treeIndices;
    private int nodesUsed = 2;
    
    
    private const float FloatTolerance = 0.0001f;
    private const int BinCount = 32;
    
    public MasterBvhTree(BoundingBox[] localTrees)
    {
        _localTrees = localTrees;
        _nodes = new Node[localTrees.Length * 2 - 1];
        _treeIndices = new int[localTrees.Length];
        
        for (var i = 0; i < localTrees.Length; i++)
        {
            _treeIndices[i] = i;
        }

        _nodes[0] = new Node
        {
            treeCount = localTrees.Length,
            leftFirst = 0
        };

        UpdateBounds(0);
        Subdivide(0);
    }
    
    private void Subdivide(uint nodeIndex)
    {
        ref var node = ref _nodes[nodeIndex];

        var splitCost = FindBestSplitPlane(ref node, out var axis, out var splitPosition);
        var noSplitCost = CalculateNodeCost(ref node);
        if (splitCost >= noSplitCost)
            return;

        var i = node.leftFirst;
        var j = i + node.triangleCount - 1;
        while (i <= j)
        {
            if (_triangles[_triangleIndices[i]].Center[axis] < splitPosition)
                i++;
            else
            {
                (_triangleIndices[i], _triangleIndices[j]) = (_triangleIndices[j], _triangleIndices[i]);
                j--;
            }
        }

        var leftCount = i - node.leftFirst;
        if (leftCount == 0 || leftCount == node.triangleCount) return;

        var leftChildIndex = nodesUsed++;
        var rightChildIndex = nodesUsed++;

        _nodes[leftChildIndex] = new()
        {
            leftFirst = node.leftFirst,
            triangleCount = leftCount
        };

        _nodes[rightChildIndex] = new()
        {
            leftFirst = i,
            triangleCount = node.triangleCount - leftCount
        };

        node.leftFirst = leftChildIndex;
        node.triangleCount = 0;

        UpdateBounds(leftChildIndex);
        UpdateBounds(rightChildIndex);

        Subdivide(leftChildIndex);
        Subdivide(rightChildIndex);
    }

    private void UpdateBounds(uint nodeIndex)
    {
        ref var node = ref _nodes[nodeIndex];
        node.Bounds = new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue));

        var points = new Vector3[3];


        for (var i = 0; i < node.triangleCount; i++)
        {
            var triangle = _triangles[_triangleIndices[node.leftFirst + i]];

            points[0] = triangle.V0;
            points[1] = triangle.V1;
            points[2] = triangle.V2;

            BoundingBox.CreateMerged(BoundingBox.CreateFromPoints(points), node.Bounds, out node.Bounds);
        }
    }

    float FindBestSplitPlane(ref Node node, out int axis, out float splitPosition)
    {
        axis = -1;
        splitPosition = float.NaN;

        float bestCost = float.MaxValue;

        var bins = (stackalloc Bin[BinCount]);
        var triangleVertices = new Vector3[3];

        var leftArea = (stackalloc float[BinCount - 1]);
        var rightArea = (stackalloc float[BinCount - 1]);
        var leftCount = (stackalloc int[BinCount - 1]);
        var rightCount = (stackalloc int[BinCount - 1]);

        for (int a = 0; a < 3; a++)
        {
            var boundsMin = float.MaxValue;
            var boundsMax = float.MinValue;

            for (int i = 0; i < node.triangleCount; i++)
            {
                ref var triangle = ref _triangles[_triangleIndices[node.leftFirst + i]];
                boundsMin = float.Min(boundsMin, triangle.Center[a]);
                boundsMax = float.Max(boundsMax, triangle.Center[a]);
            }

            if (Math.Abs(boundsMin - boundsMax) < FloatTolerance) continue;

            bins.Clear();

            var scale = BinCount / (boundsMax - boundsMin);
            for (int i = 0; i < node.triangleCount; i++)
            {
                ref var triangle = ref _triangles[_triangleIndices[node.leftFirst + i]];
                var binIndex = int.Min(BinCount - 1, (int)((triangle.Center[a] - boundsMin) * scale));
                bins[binIndex].Count++;

                triangleVertices[0] = triangle.V0;
                triangleVertices[1] = triangle.V1;
                triangleVertices[2] = triangle.V2;

                BoundingBox.CreateMerged(BoundingBox.CreateFromPoints(triangleVertices), bins[binIndex].Bounds,
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

    float CalculateNodeCost(ref Node node)
    {
        var area = GetArea(node.Bounds);
        return area * node.triangleCount;
    }

    private float GetArea(BoundingBox rightBounds)
    {
        var e = rightBounds.Max - rightBounds.Min;
        return 2 * (e.X * e.Y + e.X * e.Z + e.Y * e.Z);
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct Node
    {
        [FieldOffset(0)]
        public BoundingBox Bounds;
        
        [FieldOffset(sizeof(float) * 3 * 2)]
        public int leftFirst;
        
        [FieldOffset(sizeof(float) * 3 * 2 + sizeof(int))]
        public int treeCount;
        public bool IsLeaf => treeCount > 0;
    }
}