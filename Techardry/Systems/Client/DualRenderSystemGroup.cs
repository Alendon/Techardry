using System.Runtime.CompilerServices;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.SystemGroups;
using MintyCore.Systems.Client;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Systems.Client;

[RegisterSystem("dual_render")]
[ExecuteAfter(typeof(ApplyGpuCameraBufferSystem))]
[ExecuteInSystemGroup(typeof(PresentationSystemGroup))]
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
}