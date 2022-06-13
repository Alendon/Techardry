using System.Diagnostics;
using System.Numerics;
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
    private MemoryBuffer _nodeBuffer;
    private MemoryBuffer _dataBuffer;
    private DescriptorSet octreeNodeDescriptorSet;
    private DescriptorSet octreeDataDescriptorSet;

    private int totalNodeSize;
    private int totalDataSize;

    private CommandBuffer _commandBuffer;

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
    }

    private unsafe void CreateDescriptors()
    {
        octreeNodeDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctreeNode);

        DescriptorBufferInfo nodeBufferInfo = new DescriptorBufferInfo(
            _nodeBuffer.Buffer,
            0,
            _nodeBuffer.Size
        );

        Span<WriteDescriptorSet> nodeDescriptorWrites = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = octreeNodeDescriptorSet,
                DstArrayElement = 0,
                PBufferInfo = &nodeBufferInfo
            }
        };


        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, nodeDescriptorWrites, 0, null);


        octreeDataDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctreeData);

        DescriptorBufferInfo dataBufferInfo = new DescriptorBufferInfo(
            _dataBuffer.Buffer,
            0,
            _dataBuffer.Size
        );

        Span<WriteDescriptorSet> dataDescriptorWrites = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = octreeDataDescriptorSet,
                DstArrayElement = 0,
                PBufferInfo = &dataBufferInfo
            }
        };


        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, dataDescriptorWrites, 0, null);
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

    private void FillOctree()
    {
        int seed = 4;
        Random rnd = new Random(seed);

        var noise = new FastNoiseLite(seed);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.02f);

        /*_octree.Insert(new VoxelData(BlockIDs.Stone), Vector3.Zero, 0);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(0, 0, 0), 3);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(0, 0, 15), 3);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(0, 15, 0), 3);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(0, 15, 15), 3);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(15, 0, 0), 3);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(15, 0, 15), 3);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(15, 15, 0), 3);
        _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(15, 15, 15), 3);*/

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
                    if (x == 3 && z == 15 && y == 9)
                    {
                        
                    }
                    
                    _octree.Insert(new VoxelData(BlockIDs.Grass), new Vector3(x, y, z), VoxelOctree.SizeOneDepth);
                }
            }
        }

        return;

        Image<Rgba32> image = new Image<Rgba32>(1000, 1000);

        var rotation = Matrix4x4.CreateFromYawPitchRoll(0, 0.0f, 0);

        Stopwatch sw = Stopwatch.StartNew();

        int iterations = 1;

        for (int i = 0; i < iterations; i++)
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var yAdjusted = (y - image.Height / 2) * 0.001f;
                var xAdjusted = (x - image.Width / 2) * 0.001f;

                var cameraDir = -Vector3.UnitZ;
                var cameraPlaneU = Vector3.UnitX;
                var cameraPlaneV = Vector3.UnitY;
                var rayDir = cameraDir + xAdjusted * cameraPlaneU + yAdjusted * cameraPlaneV;
                var rayPos = new Vector3(0, 0, 64);

                //rotate the ray dir and pos by the rotation matrix
                rayDir = Vector3.Transform(rayDir, rotation);
                rayPos = Vector3.Transform(rayPos, rotation);

                rayPos += new Vector3(8, 8, 0);

                Rgba32 color = Color.White;

                if (x == 400 && y == 475)
                {
                    
                }

                bool useConeTracing = false;
                Vector3 normal;
                if (useConeTracing)
                {
                    if (_octree.ConeTrace(rayPos, rayDir, 0.001f, out var node, out normal))
                    {
                        var voxel = _octree.GetVoxelRenderDataRef(ref node);
                        var voxelColor = voxel.Color;
                        color.FromVector4(voxelColor);
                    }
                }
                else
                {
                    if (_octree.Raycast(rayPos, rayDir, out var node, out normal))
                    {
                        var voxel = _octree.GetVoxelRenderDataRef(ref node);
                        var voxelColor = voxel.Color;
                        color.FromVector4(voxelColor);
                    }
                }

                if (normal.X < 0 || normal.Y < 0 || normal.Z < 0)
                {
                    normal = Vector3.Negate(normal);
                }

                if (normal.Equals(Vector3.UnitX))
                {
                    color.R = (byte) (color.R * 0.8);
                    color.G = (byte) (color.G * 0.8);
                    color.B = (byte) (color.B * 0.8);
                }

                if (normal.Equals(Vector3.UnitY))
                {
                    color.R = (byte) (color.R * 0.9);
                    color.G = (byte) (color.G * 0.9);
                    color.B = (byte) (color.B * 0.9);
                }

                if (normal.Equals(Vector3.UnitZ))
                {
                    color.R = (byte) (color.R * 1);
                    color.G = (byte) (color.G * 1);
                    color.B = (byte) (color.B * 1);
                }

                image[x, y] = color;
            }
        }

        sw.Stop();

        var fileStream = new FileStream("test.png", FileMode.Create);
        image.SaveAsPng(fileStream);
        fileStream.Dispose();

        Console.WriteLine($"Rendering took {sw.Elapsed.TotalMilliseconds / iterations}ms per frame");
        Console.WriteLine("Bye, World!");
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

        Span<DescriptorSet> descriptorSets = stackalloc DescriptorSet[]
        {
            octreeNodeDescriptorSet,
            octreeDataDescriptorSet
        };


        vk.CmdBindDescriptorSets(_commandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0,
            (uint) descriptorSets.Length,
            descriptorSets, 0, (uint*) null);
        
        vk.CmdDraw(_commandBuffer, _mesh.VertexCount, 1, 0, 0);
    }

    public override Identification Identification => SystemIDs.VoxelRender;

    public override void Dispose()
    {
        base.Dispose();
        _mesh.Dispose();
    }
}