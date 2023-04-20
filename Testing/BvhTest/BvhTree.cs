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
        if (node.triangleCount <= 2) return;

        var extent = node.Bounds.Max - node.Bounds.Min;
        var axis = 0;
        if (extent.Y > extent.X) axis = 1;
        if (extent.Z > extent[axis]) axis = 2;
        var splitPosition = node.Bounds.Min[axis] + extent[axis] * 0.5f;

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

    public void Intersect(ref Ray ray, uint nodeIndex)
    {
        ref var node = ref _nodes[nodeIndex];
        if (!IntersectsBoundingBox(ref ray, ref node.Bounds)) return;
        if (node.IsLeaf)
        {
            for (var i = 0u; i < node.triangleCount; i++)
            {
                IntersectTriangle(ref ray, ref _triangles[_triangleIndices[node.leftFirst + i]]);
            }
        }
        else
        {
            Intersect(ref ray, node.leftFirst);
            Intersect(ref ray, node.leftFirst + 1);
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

    private bool IntersectsBoundingBox(ref Ray ray, ref BoundingBox nodeBounds)
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

        return tmax >= tmin && tmin < ray.T && tmax > 0;
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