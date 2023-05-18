using System.Numerics;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Components.Client;
using Techardry.Identifications;
using Techardry.Render;
using Techardry.Utils;
using Techardry.World;
using DescriptorSetIDs = Techardry.Identifications.DescriptorSetIDs;
using PipelineIDs = Techardry.Identifications.PipelineIDs;
using SystemIDs = Techardry.Identifications.SystemIDs;

namespace Techardry.Systems.Client;

[RegisterSystem("voxel_render")]
[ExecuteInSystemGroup<DualRenderSystemGroup>]
[ExecuteAfter<RenderInstancedSystem>]
public sealed partial class VoxelRender : ARenderSystem
{
    [ComponentQuery] private readonly ComponentQuery<object, (Camera, Position)> _cameraQuery = new();

    private RenderData[] _renderData = Array.Empty<RenderData>();

    class RenderData
    {
        public MemoryBuffer CameraDataBuffer;
        public DescriptorSet CameraDataDescriptors;

        public ImageView LastColorImageView;
        public DescriptorSet InputAttachmentDescriptorSet;
    }


    private RenderData CurrentRenderData => _renderData[VulkanEngine.ImageIndex];

    private RenderResourcesWorker? _worker;

    public override void Setup(SystemManager systemManager)
    {
        if (World is not TechardryWorld techardryWorld)
        {
            Logger.WriteLog("VoxelRenderSystem can only be used in a TechardryWorld", LogImportance.Exception,
                "VoxelRender");
            return;
        }

        _cameraQuery.Setup(this);
        _worker = new RenderResourcesWorker(techardryWorld);
        _worker.Start();

        _renderData = new RenderData[VulkanEngine.SwapchainImageCount];
        for (var i = 0; i < VulkanEngine.SwapchainImageCount; i++)
        {
            _renderData[i] = new RenderData();
        }

        SetRenderArguments(new RenderPassArguments
        {
            SubpassIndex = 1
        });

        CreateCameraDataBuffers();
        CreateCameraDataDescriptors();
    }

    public override void PreExecuteMainThread()
    {
        var index = VulkanEngine.ImageIndex;
        if (VulkanEngine.SwapchainImageViews[index].Handle != _renderData[index].LastColorImageView.Handle)
        {
            if (_renderData[index].InputAttachmentDescriptorSet.Handle != 0)
                DescriptorSetHandler.FreeDescriptorSet(_renderData[index].InputAttachmentDescriptorSet);
            CreateInputAttachments(index);
        }

        base.PreExecuteMainThread();
    }

    protected override unsafe void Execute()
    {
        if (World is null) return;

        if ((KeyActions.RenderMode & 1) == 0) return;

        var cameraEntity = _cameraQuery.FirstOrDefault(entityWrapper =>
            World.EntityManager.GetEntityOwner(entityWrapper.Entity) == PlayerHandler.LocalPlayerGameId);

        if (cameraEntity.Entity.ArchetypeId == default) return;

        if (_worker is null || !_worker.TryGetRenderResources(out var renderDescriptorSet))
        {
            Logger.WriteLog("Failed to update voxel data", LogImportance.Error, "VoxelRender");
            return;
        }

        var vk = VulkanEngine.Vk;


        var data = MemoryManager.Map(CurrentRenderData.CameraDataBuffer.Memory);

        var cameraData = cameraEntity.GetCamera();
        var positionData = cameraEntity.GetPosition();
        var forward = cameraData.Forward;
        var up = cameraData.Upward;

        var cameraGpuData = (CameraData*)data;
        cameraGpuData->Forward = forward;
        cameraGpuData->Upward = up;
        cameraGpuData->AspectRatio = VulkanEngine.SwapchainExtent.Width / (float)VulkanEngine.SwapchainExtent.Height;
        cameraGpuData->HFov = cameraData.Fov;
        cameraGpuData->Position = positionData.Value;
        cameraGpuData->Near = cameraData.NearPlane;
        cameraGpuData->Far = cameraData.FarPlane;

        MemoryManager.UnMap(CurrentRenderData.CameraDataBuffer.Memory);

        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.Voxel);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.Voxel);

        vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline);

        Span<Viewport> viewports = stackalloc Viewport[]
        {
            new Viewport(0, 0, VulkanEngine.SwapchainExtent.Width, VulkanEngine.SwapchainExtent.Height, 0, 1)
        };
        vk.CmdSetViewport(CommandBuffer, 0, viewports);

        Span<Rect2D> scissors = stackalloc Rect2D[]
        {
            new Rect2D(new Offset2D(0, 0), VulkanEngine.SwapchainExtent)
        };
        vk.CmdSetScissor(CommandBuffer, 0, scissors);

        Logger.AssertAndThrow(
            TextureAtlasHandler.TryGetAtlasDescriptorSet(TextureAtlasIDs.BlockTexture,
                out var atlasDescriptorSet), "Failed to get atlas descriptor set", "Techardry/Render");


        Span<DescriptorSet> descriptorSets = stackalloc DescriptorSet[]
        {
            CurrentRenderData.CameraDataDescriptors,
            atlasDescriptorSet,
            CurrentRenderData.InputAttachmentDescriptorSet,
            renderDescriptorSet
        };


        vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0,
            (uint)descriptorSets.Length,
            descriptorSets, 0, (uint*)null);

        vk.CmdDraw(CommandBuffer, 6, 1, 0, 0);
    }


    private unsafe void CreateInputAttachments(uint index)
    {
        CurrentRenderData.InputAttachmentDescriptorSet =
            DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.InputAttachment);

        DescriptorImageInfo depthImageInfo = new()
        {
            ImageLayout = ImageLayout.General,
            ImageView = VulkanEngine.DepthImageView
        };

        DescriptorImageInfo colorImageInfo = new()
        {
            ImageLayout = ImageLayout.General,
            ImageView = VulkanEngine.SwapchainImageViews[index]
        };

        Span<WriteDescriptorSet> writeDescriptorSets = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet(StructureType.WriteDescriptorSet, null,
                CurrentRenderData.InputAttachmentDescriptorSet, 0, 0,
                1, DescriptorType.InputAttachment, &depthImageInfo),

            new WriteDescriptorSet(StructureType.WriteDescriptorSet, null,
                CurrentRenderData.InputAttachmentDescriptorSet, 1, 0,
                1, DescriptorType.InputAttachment, &colorImageInfo)
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, (uint)writeDescriptorSets.Length,
            writeDescriptorSets.GetPinnableReference(), 0, null);

        CurrentRenderData.LastColorImageView = VulkanEngine.SwapchainImageViews[index];
    }


    private unsafe void CreateCameraDataBuffers()
    {
        Span<uint> queue = stackalloc uint[] { VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value };

        foreach (var renderData in _renderData)
        {
            renderData.CameraDataBuffer = MemoryBuffer.Create(
                BufferUsageFlags.UniformBufferBit,
                (ulong)Marshal.SizeOf<CameraData>(), SharingMode.Exclusive, queue,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, false);
        }
    }

    private unsafe void CreateCameraDataDescriptors()
    {
        Span<WriteDescriptorSet> cameraDataDescriptorWrites = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                DstBinding = 0,
                DstArrayElement = 0,
            }
        };

        foreach (var renderData in _renderData)
        {
            ref var cameraDataDescriptor = ref renderData.CameraDataDescriptors;
            cameraDataDescriptor = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.CameraData);

            var bufferInfo = new DescriptorBufferInfo
            {
                Buffer = renderData.CameraDataBuffer.Buffer,
                Offset = 0,
                Range = (ulong)Marshal.SizeOf<CameraData>()
            };


            cameraDataDescriptorWrites[0].DstSet = cameraDataDescriptor;
            cameraDataDescriptorWrites[0].PBufferInfo = &bufferInfo;

            VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, cameraDataDescriptorWrites, 0, null);
        }
    }


    public override Identification Identification => SystemIDs.VoxelRender;

    public override void Dispose()
    {
        _worker?.Stop();

        foreach (var renderData in _renderData)
        {
            DescriptorSetHandler.FreeDescriptorSet(renderData.InputAttachmentDescriptorSet);
            DescriptorSetHandler.FreeDescriptorSet(renderData.CameraDataDescriptors);
            renderData.CameraDataBuffer.Dispose();
        }

        base.Dispose();
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct CameraData
    {
        [FieldOffset(sizeof(float) * 0)] public float HFov;
        [FieldOffset(sizeof(float) * 1)] public float AspectRatio;
        [FieldOffset(sizeof(float) * 2)] public Vector3 Forward;
        [FieldOffset(sizeof(float) * 5)] public Vector3 Upward;

        [FieldOffset(sizeof(float) * 8)] public Vector3 Position;
        [FieldOffset(sizeof(float) * 11)] public float Near;
        [FieldOffset(sizeof(float) * 12)] public float Far;
    }
}