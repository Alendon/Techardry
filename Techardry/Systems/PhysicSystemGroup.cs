using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Identifications;
using MintyCore.Physics;
using MintyCore.Registries;
using MintyCore.Utils;

namespace Techardry.Systems;

/// <summary>
///     System group for physics
/// </summary>
[RegisterSystem("physic_group")]
[ExecuteInSystemGroup<InitializationSystemGroup>]
public class PhysicSystemGroup(IGameTimer timer) : ASystemGroup
{
    private float _accumulatedDeltaTime;

    /// <inheritdoc />
    public override Identification Identification => SystemIDs.PhysicGroup;
    

    /// <inheritdoc />
    public override Task QueueSystem(IEnumerable<Task> dependency)
    {
        _accumulatedDeltaTime += timer.DeltaTime;
        
        var dependencies = dependency.ToList();
        while (_accumulatedDeltaTime >= PhysicsWorld.FixedDeltaTime)
        {
            dependencies.Add(base.QueueSystem(dependencies));
            _accumulatedDeltaTime -= PhysicsWorld.FixedDeltaTime;
        }

        return Task.WhenAll(dependencies);
    }
}