using System.Numerics;
using FontStashSharp.Interfaces;
using MintyCore.Identifications;
using MintyCore.Render;
using Silk.NET.Vulkan;
using Techardry.UI;

namespace Techardry.Render;

/// <summary>
///     The main UI renderer
/// </summary>
public unsafe class UiRenderer : IFontStashRenderer2
{
    private Element? _rootElement;
    private Sampler _sampler;
    private Mesh _mesh;

    private Texture[] _presentTextures = Array.Empty<Texture>();
    private ImageView[] _imageViews = Array.Empty<ImageView>();
    private DescriptorSet[] _descriptorSets = Array.Empty<DescriptorSet>();

    private Extent2D Size => VulkanEngine.SwapchainExtent;
    private uint FrameIndex => VulkanEngine.ImageIndex;

    internal void SetupMainUiRendering()
    {
        CreateSampler();
        CreateMesh();
        CreateInitialTextures();
        CreateInitialImageViews();
        CreateInitialDescriptorSets();
    }

    /// <summary>
    ///     Set the main ui element
    /// </summary>
    /// <param name="mainUiElement"></param>
    public void SetMainUiContext(Element? mainUiElement)
    {
        _rootElement = mainUiElement;
    }

    public void DrawMainUi(Material uiMaterial)
    {
        if (_rootElement is null) return;
        VulkanEngine.SetActiveRenderPass(RenderPassHandler.GetRenderPass(RenderPassIDs.Main),
            SubpassContents.SecondaryCommandBuffers);
        CheckSize();
        DrawToTexture();
        DrawToScreen(uiMaterial);
    }

    private void DrawToScreen(Material uiMaterial)
    {
        var cb = VulkanEngine.GetSecondaryCommandBuffer();
        uiMaterial.Bind(cb);
        VulkanEngine.Vk.CmdBindDescriptorSets(cb, PipelineBindPoint.Graphics, uiMaterial.PipelineLayout, 0, 1,
            _descriptorSets[FrameIndex], 0, null);

        VulkanEngine.Vk.CmdBindVertexBuffers(cb, 0, 1, _mesh.MemoryBuffer.Buffer, 0);
        VulkanEngine.Vk.CmdDraw(cb, _mesh.VertexCount, 1, 0, 0);
        VulkanEngine.ExecuteSecondary(cb);
    }

    private void DrawToTexture()
    {
        if (_rootElement?.Image is null) return;
        var image = _rootElement.Image;
        TextureHandler.CopyImageToTexture(new[] { image }.AsSpan(), _presentTextures[FrameIndex], true);
    }

    private void CheckSize()
    {
        if (_rootElement is null) return;

        ref var texture = ref _presentTextures[FrameIndex];
        if (texture.Width == (int)_rootElement.PixelSize.Width &&
            texture.Height == (int)_rootElement.PixelSize.Height) return;

        ref var imageView = ref _imageViews[FrameIndex];
        ref var descriptorSet = ref _descriptorSets[FrameIndex];

        VulkanEngine.Vk.DestroyImageView(VulkanEngine.Device, imageView, VulkanEngine.AllocationCallback);
        DescriptorSetHandler.FreeDescriptorSet(descriptorSet);

        texture.Dispose();
        var description = TextureDescription.Texture2D((uint)_rootElement.PixelSize.Width,
            (uint)_rootElement.PixelSize.Height, 1, 1, Format.R8G8B8A8Unorm, TextureUsage.Sampled);
        texture = Texture.Create(ref description);

        ImageViewCreateInfo imageViewCreateInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = texture.Image,
            Format = Format.R8G8B8A8Unorm,
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
        VulkanUtils.Assert(VulkanEngine.Vk.CreateImageView(VulkanEngine.Device, imageViewCreateInfo,
            VulkanEngine.AllocationCallback, out imageView));

        descriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.SampledTexture);
        DescriptorImageInfo imageInfo = new()
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            Sampler = _sampler,
            ImageView = imageView
        };

        WriteDescriptorSet write = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DstBinding = 0,
            PImageInfo = &imageInfo,
            DstSet = descriptorSet
        };
        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, 1, write, 0, null);
    }

    private void CreateInitialDescriptorSets()
    {
        _descriptorSets = new DescriptorSet[_presentTextures.Length];
        for (var i = 0; i < _presentTextures.Length; i++)
        {
            _descriptorSets[i] = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.SampledTexture);

            DescriptorImageInfo imageInfo = new()
            {
                Sampler = _sampler,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = _imageViews[i]
            };

            WriteDescriptorSet write = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DstBinding = 0,
                PImageInfo = &imageInfo,
                DstSet = _descriptorSets[i]
            };
            VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, 1, write, 0, null);
        }
    }

    private void CreateInitialImageViews()
    {
        _imageViews = new ImageView[_presentTextures.Length];
        for (var i = 0; i < _imageViews.Length; i++)
        {
            var image = _presentTextures[i];
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image.Image,
                Format = Format.R8G8B8A8Unorm,
                ViewType = ImageViewType.Type2D,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    LayerCount = 1,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    BaseMipLevel = 0
                }
            };
            VulkanUtils.Assert(VulkanEngine.Vk.CreateImageView(VulkanEngine.Device, createInfo,
                VulkanEngine.AllocationCallback,
                out _imageViews[i]));
        }
    }

    private void CreateInitialTextures()
    {
        var description = TextureDescription.Texture2D(Size.Width, Size.Height, 1, 1,
            Format.R8G8B8A8Unorm, TextureUsage.Sampled);
        _presentTextures = new Texture[VulkanEngine.SwapchainImageCount];
        for (var i = 0; i < _presentTextures.Length; i++) _presentTextures[i] = Texture.Create(ref description);
    }

    private void CreateMesh()
    {
        //Create a mesh which spans over the screen
        Span<Vertex> vertices = stackalloc Vertex[]
        {
            new(new Vector3(-1, 1, 0), Vector3.Zero, Vector3.Zero, new Vector2(0, 0)),
            new(new Vector3(-1, -1, 0), Vector3.Zero, Vector3.Zero, new Vector2(0, 1)),
            new(new Vector3(1, -1, 0), Vector3.Zero, Vector3.Zero, new Vector2(1, 1)),

            new(new Vector3(-1, 1, 0), Vector3.Zero, Vector3.Zero, new Vector2(0, 0)),
            new(new Vector3(1, -1, 0), Vector3.Zero, Vector3.Zero, new Vector2(1, 1)),
            new(new Vector3(1, 1, 0), Vector3.Zero, Vector3.Zero, new Vector2(1, 0))
        };
        _mesh = MeshHandler.CreateDynamicMesh(vertices, (uint)vertices.Length);
    }

    private void CreateSampler()
    {
        //Create a sampler for the texture
        SamplerCreateInfo createInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            AnisotropyEnable = Vk.True,
            MaxAnisotropy = 4,
            BorderColor = BorderColor.FloatTransparentBlack,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MaxLod = 1,
            MipmapMode = SamplerMipmapMode.Linear,
            MinFilter = Filter.Linear,
            MagFilter = Filter.Linear,
            CompareEnable = Vk.True,
            CompareOp = CompareOp.Never
        };

        VulkanUtils.Assert(VulkanEngine.Vk.CreateSampler(VulkanEngine.Device, createInfo,
            VulkanEngine.AllocationCallback, out _sampler));
    }

    internal void DestroyMainUiRendering()
    {
        _mesh.Dispose();


        foreach (var imageView in _imageViews)
            VulkanEngine.Vk.DestroyImageView(VulkanEngine.Device, imageView, VulkanEngine.AllocationCallback);

        foreach (var uiDescriptorSet in _descriptorSets) DescriptorSetHandler.FreeDescriptorSet(uiDescriptorSet);

        VulkanEngine.Vk.DestroySampler(VulkanEngine.Device, _sampler, VulkanEngine.AllocationCallback);

        foreach (var texture in _presentTextures) texture.Dispose();
    }

    public void DrawQuad(object texture, ref VertexPositionColorTexture topLeft, ref VertexPositionColorTexture topRight,
        ref VertexPositionColorTexture bottomLeft, ref VertexPositionColorTexture bottomRight)
    {
        throw new NotImplementedException();
    }

    public ITexture2DManager? TextureManager { get; }
}