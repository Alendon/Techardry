using MintyCore.Graphics.Render.Data;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Render;

[RegisterIntermediateRenderDataByType("camera")]
public class CameraIntermediateData : IntermediateData
{
    public MemoryBuffer? CameraBuffer;
    public DescriptorSet CameraDescriptorSet;
    
    public override void Clear()
    {
        //clearing is not needed
        //The data gets overwritten the next time it is used
        //Also we do want to recreate the gpu data every time this instance is not used
    }

    public override void Dispose()
    {
        CameraBuffer?.Dispose();
        //the descriptor set does not need to be explicitly disposed
    }

    public override Identification Identification => IntermediateRenderDataIDs.Camera;

    
}