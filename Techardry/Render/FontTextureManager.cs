using System.Drawing;
using FontStashSharp.Interfaces;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Render;

public class FontTextureManager : ITexture2DManager
{
    private List<FontTextureWrapper> _managedTextures = new();
    public IReadOnlyList<FontTextureWrapper> ManagedTextures => _managedTextures;

    public unsafe object CreateTexture(int width, int height)
    {
        var description = TextureDescription.Texture2D((uint)width, (uint)height, 1, 1, Format.R8G8B8A8Unorm,
            TextureUsage.Sampled);
        var stagingDescription = TextureDescription.Texture2D((uint)width, (uint)height, 1, 1, Format.R8G8B8A8Unorm,
            TextureUsage.Sampled | TextureUsage.Staging);

        var texture = Texture.Create(ref description);
        var stagingTexture = Texture.Create(ref stagingDescription);

        SamplerCreateInfo samplerCreateInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            AnisotropyEnable = Vk.True,
            BorderColor = BorderColor.FloatTransparentBlack,
            MaxAnisotropy = 4,
            AddressModeU = SamplerAddressMode.ClampToBorder,
            AddressModeV = SamplerAddressMode.ClampToBorder,
            AddressModeW = SamplerAddressMode.ClampToBorder,
            MipmapMode = SamplerMipmapMode.Linear,
            CompareOp = CompareOp.Never,
            CompareEnable = Vk.False,
            MinLod = 0,
            MaxLod = 1,
            MinFilter = Filter.Linear,
            MagFilter = Filter.Linear
        };

        VulkanUtils.Assert(VulkanEngine.Vk.CreateSampler(VulkanEngine.Device, in samplerCreateInfo,
            VulkanEngine.AllocationCallback, out var sampler));

        ImageViewCreateInfo imageViewCreateInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Format = texture.Format,
            Image = texture.Image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                LayerCount = 1,
                LevelCount = 1,
                BaseArrayLayer = 0,
                BaseMipLevel = 0
            },
            ViewType = ImageViewType.Type2D
        };

        VulkanUtils.Assert(VulkanEngine.Vk.CreateImageView(VulkanEngine.Device, in imageViewCreateInfo,
            VulkanEngine.AllocationCallback, out var imageView));

        var descriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.UiFontTexture);

        DescriptorImageInfo descriptorImageInfo = new()
        {
            Sampler = sampler,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = imageView
        };

        WriteDescriptorSet writeDescriptorSet = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DstSet = descriptorSet,
            PImageInfo = &descriptorImageInfo,
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, 1, &writeDescriptorSet, 0, null);

        var textureWrapper = new FontTextureWrapper()
        {
            Texture = texture,
            StagingTexture = stagingTexture,
            Sampler = sampler,
            ImageView = imageView,
            SampledImageDescriptorSet = descriptorSet
        };
        _managedTextures.Add(textureWrapper);
        
        return textureWrapper;
    }

    public Point GetTextureSize(object texture)
    {
        Logger.AssertAndThrow(texture is FontTextureWrapper, $"Texture is not of type {nameof(FontTextureWrapper)}", "UI");
        var tex = (FontTextureWrapper)texture;

        return new Point((int)tex.Texture.Width, (int)tex.Texture.Height);
    }

    public unsafe void SetTextureData(object texture, Rectangle bounds, byte[] data)
    {
        Logger.AssertAndThrow(texture is FontTextureWrapper, $"Texture is not of type {nameof(FontTextureWrapper)}", "UI");

        if (data.Any(x => x != 0))
        {
            
        }
        else
        {
            Console.WriteLine("No texture data to set");
        }

        var tex = ((FontTextureWrapper)texture).StagingTexture;
        var layout = tex.GetSubresourceLayout(0);

        var dataSpan = data.AsSpan();
        var texSpan = new Span<byte>((void*)(MemoryManager.Map(tex.MemoryBlock).ToInt64() + (long)layout.Offset),
            (int)layout.Size);

        for (var y = 0; y < bounds.Height; y++)
        {
            var sourceSpan = dataSpan.Slice(y * bounds.Width * 4, bounds.Width * 4);
            var destinationSpan = texSpan.Slice((int)layout.RowPitch * (bounds.Y + y) + bounds.X * 4, bounds.Width * 4);
            sourceSpan.CopyTo(destinationSpan);
        }

        ((FontTextureWrapper)texture).StagingTexture = tex;
        ((FontTextureWrapper)texture).Changed = true;
    }
}