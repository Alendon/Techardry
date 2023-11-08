using System.Numerics;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Identifications;
using MintyCore.Registries;
using MintyCore.Utils;

namespace Techardry.Entities;

public static class Archetypes
{
    [RegisterArchetype("test_camera")]
    public static ArchetypeInfo TestCamera => new(new[]
        {
            Identifications.ComponentIDs.Camera,
            ComponentIDs.Position,
            Identifications.ComponentIDs.LastChunk,
            Identifications.ComponentIDs.Input,
            ComponentIDs.Rotation, ComponentIDs.Transform, ComponentIDs.Scale,
        });

 
}