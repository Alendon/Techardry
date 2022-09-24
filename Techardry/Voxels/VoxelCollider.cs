using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;

namespace Techardry.Voxels;

public unsafe struct VoxelCollider : IHomogeneousCompoundShape<Box, BoxWide>
{
    readonly VoxelOctree _octree;

    public VoxelCollider(VoxelOctree octree)
    {
        _octree = octree;
    }

    public ShapeBatch CreateShapeBatch(BufferPool pool, int initialCapacity, Shapes shapeBatches)
    {
        throw new NotImplementedException();
    }

    public int TypeId => 12;

    public void FindLocalOverlaps<TOverlaps, TSubpairOverlaps>(ref Buffer<OverlapQueryForPair> pairs, BufferPool pool,
        Shapes shapes,
        ref TOverlaps overlaps) where TOverlaps : struct, ICollisionTaskOverlaps<TSubpairOverlaps>
        where TSubpairOverlaps : struct, ICollisionTaskSubpairOverlaps
    {
        throw new NotImplementedException();
    }

    public void FindLocalOverlaps<TOverlaps>(in Vector3 min, in Vector3 max, in Vector3 sweep, float maximumT,
        BufferPool pool,
        Shapes shapes, void* overlaps) where TOverlaps : ICollisionTaskSubpairOverlaps
    {
        throw new NotImplementedException();
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

    public void RayTest<TRayHitHandler>(in RigidPose pose, in RayData ray, ref float maximumT,
        ref TRayHitHandler hitHandler) where TRayHitHandler : struct, IShapeRayHitHandler
    {
        throw new NotImplementedException();
    }

    public void RayTest<TRayHitHandler>(in RigidPose pose, ref RaySource rays, ref TRayHitHandler hitHandler)
        where TRayHitHandler : struct, IShapeRayHitHandler
    {
        throw new NotImplementedException();
    }

    public void GetLocalChild(int childIndex, out Box childData)
    {
        var node = _octree.GetNode(_octree.Data.ownerNodes[childIndex]);
        var halfSize = VoxelOctree.Dimensions / (float)(1 << (node.Depth + 1));
        childData = new Box(halfSize, halfSize, halfSize);
    }

    public void GetPosedLocalChild(int childIndex, out Box childData, out RigidPose childPose)
    {
        ref var node = ref _octree.GetNode(_octree.Data.ownerNodes[childIndex]);
        var halfSize = VoxelOctree.Dimensions / (float)(1 << (node.Depth + 1));
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
            node = ref _octree.GetParentNode(ref node);
        }
    }

    public void GetLocalChild(int childIndex, ref BoxWide childData)
    {
        var node = _octree.GetNode(_octree.Data.ownerNodes[childIndex]);
        var halfSize = VoxelOctree.Dimensions / (float)(1 << (node.Depth + 1));
        GatherScatter.GetFirst(ref childData.HalfHeight) = halfSize;
        GatherScatter.GetFirst(ref childData.HalfWidth) = halfSize;
        GatherScatter.GetFirst(ref childData.HalfLength) = halfSize;
    }

    public void Dispose(BufferPool pool)
    {
        
    }

    public int ChildCount => (int) _octree.DataCount;
}