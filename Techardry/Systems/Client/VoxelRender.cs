using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MintyCore.Components.Client;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.SystemGroups;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Techardry.Identifications;
using Techardry.Lib.FastNoseLite;
using Techardry.Voxels;

namespace Techardry.Systems.Client;

[RegisterSystem("voxel_render")]
[ExecuteInSystemGroup(typeof(PresentationSystemGroup))]
[ExecuteAfter(typeof(MintyCore.Systems.Client.RenderInstancedSystem))]
public partial class VoxelRender : ASystem
{
    [ComponentQuery] private ComponentQuery<object, Camera> _cameraQuery = new();

    private Mesh _mesh;
    private MemoryBuffer _buffer;
    private DescriptorSet octreeDescriptorSet;

    private int totalNodeSize;
    private int totalDataSize;

    private CommandBuffer _commandBuffer;

    private VoxelOctree _octree = new(VoxelOctree.MaximumLevelCount);

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
        CreateBuffer();
        CreateDescriptors();
    }

    private unsafe void CreateDescriptors()
    {
        octreeDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctree);

        DescriptorBufferInfo nodeBufferInfo = new()
        {
            Buffer = _buffer.Buffer,
            Offset = 0,
            Range = (ulong) totalNodeSize
        };
        DescriptorBufferInfo dataBufferInfo = new()
        {
            Buffer = _buffer.Buffer,
            Offset = (ulong) totalNodeSize,
            Range = (ulong) totalDataSize
        };

        Span<WriteDescriptorSet> writeSets = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = octreeDescriptorSet,
                PBufferInfo = &nodeBufferInfo
            },
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 1,
                DstSet = octreeDescriptorSet,
                PBufferInfo = &dataBufferInfo
            }
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, (uint) writeSets.Length, writeSets, 0,
            Array.Empty<CopyDescriptorSet>().AsSpan());
    }

    private unsafe void CreateBuffer()
    {
        int nodeSize = Marshal.SizeOf<VoxelOctree.Node>();
        int dataSize = Marshal.SizeOf<VoxelRenderData>();

        var nodeCount = _octree.NodeCount.left;
        var dataCount = _octree.DataCount.left;

        totalNodeSize = nodeSize * nodeCount;
        totalDataSize = dataSize * dataCount;

        var dataOffset = totalNodeSize;

        var totalSize = totalNodeSize + totalDataSize;

        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};

        _buffer = MemoryBuffer.Create(
            BufferUsageFlags.BufferUsageStorageBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
            (ulong) totalSize,
            SharingMode.Exclusive,
            queue, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit, false);

        var stagingBuffer = MemoryBuffer.Create(BufferUsageFlags.BufferUsageTransferSrcBit, (ulong) totalSize,
            SharingMode.Exclusive, queue,
            MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit, true);

        var stagingBufferPtr = MemoryManager.Map(stagingBuffer.Memory);

        var targetNodes = new Span<VoxelOctree.Node>((void*) stagingBufferPtr, nodeCount);
        var sourceNodes = _octree._nodes.AsSpan(0, nodeCount);
        sourceNodes.CopyTo(targetNodes);

        var targetData = new Span<VoxelRenderData>((void*) (stagingBufferPtr + dataOffset), dataCount);
        var sourceData = _octree._data.renderData.AsSpan(0, dataCount);
        sourceData.CopyTo(targetData);

        MemoryManager.UnMap(stagingBuffer.Memory);

        var commandBuffer = VulkanEngine.GetSingleTimeCommandBuffer();
        Span<BufferCopy> bufferCopy = stackalloc BufferCopy[]
        {
            new BufferCopy(0, 0, (ulong) totalSize),
        };
        VulkanEngine.Vk.CmdCopyBuffer(commandBuffer, stagingBuffer.Buffer, _buffer.Buffer, bufferCopy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(commandBuffer);

        stagingBuffer.Dispose();
    }

    private void FillOctree()
    {
        int seed = 4;
        Random rnd = new Random(seed);

        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.02f);

        //_octree.Insert(new VoxelData(BlockIDs.Stone), Vector3.Zero, 0);

        for (int x = 0; x < VoxelOctree.Dimensions; x++)
        {
            for (int z = 0; z < VoxelOctree.Dimensions; z++)
            {
                for (int y = 0; y < 6; y++)
                {
                    _octree.Insert(new VoxelData(BlockIDs.Dirt), new Vector3(x, y, z), VoxelOctree.SizeOneDepth);
                }

                var noiseValue = noise.GetNoise(x, z);
                noiseValue += 0.5f;
                noiseValue /= 0.5f;
                noiseValue *= 6;

                for (int y = 6; y < 7 + noiseValue; y++)
                {
                    _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(x, y, z), VoxelOctree.SizeOneDepth);
                }
            }
        }

        
    }

    public override void PreExecuteMainThread()
    {
        base.PreExecuteMainThread();
        _commandBuffer = VulkanEngine.GetSecondaryCommandBuffer();
    }

    public override void PostExecuteMainThread()
    {
        base.PostExecuteMainThread();
        VulkanEngine.ExecuteSecondary(_commandBuffer);
    }

    protected override unsafe void Execute()
    {
        //if (World is null) return;
        //
        //var cameraEntity = cameraQuery.FirstOrDefault();
        //
        //if (cameraEntity.Entity.ArchetypeId == default) return;

        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.Voxel);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.Voxel);

        var vk = VulkanEngine.Vk;
        vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, pipeline);
        vk.CmdBindVertexBuffers(_commandBuffer, 0, 1, _mesh.MemoryBuffer.Buffer, 0);

        Span<Viewport> viewports = stackalloc Viewport[]
        {
            new Viewport(0, 0, VulkanEngine.SwapchainExtent.Width, VulkanEngine.SwapchainExtent.Height, 0, 1)
        };
        vk.CmdSetViewport(_commandBuffer, 0, viewports);

        Span<Rect2D> scissors = stackalloc Rect2D[]
        {
            new Rect2D(new Offset2D(0, 0), VulkanEngine.SwapchainExtent)
        };
        vk.CmdSetScissor(_commandBuffer, 0, scissors);

        var descriptorSet = octreeDescriptorSet;
        vk.CmdBindDescriptorSets(_commandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0, 1,
            &descriptorSet, 0, null);

        vk.CmdDraw(_commandBuffer, _mesh.VertexCount, 1, 0, 0);
    }

    public override Identification Identification => SystemIDs.VoxelRender;

    public override void Dispose()
    {
        base.Dispose();
        _mesh.Dispose();
        _buffer.Dispose();
    }
}