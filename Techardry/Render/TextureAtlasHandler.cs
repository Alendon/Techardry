using System.Numerics;
using JetBrains.Annotations;
using MintyCore.Identifications;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;

namespace Techardry.Render;

public static class TextureAtlasHandler
{
    private static readonly Dictionary<Identification, Texture> _atlasTextures = new();
    private static readonly Dictionary<Identification, ImageView> _atlasViews = new();
    private static readonly Dictionary<Identification, Sampler> _atlasSamplers = new();
    private static readonly Dictionary<Identification, DescriptorSet> _atlasDescriptorSets = new();

    private static readonly Dictionary<Identification, Dictionary<Identification, AtlasLocationInfo>> _atlasLocations =
        new();

    internal static unsafe void CreateTextureAtlas(Identification atlasId, Identification[] textureIds)
    {
        Logger.AssertAndThrow(textureIds.Length > 0, "Texture atlas must have at least one texture",
            "Techardry/TextureAtlas");

        (Identification id, Texture tex)[] textures =
            (from textureId in textureIds select (textureId, TextureHandler.GetTexture(textureId))).ToArray();

        textures.AsSpan().Sort((texture, texture1) =>
            -(texture.tex.Width * texture.tex.Height).CompareTo(texture1.tex.Width * texture1.tex.Height));

        var atlasDescription = new TextureDescription()
        {
            Depth = 1,
            Format = textures[0].tex.Format,
            Height = textures[0].tex.Height,
            Width = textures[0].tex.Width,
            Type = ImageType.ImageType2D,
            Usage = TextureUsage.Sampled,
            SampleCount = SampleCountFlags.SampleCount1Bit,
            ArrayLayers = (uint) textures.Length,
            MipLevels = 1
        };

        Dictionary<Identification, AtlasLocationInfo> textureLocationInfos = new();

        var atlas = Texture.Create(ref atlasDescription);

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();

        for (var index = 0; index < textures.Length; index++)
        {
            var texture = textures[index];

            var srcSubresourceLayers = new ImageSubresourceLayers()
            {
                AspectMask = ImageAspectFlags.ImageAspectColorBit,
                LayerCount = 1,
                MipLevel = 0,
                BaseArrayLayer = 0
            };

            var dstSubresourceLayers = new ImageSubresourceLayers()
            {
                AspectMask = ImageAspectFlags.ImageAspectColorBit,
                LayerCount = 1,
                MipLevel = 0,
                BaseArrayLayer = (uint) index
            };

            var copy = new ImageCopy()
            {
                Extent = new Extent3D(texture.tex.Width, texture.tex.Height, 1),
                SrcOffset = new Offset3D(0, 0, 0),
                DstOffset = new Offset3D(0, 0, 0),
                SrcSubresource = srcSubresourceLayers,
                DstSubresource = dstSubresourceLayers
            };

            var oldSrcLayout = texture.tex.GetImageLayout(0, 0);
            texture.tex.TransitionImageLayout(cb, 0, 1, 0, 1, ImageLayout.TransferSrcOptimal);

            var oldDstLayout = atlas.GetImageLayout(0, (uint) index);
            atlas.TransitionImageLayout(cb, 0, 1, (uint) index, 1, ImageLayout.TransferDstOptimal);


            VulkanEngine.Vk.CmdCopyImage(cb, texture.tex.Image, ImageLayout.TransferSrcOptimal, atlas.Image,
                ImageLayout.TransferDstOptimal, 1, copy);

            texture.tex.TransitionImageLayout(cb, 0, 1, 0, 1, oldSrcLayout);
            atlas.TransitionImageLayout(cb, 0, 1, (uint) index, 1, oldDstLayout);

            textureLocationInfos[texture.id] = new AtlasLocationInfo(Vector2.Zero,
                new Vector2((float) texture.tex.Width / atlas.Width, (float) texture.tex.Height / atlas.Height), index);
        }

        VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

        _atlasTextures[atlasId] = atlas;
        _atlasLocations[atlasId] = textureLocationInfos;

        ImageViewCreateInfo imageViewCreateInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Components =
            {
                A = ComponentSwizzle.A,
                B = ComponentSwizzle.B,
                G = ComponentSwizzle.G,
                R = ComponentSwizzle.R
            },
            Flags = 0,
            Format = atlas.Format,
            Image = atlas.Image,
            PNext = null,
            SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ImageAspectColorBit,
                LayerCount = atlas.ArrayLayers,
                BaseArrayLayer = 0,
                LevelCount = atlas.MipLevels,
                BaseMipLevel = 0
            },
            ViewType = ImageViewType.ImageViewType2DArray
        };

        VulkanEngine.Vk.CreateImageView(VulkanEngine.Device, imageViewCreateInfo, VulkanEngine.AllocationCallback,
            out var imageView);
        _atlasViews[atlasId] = imageView;

        SamplerCreateInfo samplerCreateInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            Flags = 0,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MipLodBias = 0,
            AnisotropyEnable = true,
            MaxAnisotropy = 16,
            CompareEnable = false,
            CompareOp = CompareOp.Never,
            MinLod = 0,
            MaxLod = 0,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false
        };

        VulkanEngine.Vk.CreateSampler(VulkanEngine.Device, samplerCreateInfo, VulkanEngine.AllocationCallback,
            out var sampler);

        _atlasSamplers[atlasId] = sampler;

        DescriptorSet atlasDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.SampledTexture);

        DescriptorImageInfo imageInfo = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = imageView,
            Sampler = sampler
        };

        WriteDescriptorSet writeSet = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DstBinding = 0,
            DstSet = atlasDescriptorSet,
            DstArrayElement = 0,
            PImageInfo = &imageInfo,
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, 1, &writeSet, 0, null);
        _atlasDescriptorSets[atlasId] = atlasDescriptorSet;
    }

    public static bool TryGetAtlasTexture(Identification id, out Texture texture)
    {
        return _atlasTextures.TryGetValue(id, out texture);
    }

    public static bool TryGetAtlasLocation(Identification atlasId, Identification subTextureId,
        out AtlasLocationInfo locationInfo)
    {
        locationInfo = default;
        return _atlasLocations.TryGetValue(atlasId, out var atlasLocations) &&
               atlasLocations.TryGetValue(subTextureId, out locationInfo);
    }
    
    public static bool TryGetAtlasDescriptorSet(Identification atlasId, out DescriptorSet descriptorSet)
    {
        return _atlasDescriptorSets.TryGetValue(atlasId, out descriptorSet);
    }
    
    public static bool TryGetAtlasView(Identification atlasId, out ImageView imageView)
    {
        return _atlasViews.TryGetValue(atlasId, out imageView);
    }
    
    public static bool TryGetAtlasSampler(Identification atlasId, out Sampler sampler)
    {
        return _atlasSamplers.TryGetValue(atlasId, out sampler);
    }

    internal static unsafe void RemoveTextureAtlas(Identification id)
    {
        if (_atlasDescriptorSets.Remove(id, out var descriptorSet))
        {
            DescriptorSetHandler.FreeDescriptorSet(descriptorSet);
        }
        
        if(_atlasSamplers.Remove(id, out var sampler))
        {
            VulkanEngine.Vk.DestroySampler(VulkanEngine.Device, sampler, VulkanEngine.AllocationCallback);
        }
        
        if(_atlasViews.Remove(id, out var imageView))
        {
            VulkanEngine.Vk.DestroyImageView(VulkanEngine.Device, imageView, VulkanEngine.AllocationCallback);
        }
        
        if(_atlasTextures.Remove(id, out var texture))
        {
            texture.Dispose();
        }

        _atlasLocations.Remove(id);
    }

    internal static void Clear()
    {
        var ids = _atlasTextures.Keys.ToArray();
        foreach (var id in ids)
        {
            RemoveTextureAtlas(id);
        }
    }
}

public record struct AtlasLocationInfo([UsedImplicitly] Vector2 Position, Vector2 Size, int ArrayIndex);