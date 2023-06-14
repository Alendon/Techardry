using MintyCore.Render;
using Silk.NET.Vulkan;

namespace Techardry.Render;

public class FontTextureWrapper : IDisposable
{
    public required Texture Texture { get; set; }
    public required Texture StagingTexture { get; set; }
    public bool Changed { get; set; }
    public required ImageView ImageView { get; set; }
    public required Sampler Sampler { get; set; }
    public required DescriptorSet SampledImageDescriptorSet { get; set; }

    public void ApplyChanges(CommandBuffer commandBuffer)
    {
        if (!Changed) return;

        Texture.CopyTo(commandBuffer, (StagingTexture, 0, 0, 0, 0, 0), (Texture, 0, 0, 0, 0, 0), Texture.Width,
            Texture.Height, 1, 1);
        Changed = false;
    }

    public unsafe void Dispose()
    {
        DescriptorSetHandler.FreeDescriptorSet(SampledImageDescriptorSet);
        VulkanEngine.Vk.DestroySampler(VulkanEngine.Device, Sampler, VulkanEngine.AllocationCallback);
        VulkanEngine.Vk.DestroyImageView(VulkanEngine.Device, ImageView, VulkanEngine.AllocationCallback);

        Texture.Dispose();
        StagingTexture.Dispose();
    }
}