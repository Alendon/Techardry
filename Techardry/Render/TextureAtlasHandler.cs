using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using JetBrains.Annotations;
using MintyCore.Graphics;
using MintyCore.Graphics.Managers;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Identifications;
using MintyCore.Utils;
using RectpackSharp;
using Serilog.Core;
using Silk.NET.Vulkan;

namespace Techardry.Render;

[Singleton<ITextureAtlasHandler>(/*SingletonContextFlags.NoHeadless*/)]
public class TextureAtlasHandler : ITextureAtlasHandler
{
    public required ITextureManager TextureManager { private get; [UsedImplicitly] set; }
    public required IDescriptorSetManager DescriptorSetHandler { private get; [UsedImplicitly] set; }
    public required IVulkanEngine VulkanEngine { private get; [UsedImplicitly] set; }
    
    private readonly Dictionary<Identification, Texture> _atlasTextures = new();
    private readonly Dictionary<Identification, ImageView> _atlasViews = new();
    private readonly Dictionary<Identification, Sampler> _atlasSamplers = new();
    private readonly Dictionary<Identification, DescriptorSet> _atlasDescriptorSets = new();

    private readonly Dictionary<Identification, Dictionary<Identification, AtlasLocationInfo>> _atlasLocations =
        new();

    public unsafe void CreateTextureAtlas(Identification atlasId, Identification[] textureIds)
    {
        if(textureIds.Length <= 0)
            throw new ArgumentException("Texture atlas must have at least one texture", nameof(textureIds));

        (Identification id, Texture tex)[] textures =
            (from textureId in textureIds select (textureId, TextureManager.GetTexture(textureId))).ToArray();
        
        var rectangles = new PackingRectangle[textures.Length];
        for (var i = 0; i < textures.Length; i++)
        {
            var (_, tex) = textures[i];

            rectangles[i] = new PackingRectangle()
            {
                Id = i,
                Height = tex.Height,
                Width = tex.Width
            };
        }

        RectanglePacker.Pack(rectangles, out var bounds, PackingHints.MostlySquared);
        

        var atlasDescription = new TextureDescription()
        {
            Depth = 1,
            Format = textures[0].tex.Format,
            Height = bounds.Height,
            Width = bounds.Width,
            Type = ImageType.Type2D,
            Usage = TextureUsage.Sampled,
            SampleCount = SampleCountFlags.Count1Bit,
            ArrayLayers = 1,
            MipLevels = 1
        };

        Dictionary<Identification, AtlasLocationInfo> textureLocationInfos = new();

        var atlas = TextureManager.Create(ref atlasDescription);

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();
        
        var oldDstLayout = atlas.GetImageLayout(0, 0);
        atlas.TransitionImageLayout(cb, 0, 1, 0, 1, ImageLayout.TransferDstOptimal);

        for (var index = 0u; index < rectangles.Length; index++)
        {
            var rectangle = rectangles[index];
            var (id, texture) = textures[rectangle.Id];

            var srcSubresourceLayers = new ImageSubresourceLayers()
            {
                AspectMask = ImageAspectFlags.ColorBit,
                LayerCount = 1,
                MipLevel = 0,
                BaseArrayLayer = 0
            };

            var dstSubresourceLayers = new ImageSubresourceLayers()
            {
                AspectMask = ImageAspectFlags.ColorBit,
                LayerCount = 1,
                MipLevel = 0,
                BaseArrayLayer = 0
            };

            var copy = new ImageCopy()
            {
                Extent = new Extent3D(texture.Width, texture.Height, texture.Depth),
                SrcOffset = new Offset3D(0, 0, 0),
                DstOffset = new Offset3D((int)rectangle.X, (int)rectangle.Y, 0),
                SrcSubresource = srcSubresourceLayers,
                DstSubresource = dstSubresourceLayers
            };

            var oldSrcLayout = texture.GetImageLayout(0, 0);
            texture.TransitionImageLayout(cb, 0, 1, 0, 1, ImageLayout.TransferSrcOptimal);

            VulkanEngine.Vk.CmdCopyImage(cb.InternalCommandBuffer, texture.Image, ImageLayout.TransferSrcOptimal, atlas.Image,
                ImageLayout.TransferDstOptimal, 1, copy);

            texture.TransitionImageLayout(cb, 0, 1, 0, 1, oldSrcLayout);

            textureLocationInfos[id] = new AtlasLocationInfo(new Vector2(rectangle.X / (float)bounds.Width, rectangle.Y/ (float)bounds.Height),
                new Vector2(rectangle.Width / (float)bounds.Width, rectangle.Height/ (float)bounds.Height));
        }
        atlas.TransitionImageLayout(cb, 0, 1, 0, 1, oldDstLayout);

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
                AspectMask = ImageAspectFlags.ColorBit,
                LayerCount = atlas.ArrayLayers,
                BaseArrayLayer = 0,
                LevelCount = atlas.MipLevels,
                BaseMipLevel = 0
            },
            ViewType = ImageViewType.Type2D
        };

        VulkanEngine.Vk.CreateImageView(VulkanEngine.Device, imageViewCreateInfo, null,
            out var imageView);
        _atlasViews[atlasId] = imageView;

        SamplerCreateInfo samplerCreateInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            Flags = 0,
            MagFilter = Filter.Nearest,
            MinFilter = Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MipLodBias = 0,
            AnisotropyEnable = false,
            MaxAnisotropy = 1,
            CompareEnable = false,
            CompareOp = CompareOp.Never,
            MinLod = 0,
            MaxLod = 0,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false
        };

        VulkanEngine.Vk.CreateSampler(VulkanEngine.Device, samplerCreateInfo, null,
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

    public bool TryGetAtlasTexture(Identification id, [MaybeNullWhen(false)] out Texture texture)
    {
        return _atlasTextures.TryGetValue(id, out texture);
    }

    public bool TryGetAtlasLocation(Identification atlasId, Identification subTextureId,
        out AtlasLocationInfo locationInfo)
    {
        locationInfo = default;
        return _atlasLocations.TryGetValue(atlasId, out var atlasLocations) &&
               atlasLocations.TryGetValue(subTextureId, out locationInfo);
    }
    
    public bool TryGetAtlasDescriptorSet(Identification atlasId, out DescriptorSet descriptorSet)
    {
        return _atlasDescriptorSets.TryGetValue(atlasId, out descriptorSet);
    }
    
    public bool TryGetAtlasView(Identification atlasId, out ImageView imageView)
    {
        return _atlasViews.TryGetValue(atlasId, out imageView);
    }
    
    public bool TryGetAtlasSampler(Identification atlasId, out Sampler sampler)
    {
        return _atlasSamplers.TryGetValue(atlasId, out sampler);
    }

    public unsafe void RemoveTextureAtlas(Identification id)
    {
        if (_atlasDescriptorSets.Remove(id, out var descriptorSet))
        {
            DescriptorSetHandler.FreeDescriptorSet(descriptorSet);
        }
        
        if(_atlasSamplers.Remove(id, out var sampler))
        {
            VulkanEngine.Vk.DestroySampler(VulkanEngine.Device, sampler, null);
        }
        
        if(_atlasViews.Remove(id, out var imageView))
        {
            VulkanEngine.Vk.DestroyImageView(VulkanEngine.Device, imageView, null);
        }
        
        if(_atlasTextures.Remove(id, out var texture))
        {
            texture.Dispose();
        }

        _atlasLocations.Remove(id);
    }

    public void Clear()
    {
        var ids = _atlasTextures.Keys.ToArray();
        foreach (var id in ids)
        {
            RemoveTextureAtlas(id);
        }
    }
}

public record struct AtlasLocationInfo([UsedImplicitly] Vector2 Position, Vector2 Size);