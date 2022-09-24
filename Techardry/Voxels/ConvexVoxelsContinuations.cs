using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuUtilities;

namespace Techardry.Voxels;

public unsafe struct ConvexVoxelsContinuations : IConvexCompoundContinuationHandler<NonconvexReduction>
{
    public ref NonconvexReduction CreateContinuation<TCallbacks>(ref CollisionBatcher<TCallbacks> collisionBatcher,
        int childCount,
        in BoundsTestedPair pair, in OverlapQueryForPair queryForPair, out int continuationIndex)
        where TCallbacks : struct, ICollisionCallbacks
    {
        return ref collisionBatcher.NonconvexReductions.CreateContinuation(childCount, collisionBatcher.Pool,
            out continuationIndex);
    }

    public static void GetChildData<TCallbacks>(ref CollisionBatcher<TCallbacks> collisionBatcher,
        ref NonconvexReductionChild continuationChild,
        in BoundsTestedPair pair, int shapeTypeA, int childIndexB, out RigidPose childPoseB, out int childTypeB,
        out void* childShapeDataB)
        where TCallbacks : struct, ICollisionCallbacks
    {
        ref var collider = ref Unsafe.AsRef<VoxelCollider>(pair.B);
        collider.GetPosedLocalChild(childIndexB, out var childData, out childPoseB);

        QuaternionEx.TransformWithoutOverlap(childPoseB.Position, pair.OrientationB, out childPoseB.Position);
        childTypeB = Box.Id;

        collisionBatcher.CacheShapeB(shapeTypeA, childTypeB, Unsafe.AsPointer(ref childData.HalfHeight),
            VoxelCollider.Id, out childShapeDataB);
    }

    public void ConfigureContinuationChild<TCallbacks>(ref CollisionBatcher<TCallbacks> collisionBatcher,
        ref NonconvexReduction continuation,
        int continuationChildIndex, in BoundsTestedPair pair, int shapeTypeA, int childIndexB, out RigidPose childPoseB,
        out int childTypeB, out void* childShapeDataB) where TCallbacks : struct, ICollisionCallbacks
    {
        ref var continuationChild = ref continuation.Children[continuationChildIndex];

        GetChildData(ref collisionBatcher, ref continuationChild, pair, shapeTypeA, childIndexB, out childPoseB,
            out childTypeB, out childShapeDataB);

        if (pair.FlipMask < 0)
        {
            continuationChild.ChildIndexA = childIndexB;
            continuationChild.ChildIndexB = 0;
            
            continuationChild.OffsetA = childPoseB.Position;
            continuationChild.OffsetB = default;
        }
        else
        {
            continuationChild.ChildIndexA = 0;
            continuationChild.ChildIndexB = childIndexB;
            
            continuationChild.OffsetA = default;
            continuationChild.OffsetB = childPoseB.Position;
        }
    }

    public CollisionContinuationType CollisionContinuationType => CollisionContinuationType.NonconvexReduction;
}