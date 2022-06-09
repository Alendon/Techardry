using MintyCore.Identifications;
using MintyCore.Registries;
using MintyCore.Render;
using Silk.NET.Vulkan;
using DescriptorSetIDs = MintyCore.Identifications.DescriptorSetIDs;
using ShaderIDs = Techardry.Identifications.ShaderIDs;

namespace Techardry.Voxels;

public static class RenderObjects
{
    [RegisterShader("voxel_frag","voxels/vox_render_frag.spv")]
    public static ShaderInfo VoxelFrag => new (ShaderStageFlags.ShaderStageFragmentBit);
    
    [RegisterShader("voxel_vert","voxels/vox_render_vert.spv")]
    public static ShaderInfo VoxelVert => new (ShaderStageFlags.ShaderStageVertexBit);
    
    [RegisterDescriptorSet("voxel_octree")]
    public static DescriptorSetInfo VoxelOctreeDescriptor => new()
    {
        Bindings = new[]
        {
            new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            },
            new DescriptorSetLayoutBinding
            {
                Binding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                StageFlags = ShaderStageFlags.ShaderStageFragmentBit
            }
        }
    };

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
                DescriptorSets = new []{Identifications.DescriptorSetIDs.VoxelOctree },
                DynamicStates = new [] {DynamicState.Scissor , DynamicState.Viewport},
                RasterizationInfo =
                {
                    CullMode =  CullModeFlags.CullModeNone,
                    FrontFace = FrontFace.Clockwise,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1
                },
                RenderPass = RenderPassIDs.Main,
                SampleCount = SampleCountFlags.SampleCount1Bit,
                SubPass = 0,
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
                VertexAttributeDescriptions = Vertex.GetVertexAttributes(),
                VertexInputBindingDescriptions = new[] {Vertex.GetVertexBinding()}
            };
        }
    }
}