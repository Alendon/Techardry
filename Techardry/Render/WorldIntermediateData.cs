﻿using MintyCore.Graphics.Render.Data;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Render;

[RegisterIntermediateRenderDataByType("world")]
public class WorldIntermediateData : IntermediateData
{
    public MemoryBuffer? BvhNodeBuffer;
    public MemoryBuffer? BvhIndexBuffer;
    
    
    public DescriptorSet WorldDataDescriptorSet;
    public DescriptorPool WorldDataDescriptorPool;
    
    /// <summary>
    /// How many chunks/entity buffers can be stored in the descriptor set
    /// </summary>
    public uint DescriptorSetCapacity;

    public override void Clear()
    {
        // TODO Is cleaning needed? If its get cached we save some recreation time
    }

    public override void Dispose()
    {
        BvhNodeBuffer?.Dispose();
        BvhIndexBuffer?.Dispose();
        
        //TODO Dispose descriptor set and pool
    }

    public override Identification Identification => IntermediateRenderDataIDs.World;
}