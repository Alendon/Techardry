﻿namespace Techardry.Systems.Client;
/*
[RegisterSystem("dual_render")]
[ExecuteAfter<ApplyGpuCameraBufferSystem>]
[ExecuteInSystemGroup<PresentationSystemGroup>]
public class DualRenderSystemGroup : ARenderSystemGroup
{
    public override Identification Identification => SystemIDs.DualRender;

    private Framebuffer[] _framebuffers = Array.Empty<Framebuffer>();
    private ImageView[] _colorImageViews = Array.Empty<ImageView>();

    public override void Setup(SystemManager systemManager)
    {
        SetRenderPassArguments(new RenderPassArguments()
        {
            RenderPass = RenderPassIDs.DualPipeline
        });

        CreateFramebuffers();

        base.Setup(systemManager);
    }

    public override unsafe void Dispose()
    {
        foreach (var framebuffer in _framebuffers)
        {
            VulkanEngine.Vk.DestroyFramebuffer(VulkanEngine.Device, framebuffer, VulkanEngine.AllocationCallback);
        }
        
        base.Dispose();
    }

    private unsafe void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[VulkanEngine.SwapchainImageCount];
        _colorImageViews = new ImageView[VulkanEngine.SwapchainImageCount];

        Span<ImageView> imageViews = stackalloc ImageView[2];
        imageViews[1] = VulkanEngine.DepthImageView;

        for (var i = 0; i < VulkanEngine.SwapchainImageCount; i++)
        {
            imageViews[0] = VulkanEngine.SwapchainImageViews[i];
            _colorImageViews[i] = imageViews[0];

            FramebufferCreateInfo createInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                Flags = 0,
                Height = VulkanEngine.SwapchainExtent.Height,
                Width = VulkanEngine.SwapchainExtent.Width,
                Layers = 1,
                AttachmentCount = (uint) imageViews.Length,
                PAttachments = (ImageView*) Unsafe.AsPointer(ref imageViews.GetPinnableReference()),
                RenderPass = RenderPassHandler.GetRenderPass(RenderPassIDs.DualPipeline)
            };

            VulkanEngine.Vk.CreateFramebuffer(VulkanEngine.Device, createInfo, VulkanEngine.AllocationCallback,
                out _framebuffers[i]);
        }
    }

    protected override unsafe void PreExecuteSystem(ASystem system)
    {
        if (_colorImageViews[VulkanEngine.ImageIndex].Handle !=
            VulkanEngine.SwapchainImageViews[VulkanEngine.ImageIndex].Handle)
        {
            ref var framebuffer = ref _framebuffers[VulkanEngine.ImageIndex];
            
            VulkanEngine.Vk.DestroyFramebuffer(VulkanEngine.Device, framebuffer, VulkanEngine.AllocationCallback);
            
            _colorImageViews[VulkanEngine.ImageIndex] = VulkanEngine.SwapchainImageViews[VulkanEngine.ImageIndex];
            
            Span<ImageView> imageViews = stackalloc ImageView[2];
            imageViews[0] = VulkanEngine.SwapchainImageViews[VulkanEngine.ImageIndex];
            imageViews[1] = VulkanEngine.DepthImageView;
            
            FramebufferCreateInfo createInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                Flags = 0,
                Height = VulkanEngine.SwapchainExtent.Height,
                Width = VulkanEngine.SwapchainExtent.Width,
                Layers = 1,
                AttachmentCount = (uint) imageViews.Length,
                PAttachments = (ImageView*) Unsafe.AsPointer(ref imageViews.GetPinnableReference()),
                RenderPass = RenderPassHandler.GetRenderPass(RenderPassIDs.DualPipeline)
            };
            
            VulkanEngine.Vk.CreateFramebuffer(VulkanEngine.Device, createInfo, VulkanEngine.AllocationCallback,
                out framebuffer);
        }

        base.PreExecuteSystem(system);
    }

    protected override void PostExecuteSystem(ASystem system)
    {
        if (ActiveRenderPass == null)
        {
            var renderPass = RenderPassHandler.GetRenderPass(RenderArguments.RenderPass!.Value);
            VulkanEngine.SetActiveRenderPass(renderPass, SubpassContents.SecondaryCommandBuffers,
                framebuffer: _framebuffers[VulkanEngine.ImageIndex]);
            ActiveRenderPass = renderPass;
        }

        base.PostExecuteSystem(system);
    }

    public override void PreExecuteMainThread()
    {
        CurrentSubpass = 0;
        base.PreExecuteMainThread();
    }
}*/