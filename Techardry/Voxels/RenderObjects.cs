using System.Numerics;
using MintyCore.Registries;
using MintyCore.Render;
using Silk.NET.Vulkan;
using DescriptorSetIDs = MintyCore.Identifications.DescriptorSetIDs;
using RenderPassIDs = Techardry.Identifications.RenderPassIDs;
using ShaderIDs = Techardry.Identifications.ShaderIDs;

namespace Techardry.Voxels;

public static class RenderObjects
{
    [RegisterShader("voxel_frag","voxels/vox_render_frag.spv")]
    public static ShaderInfo VoxelFrag => new (ShaderStageFlags.ShaderStageFragmentBit);
    
    [RegisterShader("voxel_vert","voxels/vox_render_vert.spv")]
    public static ShaderInfo VoxelVert => new (ShaderStageFlags.ShaderStageVertexBit);

    [RegisterDescriptorSet("master_octree")]
    public static DescriptorSetInfo MasterOctree => new()
    {
        Bindings = new []
        {
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            }
        }
    };
    
    [RegisterDescriptorSet("voxel_octree")]
    public static DescriptorSetInfo VoxelOctreeNodes => new()
    {
        Bindings = new[]
        {
            new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorCount = 100,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            }
        },
        BindingFlags = new[]
        {
            DescriptorBindingFlags.DescriptorBindingPartiallyBoundBit |
            DescriptorBindingFlags.DescriptorBindingVariableDescriptorCountBit
        },
        CreateFlags = DescriptorSetLayoutCreateFlags.DescriptorSetLayoutCreateUpdateAfterBindPoolBit
    };

    [RegisterDescriptorSet("camera_data")]
    public static DescriptorSetInfo CameraData => new()
    {
        Bindings = new[]
        {
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            }
        }
    };
    
    [RegisterDescriptorSet("input_attachment")]
    public static DescriptorSetInfo DepthInput => new()
    {
        Bindings = new[]
        {
            //Depth attachment
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.InputAttachment,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            },
            
            //Color attachment
            new DescriptorSetLayoutBinding()
            {
                Binding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.InputAttachment,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            }
        }
    };

    [RegisterRenderPass("dual_pipeline")]
    public static RenderPassInfo VoxelRenderPass => new (new[]
        {
            //Color
            new AttachmentDescription()
            {
                Flags = 0,
                Format = VulkanEngine.SwapchainImageFormat,
                Samples = SampleCountFlags.Count1Bit,
                InitialLayout = ImageLayout.PresentSrcKhr,
                FinalLayout = ImageLayout.PresentSrcKhr,
                LoadOp = AttachmentLoadOp.Load,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare
            },
            //Depth
            new AttachmentDescription()
            {
                Flags = 0,
                Format = Format.D32Sfloat,
                Samples = SampleCountFlags.Count1Bit,
                InitialLayout = ImageLayout.DepthStencilAttachmentOptimal,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
                LoadOp = AttachmentLoadOp.Load,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.Load,
                StencilStoreOp = AttachmentStoreOp.Store
            }
        },
        new[]
        {
            new SubpassDescriptionInfo
            {
                Flags = 0,
                ColorAttachments = new[]
                {
                    new AttachmentReference
                    {
                        Attachment = 0,
                        Layout = ImageLayout.ColorAttachmentOptimal
                    }
                },
                InputAttachments = Array.Empty<AttachmentReference>(),
                PreserveAttachments = Array.Empty<uint>(),
                PipelineBindPoint = PipelineBindPoint.Graphics,
                HasDepthStencilAttachment = true,
                DepthStencilAttachment =
                {
                    Attachment = 1,
                    Layout = ImageLayout.DepthStencilAttachmentOptimal
                }
            },
            new SubpassDescriptionInfo()
            {
                Flags = 0,
                ColorAttachments = new []
                {
                    new AttachmentReference()
                    {
                        Attachment = 0,
                        Layout = ImageLayout.General
                    },
                },
                InputAttachments = new []
                {
                    new AttachmentReference()
                    {
                        Attachment = 1u,
                        Layout = ImageLayout.General
                    },
                    new AttachmentReference()
                    {
                        Attachment = 0u,
                        Layout = ImageLayout.General
                    }
                },
                PreserveAttachments = Array.Empty<uint>(),
                PipelineBindPoint = PipelineBindPoint.Graphics,
                HasResolveAttachment = false,
                HasDepthStencilAttachment = true,
                DepthStencilAttachment =
                {
                    Attachment = 1,
                    Layout = ImageLayout.General
                }
            }
        },
        new[]
        {
            new SubpassDependency()
            {
                DependencyFlags = 0,
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcAccessMask = AccessFlags.NoneKhr,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.ColorAttachmentReadBit,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit
            },
            new SubpassDependency()
            {
                DependencyFlags = DependencyFlags.ByRegionBit,
                SrcSubpass = 0,
                DstSubpass = 1,
                SrcAccessMask = AccessFlags.NoneKhr,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.ColorAttachmentReadBit,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            }
        },
        0);

    [RegisterGraphicsPipeline("voxel")]
    public static GraphicsPipelineDescription VoxelPipeline
    {
        get
        {
            return new GraphicsPipelineDescription
            {
                Flags = 0,
                Scissors = new[] {new Rect2D
                {
                    Extent = VulkanEngine.SwapchainExtent,
                    Offset = new Offset2D(0, 0)
                }},
                Shaders = new[] {ShaderIDs.VoxelFrag, ShaderIDs.VoxelVert},
                Topology = PrimitiveTopology.TriangleList,
                Viewports = new[] {new Viewport
                {
                    Width = VulkanEngine.SwapchainExtent.Width,
                    Height = VulkanEngine.SwapchainExtent.Height,
                    MaxDepth = 1.0f
                }},
                DescriptorSets = new []{
                    Identifications.DescriptorSetIDs.CameraData,
                    DescriptorSetIDs.SampledTexture,
                    Identifications.DescriptorSetIDs.InputAttachment,
                    Identifications.DescriptorSetIDs.MasterOctree,
                    Identifications.DescriptorSetIDs.VoxelOctree,
                },
                DynamicStates = new [] {DynamicState.Scissor , DynamicState.Viewport},
                RasterizationInfo =
                {
                    CullMode =  CullModeFlags.CullModeNone,
                    FrontFace = FrontFace.Clockwise,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1
                },
                RenderPass = RenderPassIDs.DualPipeline,
                SampleCount = SampleCountFlags.SampleCount1Bit,
                SubPass = 1,
                BasePipelineHandle = default,
                BasePipelineIndex = 0,
                ColorBlendInfo =
                {
                    Attachments = new []
                    {
                        new PipelineColorBlendAttachmentState
                        {
                            BlendEnable = Vk.True,
                            AlphaBlendOp = BlendOp.Add,
                            ColorBlendOp = BlendOp.Add,
                            ColorWriteMask = ColorComponentFlags.ColorComponentABit | ColorComponentFlags.ColorComponentRBit | ColorComponentFlags.ColorComponentGBit | ColorComponentFlags.ColorComponentBBit,
                            SrcColorBlendFactor = BlendFactor.One,
                            SrcAlphaBlendFactor = BlendFactor.SrcAlpha,
                            DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                            DstColorBlendFactor = BlendFactor.Zero
                        }
                    }
                },
                DepthStencilInfo = default,
                VertexAttributeDescriptions = Array.Empty<VertexInputAttributeDescription>(),
                VertexInputBindingDescriptions = Array.Empty<VertexInputBindingDescription>()
            };
        }
    }
    
    [RegisterGraphicsPipeline("dual_texture")]
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
                    ColorWriteMask = ColorComponentFlags.ColorComponentRBit |
                                     ColorComponentFlags.ColorComponentGBit |
                                     ColorComponentFlags.ColorComponentBBit | ColorComponentFlags.ColorComponentABit
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
                Scissors = new[] {scissor},
                Viewports = new[] {viewport},
                DescriptorSets = new[]
                {
                    Identifications.DescriptorSetIDs.CameraBuffer, 
                    DescriptorSetIDs.SampledTexture
                },
                Flags = 0,
                Topology = PrimitiveTopology.TriangleList,
                DynamicStates = dynamicStates,
                RenderPass = RenderPassIDs.DualPipeline,
                SampleCount = SampleCountFlags.SampleCount1Bit,
                SubPass = 0,
                BasePipelineHandle = default,
                BasePipelineIndex = 0,
                PrimitiveRestartEnable = false,
                AlphaToCoverageEnable = false,
                VertexAttributeDescriptions = vertexInputAttributes,
                VertexInputBindingDescriptions = vertexInputBindings,
                RasterizationInfo =
                {
                    CullMode = CullModeFlags.CullModeNone,
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
                }
            };
            return pipelineDescription;
        }
    }
}