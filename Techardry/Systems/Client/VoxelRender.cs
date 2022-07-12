﻿using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Lib.FastNoseLite;
using Techardry.Render;
using Techardry.Utils;
using Techardry.Voxels;
using Techardry.World;
using DescriptorSetIDs = Techardry.Identifications.DescriptorSetIDs;
using PipelineIDs = Techardry.Identifications.PipelineIDs;
using SystemIDs = Techardry.Identifications.SystemIDs;

namespace Techardry.Systems.Client;

[RegisterSystem("voxel_render")]
[ExecuteInSystemGroup(typeof(DualRenderSystemGroup))]
[ExecuteAfter(typeof(RenderInstancedSystem))]
public partial class VoxelRender : ARenderSystem
{
    [ComponentQuery] private ComponentQuery<object, (Camera, Position)> _cameraQuery = new();

    private Mesh _mesh;

    private Dictionary<Int3, (MemoryBuffer, int descriptorIndex)> _octreeBuffers = new();

    private ulong _octreeBufferSize;
    private DescriptorSet _octreeDescriptorSet;
    
    private MemoryBuffer _masterOctreeBuffer;
    private DescriptorSet _masterOctreeDescriptorSet;

    private DescriptorSet[] _inputAttachmentDescriptorSet = new DescriptorSet[VulkanEngine.SwapchainImageCount];

    private ImageView[] _lastColorImageView = new ImageView[VulkanEngine.SwapchainImageCount];

    private MemoryBuffer[] _cameraDataBuffers = Array.Empty<MemoryBuffer>();
    private MemoryBuffer _cameraDataStagingBuffer;
    private DescriptorSet[] _cameraDataDescriptors = Array.Empty<DescriptorSet>();

    private TechardryWorld? _world;

    public override void Setup(SystemManager systemManager)
    {
        _cameraQuery.Setup(this);

        Span<Vertex> vertices = stackalloc Vertex[]
        {
            new Vertex(new Vector3(-1, 1, 0), Vector3.Zero, Vector3.Zero, Vector2.Zero),
            new Vertex(new Vector3(-1, -1, 0), Vector3.Zero, Vector3.Zero, Vector2.Zero),
            new Vertex(new Vector3(1, -1, 0), Vector3.Zero, Vector3.Zero, Vector2.Zero),

            new Vertex(new Vector3(-1, 1, 0), Vector3.Zero, Vector3.Zero, Vector2.Zero),
            new Vertex(new Vector3(1, -1, 0), Vector3.Zero, Vector3.Zero, Vector2.Zero),
            new Vertex(new Vector3(1, 1, 0), Vector3.Zero, Vector3.Zero, Vector2.Zero),
        };

        _mesh = MeshHandler.CreateDynamicMesh(vertices, (uint) vertices.Length);

        _world = World as TechardryWorld;

        foreach (var loadedChunk in _world!.GetLoadedChunks())
        {
            CreateVoxelBuffer(loadedChunk);
        }

        CreateDescriptors();
        CreateMasterOctree();
        CreateCameraDataBuffers();
        CreateCameraDataDescriptors();

        SetRenderArguments(new RenderPassArguments
        {
            SubpassIndex = 1
        });
    }

    public override void PreExecuteMainThread()
    {
        var index = VulkanEngine.ImageIndex;
        if (VulkanEngine.SwapchainImageViews[index].Handle != _lastColorImageView[index].Handle)
        {
            DescriptorSetHandler.FreeDescriptorSet(_inputAttachmentDescriptorSet[index]);
            CreateInputAttachments(index);
        }

        base.PreExecuteMainThread();
    }

    private unsafe void CreateInputAttachments(uint index)
    {
        _inputAttachmentDescriptorSet[index] =
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
            new WriteDescriptorSet(StructureType.WriteDescriptorSet, null, _inputAttachmentDescriptorSet[index], 0, 0,
                1, DescriptorType.InputAttachment, &depthImageInfo),

            new WriteDescriptorSet(StructureType.WriteDescriptorSet, null, _inputAttachmentDescriptorSet[index], 1, 0,
                1, DescriptorType.InputAttachment, &colorImageInfo)
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, (uint) writeDescriptorSets.Length,
            writeDescriptorSets.GetPinnableReference(), 0, null);

        _lastColorImageView[index] = VulkanEngine.SwapchainImageViews[index];
    }

    private unsafe void CreateDescriptors()
    {
        _octreeDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctree, 1);

        Span<DescriptorBufferInfo> bufferInfos = stackalloc DescriptorBufferInfo[_octreeBuffers.Count];
        Span<WriteDescriptorSet> writeDescriptorSets = stackalloc WriteDescriptorSet[_octreeBuffers.Count];

        int i = 0;
        foreach (var key in _octreeBuffers.Keys)
        {
            var (buffer, _) = _octreeBuffers[key];

            bufferInfos[i] = new DescriptorBufferInfo(buffer.Buffer, 0, (uint) buffer.Size);
            writeDescriptorSets[i] = new WriteDescriptorSet(StructureType.WriteDescriptorSet, null,
                _octreeDescriptorSet,
                0, (uint?) i, 1, DescriptorType.StorageBuffer,
                (DescriptorImageInfo*) Unsafe.AsPointer(ref bufferInfos[i]));

            _octreeBuffers[key] = (buffer, i);
        }


        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, writeDescriptorSets, 0, null);
    }

    struct OctreeHeader
    {
        [UsedImplicitly] public uint NodeCount;
    }

    private unsafe void CreateVoxelBuffer(Int3 chunkPosition)
    {
        if (_world is null || !_world.TryGetChunk(chunkPosition, out var chunk))
            return;
        var octree = chunk.Octree;

        var headerSize = (uint) Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint) Marshal.SizeOf<VoxelOctree.Node>();
        var dataSize = (uint) Marshal.SizeOf<VoxelRenderData>();

        var nodeCount = octree.NodeCount;
        var dataCount = octree.DataCount;

        _octreeBufferSize = headerSize + nodeCount * nodeSize + dataCount * dataSize;

        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};

        var octreeBuffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
            _octreeBufferSize,
            SharingMode.Exclusive,
            queue,
            MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
            false);

        var stagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageTransferSrcBit,
            _octreeBufferSize,
            SharingMode.Exclusive,
            queue,
            MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
            true);

        var stagingBufferPtr = MemoryManager.Map(stagingBuffer.Memory);

        var header = new OctreeHeader
        {
            NodeCount = nodeCount
        };

        *(OctreeHeader*) stagingBufferPtr = header;

        var srcNodes = octree.Nodes.AsSpan(0, (int) octree.NodeCount);
        var dstNodes = new Span<VoxelOctree.Node>((void*) (stagingBufferPtr + (int) headerSize), srcNodes.Length);
        srcNodes.CopyTo(dstNodes);

        var srcData = octree.Data.renderData.AsSpan(0, (int) octree.DataCount);
        var dstData =
            new Span<VoxelRenderData>((void*) (stagingBufferPtr + (int) headerSize + (int) (nodeCount * nodeSize)),
                srcData.Length);
        srcData.CopyTo(dstData);

        MemoryManager.UnMap(stagingBuffer.Memory);

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();
        BufferCopy copy = new()
        {
            Size = _octreeBufferSize,
            SrcOffset = 0,
            DstOffset = 0
        };

        VulkanEngine.Vk.CmdCopyBuffer(cb, stagingBuffer.Buffer, octreeBuffer.Buffer, 1, copy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);
        stagingBuffer.Dispose();

        _octreeBuffers[chunkPosition] = (octreeBuffer, -1);
    }

    struct MasterOctreeNode
    {
        public uint FirstChildIndex;
        public uint Leaf;
        public uint Data;
    }
    
    void CreateMasterOctree()
    {
        Int3 min = _octreeBuffers.Keys.Aggregate(Int3.Min);
        Int3 max = _octreeBuffers.Keys.Aggregate(Int3.Max);
        
        Int3 size = max - min;
        int maxSize = Math.Max(Math.Max(size.X, size.Y), size.Z);
        int octreeDivision = (int) Math.Ceiling(Math.Log(maxSize, 2));
        
        //use bitshift instead of math.pow
        Int3 octreeDimension = new Int3(1 << octreeDivision);
        
        max = min + octreeDimension;
        
        int usedOctreeNodes = 0;
        
        
    }

    private unsafe void CreateCameraDataBuffers()
    {
        _cameraDataBuffers = new MemoryBuffer[VulkanEngine.SwapchainImageCount];
        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};

        for (int i = 0; i < _cameraDataBuffers.Length; i++)
        {
            ref var cameraDataBuffer = ref _cameraDataBuffers[i];
            cameraDataBuffer = MemoryBuffer.Create(
                BufferUsageFlags.BufferUsageUniformBufferBit |
                BufferUsageFlags.BufferUsageTransferDstBit,
                (ulong) Marshal.SizeOf<CameraData>(), SharingMode.Exclusive, queue,
                MemoryPropertyFlags.MemoryPropertyDeviceLocalBit, false);
        }

        _cameraDataStagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageTransferSrcBit,
            (ulong) Marshal.SizeOf<CameraData>(), SharingMode.Exclusive, queue,
            MemoryPropertyFlags.MemoryPropertyHostVisibleBit |
            MemoryPropertyFlags.MemoryPropertyHostCoherentBit, true);
    }

    private unsafe void CreateCameraDataDescriptors()
    {
        _cameraDataDescriptors = new DescriptorSet[VulkanEngine.SwapchainImageCount];

        Span<WriteDescriptorSet> cameraDataDescriptorWrites = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                DstBinding = 0,
                DstArrayElement = 0,
            }
        };

        for (int i = 0; i < _cameraDataDescriptors.Length; i++)
        {
            ref var cameraDataDescriptor = ref _cameraDataDescriptors[i];
            cameraDataDescriptor = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.CameraData);

            var bufferInfo = new DescriptorBufferInfo()
            {
                Buffer = _cameraDataBuffers[i].Buffer,
                Offset = 0,
                Range = (ulong) Marshal.SizeOf<CameraData>()
            };


            cameraDataDescriptorWrites[0].DstSet = cameraDataDescriptor;
            cameraDataDescriptorWrites[0].PBufferInfo = &bufferInfo;

            VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, cameraDataDescriptorWrites, 0, null);
        }
    }

    private unsafe void UpdateCameraDataDescriptor()
    {
        var bufferInfo = new DescriptorBufferInfo()
        {
            Buffer = _cameraDataBuffers[VulkanEngine.ImageIndex].Buffer,
            Offset = 0,
            Range = (ulong) Marshal.SizeOf<CameraData>()
        };

        var write = new WriteDescriptorSet()
        {
            SType = StructureType.WriteDescriptorSet,
            DescriptorCount = 1,
            DstBinding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DstSet = _cameraDataDescriptors[VulkanEngine.ImageIndex],
            DstArrayElement = 0,
            PBufferInfo = &bufferInfo
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, 1, &write, 0, null);
    }


    protected override unsafe void Execute()
    {
        if (World is null) return;

        var cameraEntity = _cameraQuery.FirstOrDefault();

        if (cameraEntity.Entity.ArchetypeId == default) return;

        var data = MemoryManager.Map(_cameraDataStagingBuffer.Memory);

        var cameraData = cameraEntity.GetCamera();
        var positionData = cameraEntity.GetPosition();
        var forward = cameraData.Forward;
        var up = cameraData.Upward;

        var cameraGpuData = (CameraData*) data;
        cameraGpuData->Forward = forward;
        cameraGpuData->Upward = up;
        cameraGpuData->AspectRatio = VulkanEngine.SwapchainExtent.Width / (float) VulkanEngine.SwapchainExtent.Height;
        cameraGpuData->HFov = cameraData.Fov;
        cameraGpuData->Position = positionData.Value;
        cameraGpuData->Near = 0.1f;
        cameraGpuData->Far = 200f;

        MemoryManager.UnMap(_cameraDataStagingBuffer.Memory);

        BufferCopy copyRegion = new BufferCopy(0, 0, _cameraDataStagingBuffer.Size);

        var commandBuffer = VulkanEngine.GetSingleTimeCommandBuffer();
        VulkanEngine.Vk.CmdCopyBuffer(commandBuffer, _cameraDataStagingBuffer.Buffer,
            _cameraDataBuffers[VulkanEngine.ImageIndex].Buffer, 1, &copyRegion);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(commandBuffer);

        UpdateCameraDataDescriptor();

        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.Voxel);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.Voxel);

        var vk = VulkanEngine.Vk;
        vk.CmdBindPipeline(CommandBuffer, PipelineBindPoint.Graphics, pipeline);
        vk.CmdBindVertexBuffers(CommandBuffer, 0, 1, _mesh.MemoryBuffer.Buffer, 0);

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
            TextureAtlasHandler.TryGetAtlasDescriptorSet(Identifications.TextureAtlasIDs.BlockTexture,
                out var atlasDescriptorSet), "Failed to get atlas descriptor set", "Techardry/Render");

        Span<DescriptorSet> descriptorSets = stackalloc DescriptorSet[]
        {
            _cameraDataDescriptors[VulkanEngine.ImageIndex],
            atlasDescriptorSet,
            _inputAttachmentDescriptorSet[VulkanEngine.ImageIndex],
            _octreeDescriptorSet,
        };


        vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0,
            (uint) descriptorSets.Length,
            descriptorSets, 0, (uint*) null);

        vk.CmdDraw(CommandBuffer, _mesh.VertexCount, 1, 0, 0);
    }

    public override Identification Identification => SystemIDs.VoxelRender;

    public override void Dispose()
    {
        foreach (var descriptorSet in _inputAttachmentDescriptorSet)
        {
            DescriptorSetHandler.FreeDescriptorSet(descriptorSet);
        }

        foreach (var descriptor in _cameraDataDescriptors)
        {
            DescriptorSetHandler.FreeDescriptorSet(descriptor);
        }

        foreach (var memoryBuffer in _cameraDataBuffers)
        {
            memoryBuffer.Dispose();
        }

        _cameraDataStagingBuffer.Dispose();

        DescriptorSetHandler.FreeDescriptorSet(_octreeDescriptorSet);
        foreach (var (buffer,_) in _octreeBuffers.Values)
        {
            buffer.Dispose();
        }
        _octreeBuffers.Clear();

        _mesh.Dispose();
        base.Dispose();
    }

    [StructLayout(LayoutKind.Explicit)]
    struct CameraData
    {
        [FieldOffset(0)] public float HFov;
        [FieldOffset(4)] public float AspectRatio;
        [FieldOffset(8)] public Vector3 Forward;
        [FieldOffset(20)] public Vector3 Upward;

        [FieldOffset(32)] public Vector3 Position;
        [FieldOffset(44)] public float Near;
        [FieldOffset(48)] public float Far;
    }
}