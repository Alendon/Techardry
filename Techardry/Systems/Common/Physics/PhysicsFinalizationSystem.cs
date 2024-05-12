using System.Diagnostics;
using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.World;

namespace Techardry.Systems.Common.Physics;

[ExecuteInSystemGroup<FinalizationSystemGroup>]
[RegisterSystem("physics_finalization")]
public class PhysicsFinalizationSystem : ASystem
{
    private Stopwatch? _stopwatch;
    
    
    public override void Setup(SystemManager systemManager)
    {
    }

    protected override void Execute()
    {
        if (World is not TechardryWorld world) return;
        
        //by only starting the stopwatch when the system first runs, we can avoid a long first frame
        _stopwatch ??= Stopwatch.StartNew();

        world.BeginPhysicsStep(_stopwatch.Elapsed.TotalSeconds);
        
        _stopwatch.Restart();
    }

    public override Identification Identification => SystemIDs.PhysicsFinalization;
}