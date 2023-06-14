using System.Numerics;
using MintyCore.Registries;
using MintyCore.Render;
using Silk.NET.Vulkan;
using Techardry.Identifications;

namespace Techardry.Render;

public class GraphicPipelines
{
    [RegisterGraphicsPipeline("color")]
    internal static unsafe GraphicsPipelineDescription ColorDescription
    {
        get
        {
            Rect2D scissor = new()
            {
                Extent = VulkanEngine.SwapchainExtent,
                Offset = new Offset2D(0, 0)
            };
            Viewport viewport = new()
            {
                Width = VulkanEngine.SwapchainExtent.Width,
                Height = VulkanEngine.SwapchainExtent.Height,
                MaxDepth = 1f,
                MinDepth = 0f
            };

            var vertexInputBindings = new[]
            {
                Vertex.GetVertexBinding(),
                new VertexInputBindingDescription
                {
                    Binding = 1,
                    Stride = (uint) sizeof(Matrix4x4),
                    InputRate = VertexInputRate.Instance
                }
            };

            var attributes = Vertex.GetVertexAttributes();
            var vertexInputAttributes =
                new VertexInputAttributeDescription[attributes.Length + 4];
            for (var i = 0; i < attributes.Length; i++) vertexInputAttributes[i] = attributes[i];

            vertexInputAttributes[attributes.Length] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint) attributes.Length,
                Offset = 0
            };
            vertexInputAttributes[attributes.Length + 1] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint) attributes.Length + 1,
                Offset = (uint) sizeof(Vector4)
            };
            vertexInputAttributes[attributes.Length + 2] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint) attributes.Length + 2,
                Offset = (uint) sizeof(Vector4) * 2
            };
            vertexInputAttributes[attributes.Length + 3] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint) attributes.Length + 3,
                Offset = (uint) sizeof(Vector4) * 3
            };

            var colorBlendAttachment = new[]
            {
                new PipelineColorBlendAttachmentState
                {
                    BlendEnable = Vk.True,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add,
                    ColorWriteMask = ColorComponentFlags.RBit |
                                     ColorComponentFlags.GBit |
                                     ColorComponentFlags.BBit | ColorComponentFlags.ABit
                }
            };

            var dynamicStates = new[]
            {
                DynamicState.Viewport,
                DynamicState.Scissor
            };

            GraphicsPipelineDescription pipelineDescription = new()
            {
                Shaders = new[]
                {
                    ShaderIDs.TriangleVert,
                    ShaderIDs.ColorFrag
                },
                Scissors = new[] {scissor},
                Viewports = new[] {viewport},
                DescriptorSets = new[] {DescriptorSetIDs.CameraBuffer},
                Flags = 0,
                Topology = PrimitiveTopology.TriangleList,
                DynamicStates = dynamicStates,
                RenderPass = MintyCore.Identifications.RenderPassIDs.Main,
                SampleCount = SampleCountFlags.Count1Bit,
                SubPass = 0,
                BasePipelineHandle = default,
                BasePipelineIndex = 0,
                PrimitiveRestartEnable = false,
                AlphaToCoverageEnable = false,
                VertexAttributeDescriptions = vertexInputAttributes,
                VertexInputBindingDescriptions = vertexInputBindings,
                RasterizationInfo =
                {
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.Clockwise,
                    RasterizerDiscardEnable = false,
                    LineWidth = 1,
                    PolygonMode = PolygonMode.Fill,
                    DepthBiasEnable = false,
                    DepthClampEnable = false
                },
                ColorBlendInfo =
                {
                    LogicOpEnable = false,
                    Attachments = colorBlendAttachment
                },
                DepthStencilInfo =
                {
                    DepthTestEnable = true,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.LessOrEqual,
                    MinDepthBounds = 0,
                    MaxDepthBounds = 100,
                    StencilTestEnable = false,
                    DepthBoundsTestEnable = false
                },
                PushConstantRanges = Array.Empty<PushConstantRange>()
            };
            return pipelineDescription;
        }
    }
    
    [RegisterGraphicsPipeline("texture")]
    internal static unsafe GraphicsPipelineDescription TextureDescription
    {
        get
        {
            Rect2D scissor = new()
            {
                Extent = VulkanEngine.SwapchainExtent,
                Offset = new Offset2D(0, 0)
            };
            Viewport viewport = new()
            {
                Width = VulkanEngine.SwapchainExtent.Width,
                Height = VulkanEngine.SwapchainExtent.Height,
                MaxDepth = 1f,
                MinDepth = 0f
            };

            var vertexInputBindings = new[]
            {
                Vertex.GetVertexBinding(),
                new VertexInputBindingDescription
                {
                    Binding = 1,
                    Stride = (uint)sizeof(Matrix4x4),
                    InputRate = VertexInputRate.Instance
                }
            };

            var attributes = Vertex.GetVertexAttributes();
            var vertexInputAttributes =
                new VertexInputAttributeDescription[attributes.Length + 4];
            for (var i = 0; i < attributes.Length; i++) vertexInputAttributes[i] = attributes[i];

            vertexInputAttributes[attributes.Length] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length,
                Offset = 0
            };
            vertexInputAttributes[attributes.Length + 1] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length + 1,
                Offset = (uint)sizeof(Vector4)
            };
            vertexInputAttributes[attributes.Length + 2] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length + 2,
                Offset = (uint)sizeof(Vector4) * 2
            };
            vertexInputAttributes[attributes.Length + 3] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length + 3,
                Offset = (uint)sizeof(Vector4) * 3
            };

            var colorBlendAttachment = new[]
            {
                new PipelineColorBlendAttachmentState
                {
                    BlendEnable = Vk.True,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add,
                    ColorWriteMask = ColorComponentFlags.RBit |
                                     ColorComponentFlags.GBit |
                                     ColorComponentFlags.BBit | ColorComponentFlags.ABit
                }
            };

            var dynamicStates = new[]
            {
                DynamicState.Viewport,
                DynamicState.Scissor
            };

            GraphicsPipelineDescription pipelineDescription = new()
            {
                Shaders = new[]
                {
                    ShaderIDs.TriangleVert,
                    ShaderIDs.TextureFrag
                },
                Scissors = new[] { scissor },
                Viewports = new[] { viewport },
                DescriptorSets = new[] { DescriptorSetIDs.CameraBuffer, MintyCore.Identifications.DescriptorSetIDs.SampledTexture },
                Flags = 0,
                Topology = PrimitiveTopology.TriangleList,
                DynamicStates = dynamicStates,
                RenderPass = default,
                SampleCount = SampleCountFlags.Count1Bit,
                SubPass = 0,
                BasePipelineHandle = default,
                BasePipelineIndex = 0,
                PrimitiveRestartEnable = false,
                AlphaToCoverageEnable = false,
                VertexAttributeDescriptions = vertexInputAttributes,
                VertexInputBindingDescriptions = vertexInputBindings,
                RasterizationInfo =
                {
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.Clockwise,
                    RasterizerDiscardEnable = false,
                    LineWidth = 1,
                    PolygonMode = PolygonMode.Fill,
                    DepthBiasEnable = false,
                    DepthClampEnable = false
                },
                ColorBlendInfo =
                {
                    LogicOpEnable = false,
                    Attachments = colorBlendAttachment
                },
                DepthStencilInfo =
                {
                    DepthTestEnable = true,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.LessOrEqual,
                    MinDepthBounds = 0,
                    MaxDepthBounds = 1,
                    StencilTestEnable = false,
                    DepthBoundsTestEnable = false
                },
                PushConstantRanges = Array.Empty<PushConstantRange>()
            };
            return pipelineDescription;
        }
    }
    
    
    [RegisterGraphicsPipeline("ui_overlay")]
    internal static unsafe GraphicsPipelineDescription UiOverlayDescription
    {
        get
        {
            Rect2D scissor = new()
            {
                Extent = VulkanEngine.SwapchainExtent,
                Offset = new Offset2D(0, 0)
            };
            Viewport viewport = new()
            {
                Width = VulkanEngine.SwapchainExtent.Width,
                Height = VulkanEngine.SwapchainExtent.Height,
                MaxDepth = 1f,
                MinDepth = 0f
            };

            var vertexInputBindings = new[]
            {
                Vertex.GetVertexBinding(),
                new VertexInputBindingDescription
                {
                    Binding = 1,
                    Stride = (uint)sizeof(Matrix4x4),
                    InputRate = VertexInputRate.Instance
                }
            };

            var attributes = Vertex.GetVertexAttributes();
            var vertexInputAttributes =
                new VertexInputAttributeDescription[attributes.Length + 4];
            for (var i = 0; i < attributes.Length; i++) vertexInputAttributes[i] = attributes[i];

            vertexInputAttributes[attributes.Length] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length,
                Offset = 0
            };
            vertexInputAttributes[attributes.Length + 1] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length + 1,
                Offset = (uint)sizeof(Vector4)
            };
            vertexInputAttributes[attributes.Length + 2] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length + 2,
                Offset = (uint)sizeof(Vector4) * 2
            };
            vertexInputAttributes[attributes.Length + 3] = new VertexInputAttributeDescription
            {
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Location = (uint)attributes.Length + 3,
                Offset = (uint)sizeof(Vector4) * 3
            };

            var colorBlendAttachment = new[]
            {
                new PipelineColorBlendAttachmentState
                {
                    BlendEnable = Vk.True,
                    SrcColorBlendFactor = BlendFactor.SrcAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add,
                    ColorWriteMask = ColorComponentFlags.RBit |
                                     ColorComponentFlags.GBit |
                                     ColorComponentFlags.BBit | ColorComponentFlags.ABit
                }
            };

            var dynamicStates = new[]
            {
                DynamicState.Viewport,
                DynamicState.Scissor
            };

            GraphicsPipelineDescription pipelineDescription = new()
            {
                Shaders = new[]
                {
                    ShaderIDs.UiOverlayVert,
                    ShaderIDs.UiOverlayFrag
                },
                Scissors = new[] { scissor },
                Viewports = new[] { viewport },
                DescriptorSets = new[] { MintyCore.Identifications.DescriptorSetIDs.SampledTexture },
                Flags = 0,
                Topology = PrimitiveTopology.TriangleList,
                DynamicStates = dynamicStates,
                RenderPass = default,
                SampleCount = SampleCountFlags.Count1Bit,
                SubPass = 0,
                BasePipelineHandle = default,
                BasePipelineIndex = 0,
                PrimitiveRestartEnable = false,
                AlphaToCoverageEnable = false,
                VertexAttributeDescriptions = vertexInputAttributes,
                VertexInputBindingDescriptions = vertexInputBindings,
                RasterizationInfo =
                {
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.Clockwise,
                    RasterizerDiscardEnable = false,
                    LineWidth = 1,
                    PolygonMode = PolygonMode.Fill,
                    DepthBiasEnable = false,
                    DepthClampEnable = false
                },
                ColorBlendInfo =
                {
                    LogicOpEnable = false,
                    Attachments = colorBlendAttachment
                },
                DepthStencilInfo =
                {
                    DepthTestEnable = true,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.LessOrEqual,
                    MinDepthBounds = 0,
                    MaxDepthBounds = 100,
                    StencilTestEnable = false,
                    DepthBoundsTestEnable = false
                },
                PushConstantRanges = Array.Empty<PushConstantRange>()
            };
            var uiVertInput =
                new VertexInputAttributeDescription[attributes.Length];
            for (var i = 0; i < attributes.Length; i++) uiVertInput[i] = attributes[i];
            pipelineDescription.VertexAttributeDescriptions = uiVertInput;

            var uiVertBinding = new[] { Vertex.GetVertexBinding() };
            pipelineDescription.VertexInputBindingDescriptions = uiVertBinding;
            return pipelineDescription;
        }
    }
}