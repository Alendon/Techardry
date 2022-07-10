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

    private Texture _depthTexture;
    private ImageView _depthImageView;
    private Framebuffer[] _framebuffers = Array.Empty<Framebuffer>();

    public override void Setup(SystemManager systemManager)
    {
        SetRenderPassArguments(new RenderPassArguments()
        {
            RenderPass = RenderPassIDs.DualPipeline
        });
        
        CreateDepthImage();
        CreateFramebuffers();
        
        base.Setup(systemManager);
    }

    private unsafe void CreateDepthImage()
    {
        TextureDescription desc = TextureDescription.Texture2D(VulkanEngine.SwapchainExtent.Width,
            VulkanEngine.SwapchainExtent.Height, 1, 1, Format.D16Unorm, TextureUsage.DepthStencil);

        desc.AdditionalUsageFlags = ImageUsageFlags.ImageUsageInputAttachmentBit;
        _depthTexture = Texture.Create(ref desc);

        ImageViewCreateInfo createInfo = new()
        {
            SType =  StructureType.ImageViewCreateInfo,
            /*Components = new ComponentMapping()
            {
                A = ComponentSwizzle.A,
                R = ComponentSwizzle.R,
                G = ComponentSwizzle.G,
                B = ComponentSwizzle.B
            },*/
            Flags = 0,
            Format = Format.D16Unorm,
            Image = _depthTexture.Image,
            SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ImageAspectDepthBit | ImageAspectFlags.ImageAspectStencilBit,
                LayerCount = 1,
                LevelCount = 1,
                BaseArrayLayer = 0,
                BaseMipLevel = 0
            },
            ViewType = ImageViewType.ImageViewType2D
        };

        VulkanEngine.Vk.CreateImageView(VulkanEngine.Device, createInfo, VulkanEngine.AllocationCallback,
            out _depthImageView);
    }

    private unsafe void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[VulkanEngine.SwapchainImageCount];

        Span<ImageView> imageViews = stackalloc ImageView[2];
        imageViews[1] = VulkanEngine.DepthImageView;

        for (var i = 0; i < VulkanEngine.SwapchainImageCount; i++)
        {
            imageViews[0] = VulkanEngine.SwapchainImageViews[i];

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

    protected override void PreExecuteSystem(ASystem system)
    {
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