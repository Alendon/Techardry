using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuUtilities.Memory;

namespace Techardry.Voxels;

public unsafe struct CompoundVoxelsContinuations<TCompoundA> : ICompoundPairContinuationHandler<NonconvexReduction>
    where TCompoundA : ICompoundShape
{
    public ref NonconvexReduction CreateContinuation<TCallbacks>(ref CollisionBatcher<TCallbacks> collisionBatcher,
        int childCount,
        ref Buffer<ChildOverlapsCollection> pairOverlaps, ref Buffer<OverlapQueryForPair> pairQueries,
        in BoundsTestedPair pair, out int continuationIndex) where TCallbacks : struct, ICollisionCallbacks
    {
        return ref collisionBatcher.NonconvexReductions.CreateContinuation(childCount, collisionBatcher.Pool,
            out continuationIndex);
    }

    public void GetChildAData<TCallbacks>(ref CollisionBatcher<TCallbacks> collisionBatcher,
        ref NonconvexReduction continuation,
        in BoundsTestedPair pair, int childIndexA, out RigidPose childPoseA, out int childTypeA,
        out void* childShapeDataA) where TCallbacks : struct, ICollisionCallbacks
    {
        ref var compoundA = ref Unsafe.AsRef<TCompoundA>(pair.A);
        ref var compoundChildA = ref compoundA.GetChild(childIndexA);

        Compound.GetRotatedChildPose(compoundChildA.LocalPose, pair.OrientationA, out childPoseA);
        childTypeA = compoundChildA.ShapeIndex.Type;
        collisionBatcher.Shapes[childTypeA].GetShapeData(compoundChildA.ShapeIndex.Index, out childShapeDataA, out _);
    }

    public void ConfigureContinuationChild<TCallbacks>(ref CollisionBatcher<TCallbacks> collisionBatcher,
        ref NonconvexReduction continuation,
        int continuationChildIndex, in BoundsTestedPair pair, int childIndexA, int childTypeA, int childIndexB,
        in RigidPose childPoseA, out RigidPose childPoseB, out int childTypeB, out void* childShapeDataB)
        where TCallbacks : struct, ICollisionCallbacks
    {
        ref var continuationChild = ref continuation.Children[continuationChildIndex];

        ConvexVoxelsContinuations.GetChildData(ref collisionBatcher, ref continuationChild, pair, childTypeA,
            childIndexB, out childPoseB, out childTypeB, out childShapeDataB);

        if (pair.FlipMask < 0)
        {
            continuationChild.ChildIndexA = childIndexB;
            continuationChild.ChildIndexB = childIndexA;
            continuationChild.OffsetA = childPoseB.Position;
            continuationChild.OffsetB = childPoseA.Position;
        }
        else
        {
            continuationChild.ChildIndexA = childIndexA;
            continuationChild.ChildIndexB = childIndexB;
            continuationChild.OffsetA = childPoseA.Position;
            continuationChild.OffsetB = childPoseB.Position;
        }
    }

    public CollisionContinuationType CollisionContinuationType => CollisionContinuationType.NonconvexReduction;
}