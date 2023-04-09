using MintyCore.Registries;
using Techardry.Identifications;

namespace Techardry.Render;

internal static class InstancedRenderDatas
{
    [RegisterInstancedRenderData("dual_block")]
    public static InstancedRenderDataInfo DualBlockRenderData => new()
    {
        MeshId = MeshIDs.Cube,
        MaterialIds = new[]
        {
            MaterialIDs.DualBlock
        }
    };
}