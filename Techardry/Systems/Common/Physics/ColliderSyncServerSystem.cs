using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Components.Common.Physic;
using Techardry.Identifications;

namespace Techardry.Systems.Common.Physics;

[RegisterSystem("collider_sync_server")]
[ExecuteInSystemGroup<PhysicsProcessingSystemGroup>]
[ExecutionSide(GameType.Server)]
public partial class ColliderSyncServerSystem : ASystem
{
    [ComponentQuery] private readonly ComponentQuery<(Collider, Body, Position, Rotation)> _colliderQuery = new();

    public override Identification Identification => SystemIDs.ColliderSyncServer;
    
    public override void Setup(SystemManager systemManager)
    {
        _colliderQuery.Setup(this);
    }

    protected override void Execute()
    {
        if (World is null) return;

        var physicsWorld = World.PhysicsWorld;
        var simulation = physicsWorld.Simulation;
        
        foreach (var entity in _colliderQuery)
        {
            ref var collider = ref entity.GetCollider();
            ref var body = ref entity.GetBody();
            ref var position = ref entity.GetPosition();
            ref var rotation = ref entity.GetRotation();

            var overwritePose = (body.BodyDirty & Body.DirtyFlag.Pose) != 0;
            var overwriteVelocity = (body.BodyDirty & Body.DirtyFlag.Velocity) != 0;

            if (!collider.AddedToPhysicsWorld) continue;

            var bodyReference = simulation.Bodies.GetBodyReference(collider.Handle);
            
            if (overwritePose)
            {
                bodyReference.Pose = body.Pose;
            }
            else
            {
                body.Pose = bodyReference.Pose;
                body.Dirty = true;
            }
            
            if (overwriteVelocity)
            {
                bodyReference.Velocity = body.Velocity;
            }
            else
            {
                body.Velocity = bodyReference.Velocity;
                body.Dirty = true;
            }
            
            position.Value = body.Pose.Position;
            rotation.Value = body.Pose.Orientation;
            
            position.Dirty = true;
            rotation.Dirty = true;
        }
    }

}