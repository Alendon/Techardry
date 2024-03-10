using System.Diagnostics.CodeAnalysis;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Utils;
using Silk.NET.Vulkan;

namespace Techardry.Render;


public interface ITextureAtlasHandler
{
    unsafe void CreateTextureAtlas(Identification atlasId, Identification[] textureIds);
    bool TryGetAtlasTexture(Identification id, [MaybeNullWhen(false)] out Texture texture);

    bool TryGetAtlasLocation(Identification atlasId, Identification subTextureId,
        out AtlasLocationInfo locationInfo);

    bool TryGetAtlasDescriptorSet(Identification atlasId, out DescriptorSet descriptorSet);
    bool TryGetAtlasView(Identification atlasId, out ImageView imageView);
    bool TryGetAtlasSampler(Identification atlasId, out Sampler sampler);
    unsafe void RemoveTextureAtlas(Identification id);
    void Clear();
}