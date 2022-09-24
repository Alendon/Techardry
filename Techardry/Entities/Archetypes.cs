using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.Components.Common.Physic;
using MintyCore.ECS;
using MintyCore.Identifications;
using MintyCore.Registries;
using MintyCore.Utils;
using InstancedRenderDataIDs = Techardry.Identifications.InstancedRenderDataIDs;

namespace Techardry.Entities;

public static class Archetypes
{
    [RegisterArchetype("test_camera")]
    public static ArchetypeInfo TestCamera => new()
    {
        ComponentIDs = new[]
        {
            ComponentIDs.Camera,
            ComponentIDs.Position
        },
        AdditionalDlls = new[]
        {
            typeof(Silk.NET.Vulkan.DescriptorSet).Assembly.Location
        }
    };

    [RegisterArchetype("physic_box")]
    internal static ArchetypeInfo physicBox => new(new[]
    {
        ComponentIDs.Position, ComponentIDs.Rotation,
        ComponentIDs.Scale, ComponentIDs.Transform, ComponentIDs.Mass, ComponentIDs.Collider,
        ComponentIDs.InstancedRenderAble
    }, new PhysicBoxSetup(), new[]
    {
        typeof(BodyHandle).Assembly.Location
    });

    internal class PhysicBoxSetup : IEntitySetup
    {
        public float Mass;
        public Vector3 Position;
        public Vector3 Scale;

        public void GatherEntityData(IWorld world, Entity entity)
        {
            Mass = world.EntityManager.GetComponent<Mass>(entity).MassValue;
            Position = world.EntityManager.GetComponent<Position>(entity).Value;
            Scale = world.EntityManager.GetComponent<Scale>(entity).Value;
        }

        public void SetupEntity(IWorld world, Entity entity)
        {
            world.EntityManager.SetComponent(entity, new Mass {MassValue = Mass}, false);
            world.EntityManager.SetComponent(entity, new Position {Value = Position});
            world.EntityManager.SetComponent(entity, new Scale {Value = Scale}, false);

            var pose = new RigidPose(Position, Quaternion.Identity);
            var shape = new Box(Scale.X, Scale.Y, Scale.Z);
            BodyInertia inertia = default;

            if (Mass != 0) inertia = shape.ComputeInertia(Mass);

            var description = BodyDescription.CreateDynamic(pose, inertia,
                new CollidableDescription(world.PhysicsWorld.AddShape(shape), 10),
                new BodyActivityDescription(0.1f));

            var handle = world.PhysicsWorld.AddBody(description);
            world.EntityManager.SetComponent(entity, new Collider {BodyHandle = handle}, false);

            var boxRender = new InstancedRenderAble
            {
                MaterialMeshCombination = InstancedRenderDataIDs.DualBlock
            };

            world.EntityManager.SetComponent(entity, boxRender);
        }

        public void Serialize(DataWriter writer)
        {
            writer.Put(Mass);
            writer.Put(Position);
            writer.Put(Scale);
        }

        public bool Deserialize(DataReader reader)
        {
            if (!reader.TryGetFloat(out var mass)
                || !reader.TryGetVector3(out var position)
                || !reader.TryGetVector3(out var scale))
                return false;

            Mass = mass;
            Position = position;
            Scale = scale;
            return true;
        }
    }

    [RegisterArchetype("test_render")]
    public static ArchetypeInfo TestRender => new()
    {
        ComponentIDs = new[]
        {
            ComponentIDs.Position,
            ComponentIDs.InstancedRenderAble,
            ComponentIDs.Transform,
            ComponentIDs.Scale,
            ComponentIDs.Rotation,
        }
    };
}