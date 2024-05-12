using BepuPhysics;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Components.Common.Physic;
using Techardry.Identifications;
using Techardry.World;

namespace Techardry.Systems.Common.Physics;

[RegisterSystem("collider_lifetime")]
[ExecuteInSystemGroup<PhysicsProcessingSystemGroup>]
public partial class ColliderLifetimeSystem : ASystem
{
    [ComponentQuery] private readonly ComponentQuery<(Collider, Body)> _componentQuery = new();
    private readonly List<BodyHandle> _bodiesToRemove = new();

    public override void Setup(SystemManager systemManager)
    {
        _componentQuery.Setup(this);

        IEntityManager.PreEntityDeleteEvent += OnEntityDelete;
    }

    private void OnEntityDelete(IWorld world, Entity entity)
    {
        if (!ReferenceEquals(World, world)) return;

        ref var collider = ref world.EntityManager.TryGetComponent<Collider>(entity, out var hasCollider);
        if (hasCollider)
        {
            _bodiesToRemove.Add(collider.Handle);
        }
    }

    protected override void Execute()
    {
        if (World is not TechardryWorld world) return;

        var physicsWorld = world.PhysicsWorld;
        var simulation = physicsWorld.Simulation;

        foreach (var entity in _componentQuery)
        {
            ref var collider = ref entity.GetCollider();
            ref var body = ref entity.GetBody();
            var dirtyFlag = body.BodyDirty;
            
            var bodyExists = simulation.Bodies.BodyExists(collider.Handle);
            var recreateBody = (dirtyFlag & Body.DirtyFlag.NeedReplacement) != 0;
            
            //remove body if needed
            if (bodyExists && (recreateBody || body.BodyShouldExists))
            {
                physicsWorld.RemoveBody(collider.Handle);
                
                collider.Handle = new BodyHandle(-1);
                bodyExists = false;
            }
            
            //create body if needed, after creation nothing more needs to be done
            if (!bodyExists && body.BodyShouldExists)
            {
                collider.Handle = physicsWorld.AddBody(body.GetBodyDescription());
                continue;
            }

            //update inertia if needed
            if (bodyExists && (dirtyFlag & Body.DirtyFlag.LocalInertia) != 0)
            {
                simulation.Bodies.GetBodyReference(collider.Handle).LocalInertia = body.LocalInertia;   
            }
        }

        foreach (var bodyHandle in _bodiesToRemove)
        {
            physicsWorld.RemoveBody(bodyHandle);
        }
        
        _bodiesToRemove.Clear();
    }

    public override Identification Identification => SystemIDs.ColliderLifetime;
}