using System.Numerics;
using System.Runtime.InteropServices;
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
using Techardry.Voxels;
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
    private MemoryBuffer _nodeBuffer;
    private MemoryBuffer _dataBuffer;
    private DescriptorSet _octreeDescriptorSet;
    private DescriptorSet[] _inputAttachmentDescriptorSet = new DescriptorSet[VulkanEngine.SwapchainImageCount];

    private ImageView[] _lastColorImageView = new ImageView[VulkanEngine.SwapchainImageCount];

    private MemoryBuffer[] _cameraDataBuffers = Array.Empty<MemoryBuffer>();
    private MemoryBuffer _cameraDataStagingBuffer;
    private DescriptorSet[] _cameraDataDescriptors = Array.Empty<DescriptorSet>();

    private int totalNodeSize;
    private int totalDataSize;

    private VoxelOctree _octree = new();

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


        FillOctree();
        CreateNodeBuffer();
        CreateDataBuffer();
        CreateDescriptors();
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
        _octreeDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctree);

        DescriptorBufferInfo nodeBufferInfo = new DescriptorBufferInfo(
            _nodeBuffer.Buffer,
            0,
            _nodeBuffer.Size
        );
        
        DescriptorBufferInfo dataBufferInfo = new DescriptorBufferInfo(
            _dataBuffer.Buffer,
            0,
            _dataBuffer.Size
        );
        
        

        Span<WriteDescriptorSet> nodeDescriptorWrites = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = _octreeDescriptorSet,
                DstArrayElement = 0,
                PBufferInfo = &nodeBufferInfo
            },
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 1,
                DstSet = _octreeDescriptorSet,
                DstArrayElement = 0,
                PBufferInfo = &dataBufferInfo
            }
        };


        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, nodeDescriptorWrites, 0, null);
    }

    private unsafe void CreateNodeBuffer()
    {
        int nodeSize = Marshal.SizeOf<VoxelOctree.Node>();
        var nodeCount = _octree.NodeCount;
        totalNodeSize = nodeSize * nodeCount;

        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};

        _nodeBuffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
            (ulong) totalNodeSize,
            SharingMode.Exclusive,
            queue, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit, false);

        var stagingBuffer = MemoryBuffer.Create(BufferUsageFlags.BufferUsageTransferSrcBit, (ulong) totalNodeSize,
            SharingMode.Exclusive, queue,
            MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit, true);

        var stagingBufferPtr = MemoryManager.Map(stagingBuffer.Memory);

        var targetNodes = new Span<VoxelOctree.Node>((void*) stagingBufferPtr, nodeCount);
        var sourceNodes = _octree.Nodes.AsSpan(0, nodeCount);
        sourceNodes.CopyTo(targetNodes);

        MemoryManager.UnMap(stagingBuffer.Memory);

        var commandBuffer = VulkanEngine.GetSingleTimeCommandBuffer();
        Span<BufferCopy> bufferCopy = stackalloc BufferCopy[]
        {
            new BufferCopy(0, 0, (ulong) totalNodeSize),
        };
        VulkanEngine.Vk.CmdCopyBuffer(commandBuffer, stagingBuffer.Buffer, _nodeBuffer.Buffer, bufferCopy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(commandBuffer);

        stagingBuffer.Dispose();
    }

    private unsafe void CreateDataBuffer()
    {
        int dataSize = Marshal.SizeOf<VoxelRenderData>();
        var dataCount = _octree.DataCount;
        totalDataSize = dataSize * dataCount;

        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};

        _dataBuffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
            (ulong) totalDataSize,
            SharingMode.Exclusive,
            queue, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit, false);

        var stagingBuffer = MemoryBuffer.Create(BufferUsageFlags.BufferUsageTransferSrcBit, (ulong) totalDataSize,
            SharingMode.Exclusive, queue,
            MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit, true);

        var stagingBufferPtr = MemoryManager.Map(stagingBuffer.Memory);

        var targetData = new Span<VoxelRenderData>((void*) stagingBufferPtr, dataCount);
        var sourceData = _octree.Data.renderData.AsSpan(0, dataCount);
        sourceData.CopyTo(targetData);

        MemoryManager.UnMap(stagingBuffer.Memory);

        var commandBuffer = VulkanEngine.GetSingleTimeCommandBuffer();
        Span<BufferCopy> bufferCopy = stackalloc BufferCopy[]
        {
            new BufferCopy(0, 0, (ulong) totalDataSize),
        };
        VulkanEngine.Vk.CmdCopyBuffer(commandBuffer, stagingBuffer.Buffer, _dataBuffer.Buffer, bufferCopy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(commandBuffer);

        stagingBuffer.Dispose();
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

    private void FillOctree()
    {
        int seed = 5;
        
        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.02f);

        int placed = 0;
        for (int x = 0; x < VoxelOctree.Dimensions; x++)
        {
            for (int z = 0; z < VoxelOctree.Dimensions; z++)
            {
                for (int y = 0; y < 6; y++)
                {
                    placed++;
                    _octree.Insert(new VoxelData(BlockIDs.Stone), new Vector3(x, y, z), VoxelOctree.SizeOneDepth);
                }

                var noiseValue = noise.GetNoise(x, z);
                noiseValue += 0.5f;
                noiseValue /= 0.5f;
                noiseValue *= 6;

                for (int y = 6; y < 7 + noiseValue; y++)
                {
                    placed++;
                    _octree.Insert(new VoxelData(BlockIDs.Dirt), new Vector3(x, y, z), VoxelOctree.SizeOneDepth);
                }
            }
        }
        
        Logger.WriteLog($"{placed} voxels placed. {_octree.NodeCount} nodes and {_octree.DataCount} data elements used.", LogImportance.Debug, "VoxelRender");
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

        _dataBuffer.Dispose();
        _nodeBuffer.Dispose();

        _octree.Dispose();

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