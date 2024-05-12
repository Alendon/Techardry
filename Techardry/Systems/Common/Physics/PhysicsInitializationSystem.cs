using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.World;

namespace Techardry.Systems.Common.Physics;

[ExecuteInSystemGroup<InitializationSystemGroup>]
[RegisterSystem("physics_initialization")]
public class PhysicsInitializationSystem : ASystem
{
    public override void Setup(SystemManager systemManager)
    {
        
    }

    protected override void Execute()
    {
        if (World is not TechardryWorld world) return;

        world.WaitForPhysicsCompletion();
    }

    public override Identification Identification => SystemIDs.PhysicsInitialization;
}