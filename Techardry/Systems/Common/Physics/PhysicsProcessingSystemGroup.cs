using MintyCore.ECS;
using MintyCore.ECS.SystemGroups;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.Systems.Common.Physics;

[RegisterSystem("physics_processing_group")]
[ExecuteInSystemGroup<SimulationSystemGroup>]
public class PhysicsProcessingSystemGroup : ASystemGroup
{
    public override Identification Identification => SystemIDs.PhysicsProcessingGroup;
}