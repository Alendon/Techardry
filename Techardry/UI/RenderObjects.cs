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
        VertexAttributeDescriptions = new VertexInputAttributeDescription[]
        {
            //location rectangle
            new()
            {
                Binding = 0U,
                Format = Format.R32G32B32A32Sfloat,
                Location = 0U,
                Offset = 0
            },
            //uv rectangle
            new()
            {
                Binding = 0U,
                Format = Format.R32G32B32A32Sfloat,
                Location = 1U,
                Offset = sizeof(float) * 4
            },
        },
        VertexInputBindingDescriptions = new VertexInputBindingDescription[]
        {
            new()
            {
                Binding = 0,
                Stride = sizeof(float) * 4 * 2,
                InputRate = VertexInputRate.Instance
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
        VertexAttributeDescriptions = new VertexInputAttributeDescription[]
        {
            //location rectangle
            new()
            {
                Binding = 0U,
                Format = Format.R32G32B32A32Sfloat,
                Location = 0U,
                Offset = 0
            },
            //color
            new()
            {
                Binding = 0U,
                Format = Format.R32G32B32A32Sfloat,
                Location = 1U,
                Offset = sizeof(float) * 4
            },
        },
        VertexInputBindingDescriptions = new VertexInputBindingDescription[]
        {
            new()
            {
                Binding = 0,
                Stride = sizeof(float) * 4 * 2,
                InputRate = VertexInputRate.Instance
            }
        }
    };
}