using MintyCore.Identifications;
using MintyCore.Registries;

namespace Techardry.Entities;

public static class Archetypes
{
    [RegisterArchetype("test_camera")]
    public static ArchetypeInfo TestCamera => new()
    {
        ComponentIDs = new []
        {
            ComponentIDs.Camera,
            ComponentIDs.Position
        },
        AdditionalDlls = new []
        {
            typeof(Silk.NET.Vulkan.DescriptorSet).Assembly.Location
        }
    };

    [RegisterArchetype("test_render")]
    public static ArchetypeInfo TestRender => new()
    {
        ComponentIDs = new []
        {
            ComponentIDs.Position,
            ComponentIDs.InstancedRenderAble,
            ComponentIDs.Transform,
            ComponentIDs.Scale,
            ComponentIDs.Rotation,
        }
    };
}