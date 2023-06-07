using MintyCore.Registries;
using MintyCore.Render;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using RenderPassIDs = MintyCore.Identifications.RenderPassIDs;

namespace Techardry.UI;

public static class RenderObjects
{
    [RegisterShader("ui_texture_fragment_shader", "ui/ui_texture_frag.spv")]
    public static ShaderInfo UiFragmentShader => new(ShaderStageFlags.FragmentBit);
    
    [RegisterShader("ui_texture_vertex_shader", "ui/ui_texture_vert.spv")]
    public static ShaderInfo UiVertexShader => new(ShaderStageFlags.VertexBit);

    [RegisterDescriptorSet("ui_texture_descriptor")]
    public static DescriptorSetInfo UiTextureDescriptorSet => new()
    {
        DescriptorSetsPerPool = 64,
        CreateFlags = DescriptorSetLayoutCreateFlags.None,
        Bindings = new []
        {
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit
            },
            new DescriptorSetLayoutBinding()
            {
                Binding = 1,
                DescriptorType = DescriptorType.SampledImage,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit
            }
        }
    };

    [RegisterGraphicsPipeline("ui_texture_pipeline")]
    public static GraphicsPipelineDescription UiTexturePipeline => new()
    {
        Flags = 0,
        Scissors = new[]
        {
            new Rect2D()
            {
                Offset = new Offset2D(0,0),
                Extent = VulkanEngine.SwapchainExtent
            }
        },
        Shaders = new []
        {
            ShaderIDs.UiTextureVertexShader,
            ShaderIDs.UiTextureFragmentShader
        },
        Topology = PrimitiveTopology.TriangleList,
        Viewports = new []
        {
            new Viewport()
            {
                Height = VulkanEngine.SwapchainExtent.Height,
                Width =  VulkanEngine.SwapchainExtent.Width,
                MaxDepth = 1f
            }
        },
        DescriptorSets = new []
        {
            DescriptorSetIDs.UiTextureDescriptor
        },
        DynamicStates = new []
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
        SampleCount = SampleCountFlags.None,
        SubPass = 0,
        ColorBlendInfo = new ColorBlendInfo()
        {
            
        }

    };



}