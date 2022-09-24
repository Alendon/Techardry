using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using Silk.NET.Maths;

namespace Techardry.Voxels;

public unsafe struct VoxelCollider : IHomogeneousCompoundShape<Box, BoxWide>
{
    GCHandle _octreeHandle;

    VoxelOctree Octree => (VoxelOctree) _octreeHandle.Target!;

    public VoxelCollider(VoxelOctree octree)
    {
        _octreeHandle = GCHandle.Alloc(octree, GCHandleType.Normal);
    }

    public ShapeBatch CreateShapeBatch(BufferPool pool, int initialCapacity, Shapes shapeBatches)
    {
        return new HomogeneousCompoundShapeBatch<VoxelCollider, Box, BoxWide>(pool, initialCapacity);
    }

    public int TypeId => 12;

    public void FindLocalOverlaps<TOverlaps, TSubpairOverlaps>(ref Buffer<OverlapQueryForPair> pairs, BufferPool pool,
        Shapes shapes,
        ref TOverlaps overlaps) where TOverlaps : struct, ICollisionTaskOverlaps<TSubpairOverlaps>
        where TSubpairOverlaps : struct, ICollisionTaskSubpairOverlaps
    {
        for (var i = 0; i < pairs.Length; i++)
        {
            ref var pair = ref pairs[i];

            ref var collider = ref Unsafe.AsRef<VoxelCollider>(pair.Container);
            ref var subPairOverlaps = ref overlaps.GetOverlapsForPair(i);

            var octree = collider.Octree;

            for (var j = 0; j < octree.DataCount; j++)
            {
                ref var owner = ref octree.GetNode(octree.Data.ownerNodes[j]);

                if (owner.IsEmpty) continue;

                collider.GetPosedLocalChild(j, out var childData, out var childPose);

                var min = childPose.Position;
                var max = min + new Vector3(childData.Width);

                if (BoundingBox.Intersects(min, max, pair.Min, pair.Max))
                {
                    subPairOverlaps.Allocate(pool) = j;
                }
            }
        }
    }

    public void FindLocalOverlaps<TOverlaps>(in Vector3 min, in Vector3 max, in Vector3 sweep, float maximumT,
        BufferPool pool,
        Shapes shapes, void* overlaps) where TOverlaps : ICollisionTaskSubpairOverlaps
    {
        ref var overlapsRef = ref Unsafe.AsRef<TOverlaps>(overlaps);

        var octree = Octree;

        Tree.ConvertBoxToCentroidWithExtent(min, max, out var origin, out var expansion);
        TreeRay.CreateFrom(origin, sweep, maximumT, out var ray);

        for (var i = 0; i < octree.DataCount; i++)
        {
            ref var owner = ref octree.GetNode(octree.Data.ownerNodes[i]);
            
            if (owner.IsEmpty) continue;
            
            GetPosedLocalChild(i, out var childData, out var childPose);
            
            var voxelMin = childPose.Position - expansion;
            var voxelMax = min + new Vector3(childData.Width) + expansion;
            
            if(Tree.Intersects(voxelMin, voxelMax, &ray, out _))
                overlapsRef.Allocate(pool) = i;
        }
    }

    public void ComputeBounds(in Quaternion orientation, out Vector3 min, out Vector3 max)
    {
        if (!orientation.IsIdentity)
        {
            throw new Exception("VoxelCollider does not support non-identity orientations.");
        }

        min = Vector3.Zero;
        max = new Vector3(VoxelOctree.Dimensions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFirstNode(Vector3 t0, Vector3 tm)
    {
        var result = 0;

        if (t0.X > t0.Y && t0.X > t0.Z)
        {
            if (tm.Y < t0.X) result |= 2;
            if (tm.Z < t0.X) result |= 1;

            return result;
        }

        if (t0.Y > t0.Z)
        {
            if (tm.X < t0.Y) result |= 4;
            if (tm.Z < t0.Y) result |= 1;

            return result;
        }

        if (tm.X < t0.Z) result |= 4;
        if (tm.Y < t0.Z) result |= 2;

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextNode(Vector3 tm, (int X, int Y, int Z) c)
    {
        if (tm.X < tm.Y)
        {
            if (tm.X < tm.Z)
            {
                return c.X;
            }

            return c.Z;
        }

        if (tm.Y < tm.Z)
        {
            return c.Y;
        }

        return c.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetNextChildIndex(int current, Vector3 t0, Vector3 t1, Vector3 tm)
    {
        switch (current)
        {
            case -1:
            {
                return GetFirstNode(t0, tm);
            }
            case 0:
            {
                return NextNode(new Vector3(tm.X, tm.Y, tm.Z), (4, 2, 1));
            }
            case 1:
            {
                return NextNode(new Vector3(tm.X, tm.Y, t1.Z), (5, 3, 8));
            }
            case 2:
            {
                return NextNode(new Vector3(tm.X, t1.Y, tm.Z), (6, 8, 3));
            }
            case 3:
            {
                return NextNode(new Vector3(tm.X, t1.Y, t1.Z), (7, 8, 8));
            }
            case 4:
            {
                return NextNode(new Vector3(t1.X, tm.Y, tm.Z), (8, 6, 5));
            }
            case 5:
            {
                return NextNode(new Vector3(t1.X, tm.Y, t1.Z), (8, 7, 8));
            }
            case 6:
            {
                return NextNode(new Vector3(t1.X, t1.Y, tm.Z), (8, 8, 7));
            }
            default:
            {
                return 8;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetChildT(int childIndex, Vector3 t0, Vector3 t1, Vector3 tm, out Vector3 childT0,
        out Vector3 childT1)
    {
        switch (childIndex)
        {
            case 0:
            {
                childT0 = t0;
                childT1 = tm;
                break;
            }
            case 1:
            {
                childT0 = new Vector3(t0.X, t0.Y, tm.Z);
                childT1 = new Vector3(tm.X, tm.Y, t1.Z);
                break;
            }
            case 2:
            {
                childT0 = new Vector3(t0.X, tm.Y, t0.Z);
                childT1 = new Vector3(tm.X, t1.Y, tm.Z);
                break;
            }
            case 3:
            {
                childT0 = new Vector3(t0.X, tm.Y, tm.Z);
                childT1 = new Vector3(tm.X, t1.Y, t1.Z);
                break;
            }
            case 4:
            {
                childT0 = new Vector3(tm.X, t0.Y, t0.Z);
                childT1 = new Vector3(t1.X, tm.Y, tm.Z);
                break;
            }
            case 5:
            {
                childT0 = new Vector3(tm.X, t0.Y, tm.Z);
                childT1 = new Vector3(t1.X, tm.Y, t1.Z);
                break;
            }
            case 6:
            {
                childT0 = new Vector3(tm.X, tm.Y, t0.Z);
                childT1 = new Vector3(t1.X, t1.Y, tm.Z);
                break;
            }
            case 7:
            {
                childT0 = tm;
                childT1 = t1;
                break;
            }
            default:
            {
                childT0 = Vector3.Zero;
                childT1 = Vector3.Zero;
                break;
            }
        }
    }

    public void RayTest<TRayHitHandler>(in RigidPose pose, in RayData ray, ref float maximumT,
        ref TRayHitHandler hitHandler) where TRayHitHandler : struct, IShapeRayHitHandler
    {
        if (!pose.Orientation.IsIdentity)
        {
            throw new Exception("VoxelCollider does not support non-identity orientations.");
        }

        var rayOrigin = ray.Origin - pose.Position;
        var rayDirection = ray.Direction;

        var treeMin = Vector3.Zero;
        var treeMax = new Vector3(VoxelOctree.Dimensions);

        int childIndexModifier = 0;
        rayDirection = Vector3.Normalize(rayDirection);

        if (rayDirection.X < 0)
        {
            rayOrigin.X = treeMax.X - rayOrigin.X;
            rayDirection.X = -rayDirection.X;
            childIndexModifier = 4;
        }

        if (rayDirection.Y < 0)
        {
            rayOrigin.Y = treeMax.Y - rayOrigin.Y;
            rayDirection.Y = -rayDirection.Y;
            childIndexModifier |= 2;
        }

        if (rayDirection.Z < 0)
        {
            rayOrigin.Z = treeMax.Z - rayOrigin.Z;
            rayDirection.Z = -rayDirection.Z;
            childIndexModifier |= 1;
        }

        var rayDirectionInverse = Vector3.One / rayDirection;

        var t0 = (treeMin - rayOrigin) * rayDirectionInverse;
        var t1 = (treeMax - rayOrigin) * rayDirectionInverse;

        if (Math.Max(Math.Max(t0.X, t0.Y), t0.Z) > Math.Min(Math.Min(t1.X, t1.Y), t1.Z))
            return;

        var stack = (stackalloc RayCastStackIndex[VoxelOctree.MaxDepth + 1]);

        int stackIndex = 0;
        stack[stackIndex] = new RayCastStackIndex()
        {
            NodeIndex = 0,
            LastChildIndex = -1,
            T0 = t0,
            T1 = t1
        };

        var octree = Octree;

        while (stackIndex >= 0)
        {
            var currentEntry = stack[stackIndex--];

            t0 = currentEntry.T0;
            t1 = currentEntry.T1;

            var nodeIndex = currentEntry.NodeIndex;
            ref var node = ref octree.GetNode(nodeIndex);

            if (t1.X < 0 || t1.Y < 0 || t1.Z < 0
                || (t0.X > maximumT && t0.Y > maximumT && t0.Z > maximumT))
                continue;

            if (node.IsLeaf)
            {
                if (node.IsEmpty)
                    continue;

                if (!hitHandler.AllowTest((int) node.DataIndex))
                    continue;

                float t;
                Vector3 normal;

                if (t0.X > t0.Y && t0.X > t0.Z)
                {
                    if (t0.X > maximumT)
                        return;

                    t = t0.X;

                    normal = ray.Direction.X > 0 ? new Vector3(-1, 0, 0) : new Vector3(1, 0, 0);
                }
                else if (t0.Y > t0.Z)
                {
                    if (t0.Y > maximumT)
                        return;

                    t = t0.Y;

                    normal = ray.Direction.Y > 0 ? new Vector3(0, -1, 0) : new Vector3(0, 1, 0);
                }
                else
                {
                    if (t0.Z > maximumT)
                        return;

                    t = t0.Z;

                    normal = ray.Direction.Z > 0 ? new Vector3(0, 0, -1) : new Vector3(0, 0, 1);
                }

                hitHandler.OnRayHit(ray, ref maximumT, t, normal, (int) node.DataIndex);

                if (t >= maximumT)
                    return;
            }

            var tm = (t0 + t1) * 0.5f;

            var lastChildIndex = currentEntry.LastChildIndex;
            var nextChildIndex = GetNextChildIndex(lastChildIndex, t0, t1, tm);

            if (nextChildIndex >= 8)
                continue;

            GetChildT(nextChildIndex, t0, t1, tm, out var childT0, out var childT1);

            stackIndex++;
            currentEntry.LastChildIndex = nextChildIndex;
            stack[stackIndex++] = currentEntry;

            var nodeChildren = node.GetChildIndex((uint) (nextChildIndex ^ childIndexModifier));
            stack[stackIndex] = new()
            {
                NodeIndex = nodeChildren,
                LastChildIndex = -1,
                T0 = childT0,
                T1 = childT1
            };
        }
    }

    private struct RayCastStackIndex
    {
        public uint NodeIndex;
        public int LastChildIndex;
        public Vector3 T0;
        public Vector3 T1;
    }

    public void RayTest<TRayHitHandler>(in RigidPose pose, ref RaySource rays, ref TRayHitHandler hitHandler)
        where TRayHitHandler : struct, IShapeRayHitHandler
    {
        for (int i = 0; i < rays.RayCount; i++)
        {
            rays.GetRay(i, out var ray, out var maximumT);
            RayTest(pose, Unsafe.AsRef<RayData>(ray), ref Unsafe.AsRef<float>(maximumT), ref hitHandler);
        }
    }

    public void GetLocalChild(int childIndex, out Box childData)
    {
        var node = Octree.GetNode(Octree.Data.ownerNodes[childIndex]);
        var halfSize = VoxelOctree.Dimensions / (float) (1 << (node.Depth + 1));
        childData = new Box(halfSize, halfSize, halfSize);
    }

    public void GetPosedLocalChild(int childIndex, out Box childData, out RigidPose childPose)
    {
        var octree = Octree;

        ref var node = ref octree.GetNode(octree.Data.ownerNodes[childIndex]);
        var halfSize = VoxelOctree.Dimensions / (float) (1 << (node.Depth + 1));
        childData = new Box(halfSize, halfSize, halfSize);

        childPose.Orientation = Quaternion.Identity;
        childPose.Position = Vector3.Zero;

        Vector3 nodeSize = new Vector3(halfSize);
        while (node.Depth != 0)
        {
            var offset = VoxelOctree.GetChildOffset(node.ParentChildIndex);
            offset += Vector3.One;
            offset *= nodeSize;
            childPose.Position += offset;

            nodeSize *= 2;
            node = ref octree.GetParentNode(ref node);
        }
    }

    public void GetLocalChild(int childIndex, ref BoxWide childData)
    {
        var octree = Octree;
        var node = octree.GetNode(octree.Data.ownerNodes[childIndex]);
        var halfSize = VoxelOctree.Dimensions / (float) (1 << (node.Depth + 1));
        GatherScatter.GetFirst(ref childData.HalfHeight) = halfSize;
        GatherScatter.GetFirst(ref childData.HalfWidth) = halfSize;
        GatherScatter.GetFirst(ref childData.HalfLength) = halfSize;
    }

    public void Dispose(BufferPool pool)
    {
        _octreeHandle.Free();
    }

    public int ChildCount => (int) Octree.DataCount;
}