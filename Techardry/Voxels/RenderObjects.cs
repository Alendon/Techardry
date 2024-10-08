﻿using MintyCore.Graphics;
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
    [RegisterShader("voxel_frag", "vox_render_frag.spv")]
    public static ShaderInfo VoxelFrag => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("voxel_beam_frag", "vox_beam_frag.spv")]
    public static ShaderInfo VoxelBeamFrag => new(ShaderStageFlags.FragmentBit);

    [RegisterShader("voxel_vert", "vox_render_vert.spv")]
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
                //the camera
                Identifications.DescriptorSetIDs.CameraData,

                //the texture atlas
                DescriptorSetIDs.SampledTexture,

                //world data - bvh
                Identifications.DescriptorSetIDs.Render,

                //beam data
                DescriptorSetIDs.SampledRenderTexture
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
            VertexAttributeDescriptions = [],
            VertexInputBindingDescriptions = [],
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


    [RegisterGraphicsPipeline("voxel_beam")]
    public static GraphicsPipelineDescription VoxelBeamPipeline(IVulkanEngine vulkanEngine) =>
        new()
        {
            Flags = 0,
            Scissors = [new Rect2D()],
            Shaders = [ShaderIDs.VoxelBeamFrag, ShaderIDs.VoxelVert],
            Topology = PrimitiveTopology.TriangleList,
            Viewports = [new Viewport()],
            DescriptorSets =
            [
                Identifications.DescriptorSetIDs.CameraData,
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
                ColorAttachmentFormats = [Format.R32Sfloat]
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
                        ColorWriteMask = ColorComponentFlags.RBit,
                        SrcColorBlendFactor = BlendFactor.One,
                        SrcAlphaBlendFactor = BlendFactor.SrcAlpha,
                        DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                        DstColorBlendFactor = BlendFactor.Zero
                    }
                ]
            },
            DepthStencilInfo = default,
            VertexAttributeDescriptions = [],
            VertexInputBindingDescriptions = [],
            PushConstantRanges = []
        };


    [RegisterDescriptorSet("render")]
    public static DescriptorSetInfo RenderDescriptorSet => new()
    {
        Bindings =
        [
            //world grid header
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                StageFlags = ShaderStageFlags.FragmentBit
            },
            //world grid
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