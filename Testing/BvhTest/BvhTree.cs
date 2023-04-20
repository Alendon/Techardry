using System.Numerics;
using BepuUtilities;

namespace Testing.BvhTest;

public class BvhTree
{
    private Node[] _nodes;
    private Triangle[] _triangles;
    private uint[] _triangleIndices;
    private uint nodesUsed = 2;

    private const float FloatTolerance = 0.0001f;
    private const int BinCount = 32;

    public BvhTree(Triangle[] triangles)
    {
        var size = triangles.Length * 2 - 1;
        _nodes = new Node[size];
        _triangles = triangles;
        _triangleIndices = new uint[triangles.Length];

        for (var i = 0u; i < triangles.Length; i++)
        {
            _triangleIndices[i] = i;
        }

        ref var root = ref _nodes[0];

        root.leftFirst = 0;
        root.leftFirst = 0;
        root.triangleCount = (uint)triangles.Length;

        UpdateBounds(0u);
        Subdivide(0u);
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

    public void Intersect(ref Ray ray)
    {
        uint nodeIndex = 0;
        var stack = (stackalloc uint[64]);
        int stackIndex = 0;

        while (true)
        {
            if (_nodes[nodeIndex].IsLeaf)
            {
                for (int i = 0; i < _nodes[nodeIndex].triangleCount; i++)
                {
                    IntersectTriangle(ref ray, ref _triangles[_triangleIndices[_nodes[nodeIndex].leftFirst + i]]);
                }

                if (stackIndex == 0) break;
                nodeIndex = stack[--stackIndex];
                continue;
            }

            var child1 = _nodes[nodeIndex].leftFirst;
            var child2 = child1 + 1;

            var dist1 = IntersectsBoundingBox(ray, ref _nodes[child1].Bounds);
            var dist2 = IntersectsBoundingBox(ray, ref _nodes[child2].Bounds);

            if (dist1 > dist2)
            {
                (child1, child2) = (child2, child1);
                (dist1, dist2) = (dist2, dist1);
            }

            if (Math.Abs(dist1 - float.MaxValue) < FloatTolerance)
            {
                if (stackIndex == 0) break;
                nodeIndex = stack[--stackIndex];
            }
            else
            {
                nodeIndex = child1;
                if (Math.Abs(dist2 - float.MaxValue) > FloatTolerance)
                {
                    stack[stackIndex++] = child2;
                }
            }
        }
    }

    public static void IntersectTriangle(ref Ray ray, ref Triangle triangle)
    {
        var edge1 = triangle.V1 - triangle.V0;
        var edge2 = triangle.V2 - triangle.V0;
        var h = Vector3.Cross(ray.Direction, edge2);
        var a = Vector3.Dot(edge1, h);
        if (a > -FloatTolerance && a < FloatTolerance) return;

        var f = 1 / a;
        var s = ray.Origin - triangle.V0;
        var u = f * Vector3.Dot(s, h);
        if (u < 0 || u > 1) return;

        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(ray.Direction, q);
        if (v < 0 || u + v > 1) return;

        var t = f * Vector3.Dot(edge2, q);
        if (t > FloatTolerance)
        {
            ray.T = Math.Min(ray.T, t);
        }
    }

    private float IntersectsBoundingBox(Ray ray, ref BoundingBox nodeBounds)
    {
        var tx1 = (nodeBounds.Min.X - ray.Origin.X) * ray.InverseDirection.X;
        var tx2 = (nodeBounds.Max.X - ray.Origin.X) * ray.InverseDirection.X;
        var tmin = Math.Min(tx1, tx2);
        var tmax = Math.Max(tx1, tx2);

        var ty1 = (nodeBounds.Min.Y - ray.Origin.Y) * ray.InverseDirection.Y;
        var ty2 = (nodeBounds.Max.Y - ray.Origin.Y) * ray.InverseDirection.Y;
        tmin = Math.Max(tmin, Math.Min(ty1, ty2));
        tmax = Math.Min(tmax, Math.Max(ty1, ty2));

        var tz1 = (nodeBounds.Min.Z - ray.Origin.Z) * ray.InverseDirection.Z;
        var tz2 = (nodeBounds.Max.Z - ray.Origin.Z) * ray.InverseDirection.Z;
        tmin = Math.Max(tmin, Math.Min(tz1, tz2));
        tmax = Math.Min(tmax, Math.Max(tz1, tz2));

        if (tmax >= tmin && tmin < ray.T && tmax > 0)
            return tmin;
        return float.MaxValue;
    }

    public struct Node
    {
        public BoundingBox Bounds;
        public uint leftFirst;
        public uint triangleCount;
        public bool IsLeaf => triangleCount > 0;
    }

    struct Bin
    {
        public BoundingBox Bounds;
        public int Count;
    }
}