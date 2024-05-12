using System.Numerics;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Components.Common.Physic;
using Techardry.Identifications;

namespace Techardry.Systems.Common.Physics;

[RegisterSystem("collider_sync_client")]
[ExecuteInSystemGroup<PhysicsProcessingSystemGroup>]
[ExecutionSide(GameType.Client)]
public partial class ColliderSyncClientSystem : ASystem
{
    [ComponentQuery] private readonly ComponentQuery<(Collider, Body, Position, Rotation)> _colliderQuery = new();
    public override Identification Identification => SystemIDs.ColliderSyncClient;

    public override void Setup(SystemManager systemManager)
    {
        _colliderQuery.Setup(this);
    }

    private const float PositionSmoothing = 0.1f;
    private const float RotationSmoothing = 0.1f;
    private const float VelocitySmoothing = 0.1f;
    private const float AngularVelocitySmoothing = 0.1f;

    private const float RotationSnapThreshold = 30;

    private const float MaxPositionError = 2;
    private const float PositionExtrapolationTime = 0.2f;

    protected override void Execute()
    {
        if (World is null) return;

        var physicsWorld = World.PhysicsWorld;
        var simulation = physicsWorld.Simulation;

        foreach (var entity in _colliderQuery)
        {
            ref var collider = ref entity.GetCollider();
            ref var body = ref entity.GetBody();
            ref var positionComp = ref entity.GetPosition();
            ref var rotationComp = ref entity.GetRotation();

            if (!collider.AddedToPhysicsWorld) continue;

            var bodyReference = simulation.Bodies.GetBodyReference(collider.Handle);

            ref var position = ref bodyReference.Pose.Position;
            ref var rotation = ref bodyReference.Pose.Orientation;
            ref var velocity = ref bodyReference.Velocity.Linear;
            ref var angularVelocity = ref bodyReference.Velocity.Angular;

            var serverPosition = body.Pose.Position;
            var serverRotation = body.Pose.Orientation;
            var serverVelocity = body.Velocity.Linear;
            var serverAngularVelocity = body.Velocity.Angular;

            // Smooth Interpolation (rotation and angular velocity)
            rotation = Quaternion.Slerp(rotation, serverRotation, RotationSmoothing);
            angularVelocity = Vector3.Lerp(angularVelocity, serverAngularVelocity, AngularVelocitySmoothing);

            var rotationDot = Math.Clamp(Quaternion.Dot(rotation, serverRotation), -1, 1);
            var angleDifference = Math.Acos(rotationDot) * 2 * 180 / MathF.PI;

            // Snapping (rotation)
            if (angleDifference > RotationSnapThreshold)
                rotation = serverRotation;

            // Velocity-Based Extrapolation & Smoothing (position and velocity)
            var extrapolatedPosition = serverPosition + serverVelocity * PositionExtrapolationTime;
            var positionError = Vector3.Distance(position, extrapolatedPosition);

            if (positionError > MaxPositionError)
            {
                // Snap if error is too large
                position = serverPosition;
                velocity = serverVelocity;
            }
            else
            {
                // Smooth interpolation towards extrapolated position
                position = Vector3.Lerp(position, extrapolatedPosition, PositionSmoothing);
                velocity = Vector3.Lerp(velocity, serverVelocity, VelocitySmoothing);
            }

            positionComp.Value = position;
            rotationComp.Value = rotation;

            positionComp.Dirty = true;
            rotationComp.Dirty = true;
        }
    }
}