using MintyCore.Graphics;
using MintyCore.Graphics.Managers.Implementations;
using MintyCore.Graphics.Utils;
using MintyCore.Registries;
using Serilog;
using Silk.NET.Vulkan;
using DescriptorSetIDs = MintyCore.Identifications.DescriptorSetIDs;
using ShaderIDs = Techardry.Identifications.ShaderIDs;

namespace Techardry.Voxels;

public static class RenderObjects
{
    [RegisterShader("voxel_frag", "voxels/vox_render_frag.spv")]
    public static ShaderInfo VoxelFrag => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("voxel_vert", "voxels/vox_render_vert.spv")]
    public static ShaderInfo VoxelVert => new(ShaderStageFlags.VertexBit);

    [RegisterDescriptorSet("camera_data")]
    public static DescriptorSetInfo CameraData => new()
    {
        Bindings =
        [
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                StageFlags = ShaderStageFlags.FragmentBit
            }
        ],
        DescriptorSetsPerPool = 16
    };

    [RegisterGraphicsPipeline("voxel")]
    public static GraphicsPipelineDescription VoxelPipeline(IVulkanEngine vulkanEngine) =>
        new()
        {
            Flags = 0,
            Scissors =
            [
                new Rect2D
                {
                    Extent = vulkanEngine.SwapchainExtent,
                    Offset = new Offset2D(0, 0)
                }
            ],
            Shaders = [ShaderIDs.VoxelFrag, ShaderIDs.VoxelVert],
            Topology = PrimitiveTopology.TriangleList,
            Viewports =
            [
                new Viewport
                {
                    Width = vulkanEngine.SwapchainExtent.Width,
                    Height = vulkanEngine.SwapchainExtent.Height,
                    MaxDepth = 1.0f
                }
            ],
            DescriptorSets =
            [
                Identifications.DescriptorSetIDs.CameraData,
                DescriptorSetIDs.SampledTexture,
                Identifications.DescriptorSetIDs.Render
            ],
            DynamicStates = [DynamicState.Scissor, DynamicState.Viewport],
            RasterizationInfo =
            {
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.Clockwise,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1
            },
            RenderDescription = new DynamicRenderingDescription
            {
                ColorAttachmentFormats = [vulkanEngine.SwapchainImageFormat]
            },
            SampleCount = SampleCountFlags.Count1Bit,
            SubPass = 1,
            BasePipelineHandle = default,
            BasePipelineIndex = 0,
            ColorBlendInfo =
            {
                Attachments =
                [
                    new PipelineColorBlendAttachmentState
                    {
                        BlendEnable = Vk.True,
                        AlphaBlendOp = BlendOp.Add,
                        ColorBlendOp = BlendOp.Add,
                        ColorWriteMask = ColorComponentFlags.ABit | ColorComponentFlags.RBit |
                                         ColorComponentFlags.GBit | ColorComponentFlags.BBit,
                        SrcColorBlendFactor = BlendFactor.One,
                        SrcAlphaBlendFactor = BlendFactor.SrcAlpha,
                        DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                        DstColorBlendFactor = BlendFactor.Zero
                    }
                ]
            },
            DepthStencilInfo = default,
            VertexAttributeDescriptions = Array.Empty<VertexInputAttributeDescription>(),
            VertexInputBindingDescriptions = Array.Empty<VertexInputBindingDescription>(),
            PushConstantRanges =
            [
                new PushConstantRange()
                {
                    StageFlags = ShaderStageFlags.FragmentBit,
                    Offset = 0,
                    Size = sizeof(uint)
                }
            ]
        };
    
    
    [RegisterDescriptorSet("render")]
    public static DescriptorSetInfo RenderDescriptorSet => new()
    {
        Bindings =
        [
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.FragmentBit
            },
            new DescriptorSetLayoutBinding()
            {
                Binding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.FragmentBit
            }
        ],
        DescriptorSetsPerPool = 32
    };
}