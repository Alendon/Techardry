﻿using MintyCore.Graphics;
using MintyCore.Graphics.Managers;
using MintyCore.Graphics.Render;
using MintyCore.Graphics.Render.Data;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Serilog;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Render;

[RegisterRenderModule("world")]
public class WorldRenderModule(
    IVulkanEngine vulkanEngine,
    IPipelineManager pipelineManager,
    ITextureAtlasHandler textureAtlasHandler) : RenderModule
{
    private Func<WorldIntermediateData?>? _worldIntermediateDataFunc;
    private Func<CameraIntermediateData?>? _cameraIntermediateDataFunc;
    private Func<DescriptorSet>? _beamDescriptor;
    
    private uint _frame;

    public override IEnumerable<Identification> ExecuteAfter => [RenderModuleIDs.Beam];

    public override void Setup()
    {
        _worldIntermediateDataFunc =
            ModuleDataAccessor.UseIntermediateData<WorldIntermediateData>(IntermediateRenderDataIDs.World, this);
        _cameraIntermediateDataFunc =
            ModuleDataAccessor.UseIntermediateData<CameraIntermediateData>(IntermediateRenderDataIDs.Camera, this);
        
        ModuleDataAccessor.SetColorAttachment(new Swapchain(), this);
        _beamDescriptor = ModuleDataAccessor.UseSampledTexture(RenderDataIDs.Beam, this, ColorAttachmentSampleMode.Nearest);
    }

    public override unsafe void Render(ManagedCommandBuffer commandBuffer)
    {
        if (_worldIntermediateDataFunc is null || _cameraIntermediateDataFunc is null || _beamDescriptor is null)
        {
            Log.Error("World Render Module is not setup correctly");
            return;
        }

        var worldIntermediateData = _worldIntermediateDataFunc();
        var cameraIntermediateData = _cameraIntermediateDataFunc();

        if (worldIntermediateData is null || cameraIntermediateData is null)
        {
            return;
        }

        if (!textureAtlasHandler.TryGetAtlasDescriptorSet(TextureAtlasIDs.BlockTexture,
                out var blockTextureDescriptorSet))
        {
            Log.Error("Block Texture Atlas not found");
            return;
        }

        var pipeline = pipelineManager.GetPipeline(PipelineIDs.Voxel);
        var pipelineLayout = pipelineManager.GetPipelineLayout(PipelineIDs.Voxel);

        var cb = commandBuffer.InternalCommandBuffer;
        var vk = vulkanEngine.Vk;

        vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, pipeline);

        ReadOnlySpan<Viewport> viewports =
        [
            new Viewport(0, 0, vulkanEngine.SwapchainExtent.Width, vulkanEngine.SwapchainExtent.Height, 0, 1)
        ];
        vk.CmdSetViewport(cb, 0, viewports);

        ReadOnlySpan<Rect2D> scissors =
        [
            new Rect2D(new Offset2D(0, 0), vulkanEngine.SwapchainExtent)
        ];
        vk.CmdSetScissor(cb, 0, scissors);

        var beamDescriptor = _beamDescriptor();
        
        ReadOnlySpan<DescriptorSet> descriptorSets =
        [
            cameraIntermediateData.CameraDescriptorSet,
            blockTextureDescriptorSet,
            worldIntermediateData.WorldDataDescriptorSet,
            beamDescriptor
        ];

        vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, pipelineLayout, 0, descriptorSets, 0, null);
        
        Span<uint> pushConstant = stackalloc uint[]
        {
            _frame++
        };
        vk.CmdPushConstants(cb, pipelineLayout, ShaderStageFlags.FragmentBit, 0, sizeof(uint), pushConstant);
        
        vk.CmdDraw(cb, 6, 1, 0, 0);
    }

    public override void Dispose()
    {
    }

    public override Identification Identification => RenderModuleIDs.World;
}