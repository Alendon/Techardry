using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using RenderPassIDs = MintyCore.Identifications.RenderPassIDs;

namespace Techardry.UI;

public static class RenderObjects
{
    [RegisterShader("ui_texture_fragment_shader", "ui/ui_texture_frag.spv")]
    public static ShaderInfo UiTextureFragmentShader => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("ui_texture_vertex_shader", "ui/ui_texture_vert.spv")]
    public static ShaderInfo UiTextureVertexShader => new(ShaderStageFlags.VertexBit);

    [RegisterShader("ui_color_fragment_shader", "ui/ui_color_frag.spv")]
    public static ShaderInfo UiColorFragmentShader => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("ui_color_vertex_shader", "ui/ui_color_vert.spv")]
    public static ShaderInfo UiColorVertexShader => new(ShaderStageFlags.VertexBit);
    
    [RegisterShader("ui_font_fragment_shader", "ui/ui_font_frag.spv")]
    public static ShaderInfo UiFontFragmentShader => new(ShaderStageFlags.FragmentBit);
    
    [RegisterShader("ui_font_vertex_shader", "ui/ui_font_vert.spv")]
    public static ShaderInfo UiFontVertexShader => new(ShaderStageFlags.VertexBit);

    [RegisterDescriptorSet("ui_font_texture")]
    public static DescriptorSetInfo UiFontTexture => new()
    {
        Bindings = new DescriptorSetLayoutBinding[]
        {
            new()
            {
                Binding = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit
            }
        },
        DescriptorSetsPerPool = 64
    };

    [RegisterGraphicsPipeline("ui_texture_pipeline")]
    public static GraphicsPipelineDescription UiTexturePipeline => new()
    {
        Flags = 0,
        Scissors = new[]
        {
            new Rect2D()
            {
                Offset = new Offset2D(0, 0),
                Extent = VulkanEngine.SwapchainExtent
            }
        },
        Shaders = new[]
        {
            ShaderIDs.UiTextureVertexShader,
            ShaderIDs.UiTextureFragmentShader
        },
        Topology = PrimitiveTopology.TriangleList,
        Viewports = new[]
        {
            new Viewport()
            {
                Height = VulkanEngine.SwapchainExtent.Height,
                Width = VulkanEngine.SwapchainExtent.Width,
                MaxDepth = 1f
            }
        },
        DescriptorSets = new[]
        {
            MintyCore.Identifications.DescriptorSetIDs.SampledTexture
        },
        DynamicStates = new[]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        },
        RasterizationInfo = new RasterizationInfo()
        {
            //TODO set the correct culling mode
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.Clockwise,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1f
        },
        RenderPass = RenderPassIDs.Main,
        SampleCount = SampleCountFlags.Count1Bit,
        SubPass = 0,
        ColorBlendInfo = new ColorBlendInfo()
        {
            Attachments = new[]
            {
                new PipelineColorBlendAttachmentState
                {
                    BlendEnable = Vk.True,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                                     ColorComponentFlags.ABit,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add
                }
            }
        },
        DepthStencilInfo = default,
        VertexAttributeDescriptions = Array.Empty<VertexInputAttributeDescription>(),
        VertexInputBindingDescriptions = Array.Empty<VertexInputBindingDescription>(),
        PushConstantRanges = new PushConstantRange[]
        {
            new()
            {
                Offset = 0,
                Size = sizeof(float) * 4 * 2,
                StageFlags = ShaderStageFlags.VertexBit
            }
        }
    };

    [RegisterGraphicsPipeline("ui_color_pipeline")]
    public static GraphicsPipelineDescription UiColorPipeline => new()
    {
        Flags = 0,
        Scissors = new[]
        {
            new Rect2D()
            {
                Offset = new Offset2D(0, 0),
                Extent = VulkanEngine.SwapchainExtent
            }
        },
        Shaders = new[]
        {
            ShaderIDs.UiColorVertexShader,
            ShaderIDs.UiColorFragmentShader
        },
        Topology = PrimitiveTopology.TriangleList,
        Viewports = new[]
        {
            new Viewport()
            {
                Height = VulkanEngine.SwapchainExtent.Height,
                Width = VulkanEngine.SwapchainExtent.Width,
                MaxDepth = 1f
            }
        },
        DescriptorSets = Array.Empty<Identification>(),
        DynamicStates = new[]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        },
        RasterizationInfo = new RasterizationInfo()
        {
            //TODO set the correct culling mode
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.Clockwise,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1f
        },
        RenderPass = RenderPassIDs.Main,
        SampleCount = SampleCountFlags.Count1Bit,
        SubPass = 0,
        ColorBlendInfo = new ColorBlendInfo()
        {
            Attachments = new[]
            {
                new PipelineColorBlendAttachmentState
                {
                    BlendEnable = Vk.True,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                                     ColorComponentFlags.ABit,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add
                }
            }
        },
        DepthStencilInfo = default,
        VertexAttributeDescriptions = Array.Empty<VertexInputAttributeDescription>(),
        VertexInputBindingDescriptions = Array.Empty<VertexInputBindingDescription>(),
        PushConstantRanges = new PushConstantRange[]
        {
            new()
            {
                Offset = 0,
                Size = sizeof(float) * 4 * 2,
                StageFlags = ShaderStageFlags.VertexBit
            }
        }
    };

    [RegisterGraphicsPipeline("ui_font_pipeline")]
    public static GraphicsPipelineDescription UiFontPipeline => new()
    {
        Flags = 0,
        Scissors = new[]
        {
            new Rect2D()
            {
                Offset = new Offset2D(0, 0),
                Extent = VulkanEngine.SwapchainExtent
            }
        },
        Shaders = new[]
        {
            ShaderIDs.UiFontVertexShader,
            ShaderIDs.UiFontFragmentShader
        },
        Topology = PrimitiveTopology.TriangleList,
        Viewports = new[]
        {
            new Viewport()
            {
                Height = VulkanEngine.SwapchainExtent.Height,
                Width = VulkanEngine.SwapchainExtent.Width,
                MaxDepth = 1f
            }
        },
        DescriptorSets = new[]
        {
            DescriptorSetIDs.UiFontTexture
        },
        DynamicStates = new[]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        },
        RasterizationInfo = new RasterizationInfo()
        {
            //TODO set the correct culling mode
            CullMode = CullModeFlags.None,
            FrontFace = FrontFace.Clockwise,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1f
        },
        RenderPass = RenderPassIDs.Main,
        SampleCount = SampleCountFlags.Count1Bit,
        SubPass = 0,
        ColorBlendInfo = new ColorBlendInfo()
        {
            Attachments = new[]
            {
                new PipelineColorBlendAttachmentState
                {
                    BlendEnable = Vk.True,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                                     ColorComponentFlags.ABit,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add
                }
            }
        },
        DepthStencilInfo = default,
        VertexAttributeDescriptions = Array.Empty<VertexInputAttributeDescription>(),
        VertexInputBindingDescriptions = Array.Empty<VertexInputBindingDescription>(),
        PushConstantRanges = new PushConstantRange[]
        {
            new()
            {
                Offset = 0,
                Size = sizeof(float) * 4 * 3,
                StageFlags = ShaderStageFlags.VertexBit
            }
        }
    };
}