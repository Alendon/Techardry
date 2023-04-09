using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.Render;

internal static class Materials
{
    [RegisterMaterial("dual_block")]
    public static MaterialInfo DualBlock => new()
    {
        DescriptorSets = new[]
        {
            (TextureIDs.Dirt, 1u)
        },
        PipelineId = PipelineIDs.DualTexture
    };
    
    [RegisterMaterial("triangle")]
    internal static MaterialInfo TriangleInfo => new()
    {
        PipelineId = PipelineIDs.Color,
        DescriptorSets = Array.Empty<(Identification, uint)>()
    };
    
    [RegisterMaterial("ui_overlay")]
    internal static MaterialInfo UiOverlayInfo => new()
    {
        PipelineId = PipelineIDs.UiOverlay,
        DescriptorSets = Array.Empty<(Identification, uint)>()
    };
}