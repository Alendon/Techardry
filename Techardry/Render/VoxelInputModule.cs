using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepuUtilities.Memory;
using DotNext.Threading;
using MintyCore.Graphics;
using MintyCore.Graphics.Managers;
using MintyCore.Graphics.Render;
using MintyCore.Graphics.Render.Data;
using MintyCore.Graphics.Render.Data.RegistryWrapper;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Serilog;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.Voxels;

namespace Techardry.Render;

[RegisterInputDataModule("voxel")]
public class VoxelInputModule(IMemoryManager memoryManager, IVulkanEngine vulkanEngine) : InputModule
{
    private DictionaryInputData<Int3, VoxelOctree>? _inputData;


    private Func<VoxelIntermediateData>? _intermediateDataFunc;

    public override void Setup()
    {
        _inputData = ModuleDataAccessor.UseDictionaryInputData<Int3, VoxelOctree>(RenderInputDataIDs.Voxel, this);
        _intermediateDataFunc =
            ModuleDataAccessor.ProvideIntermediateData<VoxelIntermediateData>(IntermediateRenderDataIDs.Voxel, this);
    }

    public override unsafe void Update(ManagedCommandBuffer commandBuffer)
    {
        if (_inputData is null || _intermediateDataFunc is null)
        {
            Log.Error("Voxel Input Module is not setup correctly");
            return;
        }

        FreeOldStagingBuffers();

        var intermediateData = _intermediateDataFunc();

        foreach (var (chunkPosition, octree) in _inputData.AcquireData())
        {
            if (intermediateData.TryUseOldBuffer(chunkPosition, octree.Version))
            {
                continue;
            }

            using var readLock = octree.AcquireReadLock();
            var bufferSize = CalculateOctreeBufferSize(octree);

            var stagingBuffer = GetStagingBuffer(bufferSize);
            var buffer = CreateBuffer(bufferSize);

            var mappedStagingBuffer = memoryManager.Map(stagingBuffer.Memory);
            FillHeader(mappedStagingBuffer, chunkPosition, octree);

            var srcNodes = octree.Nodes.AsSpan(0, (int)octree.NodeCount);
            var dstNodes = new Span<VoxelOctree.Node>((void*)(mappedStagingBuffer + Marshal.SizeOf<OctreeHeader>()),
                (int)octree.NodeCount);
            srcNodes.CopyTo(dstNodes);

            var srcData = octree.Data.renderData.AsSpan(0, (int)octree.DataCount);
            var dstData = new Span<VoxelRenderData>(
                (void*)(mappedStagingBuffer + Marshal.SizeOf<OctreeHeader>() +
                        Marshal.SizeOf<VoxelOctree.Node>() * octree.NodeCount), (int)octree.DataCount);
            srcData.CopyTo(dstData);

            memoryManager.UnMap(stagingBuffer.Memory);

            commandBuffer.CopyBuffer(stagingBuffer, buffer);

            intermediateData.SetNewBuffer(chunkPosition, octree.Version, buffer);
        }

        intermediateData.ReleaseOldUnusedBuffers();
    }


    private static unsafe void FillHeader(IntPtr stagingBuffer, Int3 position, VoxelOctree octree)
    {
        var worldPosition = new Vector3(position.X, position.Y, position.Z) * VoxelOctree.Dimensions;
        ref var header = ref Unsafe.AsRef<OctreeHeader>(stagingBuffer.ToPointer());
        var transform = Matrix4x4.CreateTranslation(worldPosition);
        Matrix4x4.Invert(transform, out var inverseTransform);

        header = new OctreeHeader
        {
            TreeType = TreeType.Octree,
            NodeCount = octree.NodeCount,
            Transform = transform,
            InverseTransform = inverseTransform
        };

        header.TransposedNormalMatrix[0] = inverseTransform.M11;
        header.TransposedNormalMatrix[1] = inverseTransform.M21;
        header.TransposedNormalMatrix[2] = inverseTransform.M31;

        header.TransposedNormalMatrix[3] = inverseTransform.M12;
        header.TransposedNormalMatrix[4] = inverseTransform.M22;
        header.TransposedNormalMatrix[5] = inverseTransform.M32;

        header.TransposedNormalMatrix[6] = inverseTransform.M13;
        header.TransposedNormalMatrix[7] = inverseTransform.M23;
        header.TransposedNormalMatrix[8] = inverseTransform.M33;
    }

    private static uint CalculateOctreeBufferSize(VoxelOctree octree)
    {
        var headerSize = (uint)Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint)Marshal.SizeOf<VoxelOctree.Node>();
        var dataSize = (uint)Marshal.SizeOf<VoxelRenderData>();

        return headerSize + nodeSize * octree.NodeCount + dataSize * octree.DataCount;
    }

    private MemoryBuffer CreateBuffer(uint size)
    {
        return memoryManager.CreateBuffer(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
            size,
            [vulkanEngine.GraphicQueue.familyIndex],
            MemoryPropertyFlags.DeviceLocalBit,
            false);
    }

    private List<MemoryBuffer> _stagingBuffers = new();

    private MemoryBuffer GetStagingBuffer(uint size)
    {
        var buffer = memoryManager.CreateBuffer(
            BufferUsageFlags.TransferSrcBit,
            size,
            [vulkanEngine.GraphicQueue.familyIndex],
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            false);

        _stagingBuffers.Add(buffer);
        return buffer;
    }

    private void FreeOldStagingBuffers()
    {
        foreach (var buffer in _stagingBuffers)
        {
            buffer.Dispose();
        }

        _stagingBuffers.Clear();
    }

    public override void Dispose()
    {
        FreeOldStagingBuffers();
    }

    public override Identification Identification => RenderInputModuleIDs.Voxel;

    [RegisterKeyIndexedInputData("voxel")]
    public static DictionaryInputDataRegistryWrapper<Int3, VoxelOctree> VoxelOctreeInputData => new();
}