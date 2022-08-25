using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using JetBrains.Annotations;
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
using MathHelper = MintyCore.Utils.Maths.MathHelper;
using PipelineIDs = Techardry.Identifications.PipelineIDs;
using SystemIDs = Techardry.Identifications.SystemIDs;

namespace Techardry.Systems.Client;

[RegisterSystem("voxel_render")]
[ExecuteInSystemGroup(typeof(DualRenderSystemGroup))]
[ExecuteAfter(typeof(RenderInstancedSystem))]
public partial class VoxelRender : ARenderSystem
{
    [ComponentQuery] private ComponentQuery<object, (Camera, Position)> _cameraQuery = new();

    private Dictionary<Int3, MemoryBuffer> _chunkOctreeBuffers = new();
    private Dictionary<Int3, int> _chunkDescriptorSetIndices = new();
    private DescriptorSet _octreeDescriptorSet;

    private DescriptorSet[] _inputAttachmentDescriptorSet = new DescriptorSet[VulkanEngine.SwapchainImageCount];

    private ImageView[] _lastColorImageView = new ImageView[VulkanEngine.SwapchainImageCount];

    private MemoryBuffer[] _cameraDataBuffers = Array.Empty<MemoryBuffer>();
    private MemoryBuffer _cameraDataStagingBuffer;
    private DescriptorSet[] _cameraDataDescriptors = Array.Empty<DescriptorSet>();

    private MemoryBuffer _masterOctreeBuffer;
    private DescriptorSet _masterOctreeDescriptorSet;

    private int _renderDiameterLog = 2;
    private Int3 _centralChunk = new Int3(0, 0, 0);

    public override void Setup(SystemManager systemManager)
    {
        _cameraQuery.Setup(this);

        SetRenderArguments(new RenderPassArguments
        {
            SubpassIndex = 1
        });
    }

    bool _firstFrame = true;

    private void LateSetup()
    {
        if (!_firstFrame) return;
        _firstFrame = false;

        CreateVoxelBuffer();
        CreateDescriptors();
        CreateCameraDataBuffers();
        CreateCameraDataDescriptors();
        FillMasterOctree();
    }

    public override void PreExecuteMainThread()
    {
        LateSetup();

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
        _octreeDescriptorSet =
            DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctree, _chunkOctreeBuffers.Count);

        var bufferInfosArr = new DescriptorBufferInfo[_chunkOctreeBuffers.Count];
        var descriptorWritesArr = new WriteDescriptorSet[_chunkOctreeBuffers.Count];
        var bufferInfosHandle = GCHandle.Alloc(bufferInfosArr, GCHandleType.Pinned);
        var descriptorWritesHandle = GCHandle.Alloc(descriptorWritesArr, GCHandleType.Pinned);
        var bufferInfos = bufferInfosArr.AsSpan();
        var descriptorWrites = descriptorWritesArr.AsSpan();

        int i = 0;
        foreach (var (pos, buffer) in _chunkOctreeBuffers)
        {
            bufferInfos[i] = new DescriptorBufferInfo
            {
                Buffer = buffer.Buffer,
                Offset = 0,
                Range = buffer.Size
            };

            descriptorWrites[i] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = _octreeDescriptorSet,
                DstArrayElement = (uint) i,
                PBufferInfo = (DescriptorBufferInfo*) Unsafe.AsPointer(ref bufferInfos[i])
            };

            _chunkDescriptorSetIndices[pos] = i;

            i++;
        }

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, descriptorWrites, 0, null);

        bufferInfosHandle.Free();
        descriptorWritesHandle.Free();

        File.WriteAllText("indices.json",
            JsonSerializer.Serialize(_chunkDescriptorSetIndices.OrderBy(pair => pair.Value).Select(x => x.Key)));
    }

    [StructLayout(LayoutKind.Explicit)]
    struct OctreeHeader
    {
        [UsedImplicitly] [FieldOffset(0)] public uint NodeCount;
        [UsedImplicitly] [FieldOffset(4)] public Int3 treeMin;
    }

    private unsafe void CreateVoxelBuffer()
    {
        if (World is not TechardryWorld techardryWorld) return;

        var headerSize = (uint) Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint) Marshal.SizeOf<VoxelOctree.Node>();
        var dataSize = (uint) Marshal.SizeOf<VoxelRenderData>();

        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};


        foreach (var chunkPos in techardryWorld.GetLoadedChunks())
        {
            if (!techardryWorld.TryGetChunk(chunkPos, out var chunk)) continue;

            var cb = VulkanEngine.GetSingleTimeCommandBuffer();

            var octree = chunk.Octree;

            var nodeCount = octree.NodeCount;
            var dataCount = octree.DataCount;

            var bufferSize = headerSize + nodeCount * nodeSize + dataCount * dataSize;

            var buffer = MemoryBuffer.Create(
                BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
                bufferSize,
                SharingMode.Exclusive,
                queue,
                MemoryPropertyFlags.MemoryPropertyDeviceLocalBit,
                false);

            _chunkOctreeBuffers.Add(chunkPos, buffer);


            MemoryBuffer stagingBuffer = MemoryBuffer.Create(
                BufferUsageFlags.BufferUsageTransferSrcBit,
                buffer.Size,
                SharingMode.Exclusive,
                queue,
                MemoryPropertyFlags.MemoryPropertyHostVisibleBit |
                MemoryPropertyFlags.MemoryPropertyHostCoherentBit,
                true);


            var stagingBufferPtr = MemoryManager.Map(stagingBuffer.Memory);

            var header = new OctreeHeader
            {
                NodeCount = nodeCount,
                treeMin = chunkPos
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

            File.WriteAllBytes($"code_export_{chunkPos.X}_{chunkPos.Y}_{chunkPos.Z}.bin",
                new Span<byte>((void*) stagingBufferPtr, (int) stagingBuffer.Size).ToArray());

            MemoryManager.UnMap(stagingBuffer.Memory);

            BufferCopy copy = new()
            {
                Size = buffer.Size,
                SrcOffset = 0,
                DstOffset = 0
            };

            VulkanEngine.Vk.CmdCopyBuffer(cb, stagingBuffer.Buffer, buffer.Buffer, 1, copy);
            VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

            stagingBuffer.Dispose();
        }
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

    [StructLayout(LayoutKind.Explicit)]
    struct MasterOctreeHeader
    {
        [FieldOffset(0)] public Int3 TreeMin;
        [FieldOffset(12)] public int Dimension;
        [FieldOffset(16)] public int Depth;
    }

    private static int CoordinatesToIndex(Int3 coordinates, int dimension)
    {
        int halfDimension = dimension / 2;
        int index = 0;
        while (halfDimension >= 1)
        {
            index *= 8;
            if (coordinates.Z >= halfDimension)
            {
                index += 1;
            }

            if (coordinates.Y >= halfDimension)
            {
                index += 2;
            }

            if (coordinates.X >= halfDimension)
            {
                index += 4;
            }

            coordinates %= halfDimension;
            halfDimension /= 2;
        }

        return index;
    }


    private unsafe void FillMasterOctree()
    {
        var chunkDiameter = (int) Math.Pow(2, _renderDiameterLog);
        var chunkMin = _centralChunk - new Int3(chunkDiameter / 2);

        var octreeDepth = _renderDiameterLog;

        var chunkCount = chunkDiameter * chunkDiameter * chunkDiameter;

        MasterOctreeHeader header = new()
        {
            Depth = octreeDepth,
            Dimension = chunkDiameter * VoxelOctree.Dimensions,
            TreeMin = chunkMin * VoxelOctree.Dimensions
        };

        var queues = (stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value});

        var stagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageTransferSrcBit,
            (ulong) (Marshal.SizeOf<MasterOctreeHeader>() + Marshal.SizeOf<int>() * chunkCount),
            SharingMode.Exclusive, queues, MemoryPropertyFlags.MemoryPropertyHostVisibleBit |
                                           MemoryPropertyFlags.MemoryPropertyHostCoherentBit, true);

        var pointer = MemoryManager.Map(stagingBuffer.Memory);
        *(MasterOctreeHeader*) pointer = header;
        var targetNodes = new Span<int>((void*) (pointer + Marshal.SizeOf<MasterOctreeHeader>()), chunkCount);
        targetNodes.Fill(-1);

        int max = int.MinValue;

        for (var x = 0; x < chunkDiameter; x++)
        {
            for (var y = 0; y < chunkDiameter; y++)
            {
                for (var z = 0; z < chunkDiameter; z++)
                {
                    var coordinates = new Int3(x, y, z);
                    var chunkCoordinates = chunkMin + coordinates;
                    if (!_chunkDescriptorSetIndices.TryGetValue(chunkCoordinates, out var descriptorSetIndex))
                    {
                        continue;
                    }


                    targetNodes[CoordinatesToIndex(coordinates, chunkDiameter)] = descriptorSetIndex;
                }
            }
        }

        var stream = new FileStream("master_buffer.bin", FileMode.Create);
        var data = new Span<byte>(pointer.ToPointer(), (int) stagingBuffer.Size);
        stream.Write(data);
        stream.Dispose();

        MemoryManager.UnMap(stagingBuffer.Memory);

        _masterOctreeBuffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
            stagingBuffer.Size,
            SharingMode.Exclusive, queues, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit, false);

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();
        BufferCopy copy = new()
        {
            SrcOffset = 0,
            DstOffset = 0,
            Size = stagingBuffer.Size
        };

        VulkanEngine.Vk.CmdCopyBuffer(cb, stagingBuffer.Buffer, _masterOctreeBuffer.Buffer, 1, copy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

        stagingBuffer.Dispose();

        _masterOctreeDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.MasterOctree);

        DescriptorBufferInfo bufferInfo = new()
        {
            Buffer = _masterOctreeBuffer.Buffer,
            Offset = 0,
            Range = _masterOctreeBuffer.Size
        };
        WriteDescriptorSet writeDescriptorSet = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            DstBinding = 0,
            DstSet = _masterOctreeDescriptorSet,
            PBufferInfo = &bufferInfo
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, 1, &writeDescriptorSet, 0, null);
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
            _masterOctreeDescriptorSet,
            _octreeDescriptorSet,
        };


        vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0,
            (uint) descriptorSets.Length,
            descriptorSets, 0, (uint*) null);

        vk.CmdDraw(CommandBuffer, 6, 1, 0, 0);
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

        foreach (var buffer in _chunkOctreeBuffers.Values)
        {
            buffer.Dispose();
        }

        _chunkOctreeBuffers.Clear();

        _masterOctreeBuffer.Dispose();

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