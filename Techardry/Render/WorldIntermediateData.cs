using MintyCore.Graphics.Render.Data;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Render;

[RegisterIntermediateRenderDataByType("world")]
public class WorldIntermediateData : IntermediateData
{
    public MemoryBuffer? WorldGridBuffer;
    public MemoryBuffer? WorldGridHeaderBuffer;
    
    public ulong LastVoxelIntermediateVersion = ulong.MaxValue;
    
    
    public DescriptorSet WorldDataDescriptorSet;


    public override void Clear()
    {
        // TODO Is cleaning needed? If its get cached we save some recreation time
    }

    public override void Dispose()
    {
        WorldGridBuffer?.Dispose();
        WorldGridHeaderBuffer?.Dispose();
        
        //TODO Dispose descriptor set and pool
    }

    public override Identification Identification => IntermediateRenderDataIDs.World;
}