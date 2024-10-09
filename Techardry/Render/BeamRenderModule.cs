using MintyCore.Graphics;
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

[RegisterRenderModule("beam")]
public class BeamRenderModule(
    IVulkanEngine vulkanEngine,
    IPipelineManager pipelineManager) : RenderModule
{
    private Func<WorldIntermediateData?>? _worldIntermediateDataFunc;
    private Func<CameraIntermediateData?>? _cameraIntermediateDataFunc;
    public const int BeamSizeFactor = 4;

    public override void Setup()
    {
        _worldIntermediateDataFunc =
            ModuleDataAccessor.UseIntermediateData<WorldIntermediateData>(IntermediateRenderDataIDs.World, this);
        _cameraIntermediateDataFunc =
            ModuleDataAccessor.UseIntermediateData<CameraIntermediateData>(IntermediateRenderDataIDs.Camera, this);

        ModuleDataAccessor.SetColorAttachment(RenderDataIDs.Beam, this);
    }

    public override unsafe void Render(ManagedCommandBuffer commandBuffer)
    {
        if (_worldIntermediateDataFunc is null || _cameraIntermediateDataFunc is null)
        {
            Log.Error("Beam Render Module is not setup correctly");
            return;
        }

        var worldIntermediateData = _worldIntermediateDataFunc();
        var cameraIntermediateData = _cameraIntermediateDataFunc();

        if (worldIntermediateData is null || cameraIntermediateData is null)
        {
            return;
        }

        var pipeline = pipelineManager.GetPipeline(PipelineIDs.VoxelBeam);
        var pipelineLayout = pipelineManager.GetPipelineLayout(PipelineIDs.VoxelBeam);

        var cb = commandBuffer.InternalCommandBuffer;
        var vk = vulkanEngine.Vk;

        vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, pipeline);

        var extent = BeamExtent(vulkanEngine);
        ReadOnlySpan<Viewport> viewports =
        [
            new Viewport(0, 0, extent.Width, extent.Height, 0, 1)
        ];
        vk.CmdSetViewport(cb, 0, viewports);

        ReadOnlySpan<Rect2D> scissors =
        [
            new Rect2D(new Offset2D(0, 0), extent)
        ];
        vk.CmdSetScissor(cb, 0, scissors);

        ReadOnlySpan<DescriptorSet> descriptorSets =
        [
            cameraIntermediateData.CameraDescriptorSet,
            worldIntermediateData.WorldDataDescriptorSet
        ];

        vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, pipelineLayout, 0, descriptorSets, 0, null);

        vk.CmdDraw(cb, 6, 1, 0, 0);
    }

    public override void Dispose()
    {
    }

    public override Identification Identification => RenderModuleIDs.Beam;

    [RegisterRenderTexture("beam")]
    internal static RenderTextureDescription BeamTexture(IVulkanEngine vulkanEngine) => new(
        (Func<Extent2D>)(() => BeamExtent(vulkanEngine)), Format.R32Sfloat
    );

    internal static Extent2D BeamExtent(IVulkanEngine vulkanEngine) => new(
        vulkanEngine.SwapchainExtent.Width / BeamSizeFactor,
        vulkanEngine.SwapchainExtent.Height / BeamSizeFactor);
}