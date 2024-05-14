using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepuUtilities;
using JetBrains.Annotations;
using MintyCore.Graphics;
using MintyCore.Graphics.Managers;
using MintyCore.Graphics.Render;
using MintyCore.Graphics.Utils;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Voxels;
using MathHelper = MintyCore.Utils.Maths.MathHelper;

namespace Techardry.Render;

[RegisterInputDataModule("world")]
[UsedImplicitly]
public class WorldInputModule(
    IMemoryManager memoryManager,
    IVulkanEngine vulkanEngine,
    IDescriptorSetManager descriptorSetManager) : InputModule
{
    private Func<VoxelIntermediateData?>? _voxelBufferAccessor;
    private Func<WorldIntermediateData>? _worldDataAccessor;

    private MemoryBuffer? _stagingNodeBuffer;
    private MemoryBuffer? _stagingIndexBuffer;

    public override void Setup()
    {
        _voxelBufferAccessor =
            ModuleDataAccessor.UseIntermediateData<VoxelIntermediateData>(IntermediateRenderDataIDs.Voxel, this);
        _worldDataAccessor =
            ModuleDataAccessor.ProvideIntermediateData<WorldIntermediateData>(IntermediateRenderDataIDs.World, this);
    }

    public override void Update(ManagedCommandBuffer commandBuffer)
    {
        var voxelBuffer = _voxelBufferAccessor?.Invoke();
        if (voxelBuffer is null || _worldDataAccessor is null)
        {
            return;
        }

        var worldData = _worldDataAccessor();

        if (!voxelBuffer.Buffers.Any())
            return;

        var octrees = voxelBuffer.Buffers.ToArray();
        var boundingBoxes = octrees.Select(x =>
        {
            var (position, _) = x;
            var bMin = new Vector3(position.X, position.Y, position.Z) * VoxelOctree.Dimensions;
            var bMax = bMin + new Vector3(VoxelOctree.Dimensions);

            return new BoundingBox(bMin, bMax);
        }).ToArray();

        var bvh = new MasterBvhTree(boundingBoxes);
        
        var reorderedPointers = new ulong[octrees.Length];
        //take the tree indices from the bvh and reorder the pointers of the octrees
        for (var i = 0; i < bvh.TreeIndices.Length; i++)
        {
            reorderedPointers[i] = octrees[bvh.TreeIndices[i]].address;
        }


        var nodes = bvh.Nodes;

        ApplyBufferData(commandBuffer, nodes, ref _stagingNodeBuffer, ref worldData.BvhNodeBuffer);
        ApplyBufferData(commandBuffer, reorderedPointers, ref _stagingIndexBuffer, ref worldData.BvhIndexBuffer);

        UpdateDescriptorSets(worldData);
    }

    private unsafe void UpdateDescriptorSets(WorldIntermediateData worldData)
    {
        if (worldData.WorldDataDescriptorSet.Handle == default)
        {
            worldData.WorldDataDescriptorSet = descriptorSetManager.AllocateDescriptorSet(DescriptorSetIDs.Render);
        }

        Span<WriteDescriptorSet> write = stackalloc WriteDescriptorSet[2];

        //bvh node buffer
        var nodeBufferInfo = new DescriptorBufferInfo
        {
            Buffer = worldData.BvhNodeBuffer!.Buffer,
            Range = worldData.BvhNodeBuffer.Size
        };
        write[0] = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = worldData.WorldDataDescriptorSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &nodeBufferInfo
        };

        //bvh index buffer
        var indexBufferInfo = new DescriptorBufferInfo
        {
            Buffer = worldData.BvhIndexBuffer!.Buffer,
            Range = worldData.BvhIndexBuffer.Size
        };
        write[1] = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = worldData.WorldDataDescriptorSet,
            DstBinding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &indexBufferInfo
        };

        vulkanEngine.Vk.UpdateDescriptorSets(vulkanEngine.Device, write, ReadOnlySpan<CopyDescriptorSet>.Empty);
    }

    private void ApplyBufferData<TData>(ManagedCommandBuffer commandBuffer, TData[] data,
        ref MemoryBuffer? stagingBuffer, ref MemoryBuffer? gpuBuffer) where TData : unmanaged
    {
        Span<uint> queueIndices = [vulkanEngine.GraphicQueue.familyIndex];
        var size = (ulong)(data.Length * (uint)Unsafe.SizeOf<TData>());
        var alignedSize = (uint)MathHelper.CeilPower2((int)size);

        if (stagingBuffer is null || stagingBuffer.Size < size)
        {
            stagingBuffer?.Dispose();

            stagingBuffer = memoryManager.CreateBuffer(BufferUsageFlags.TransferSrcBit,
                alignedSize, queueIndices,
                MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit, true);
        }

        if (gpuBuffer is null || gpuBuffer.Size < size)
        {
            gpuBuffer?.Dispose();

            gpuBuffer = memoryManager.CreateBuffer(BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
                alignedSize, queueIndices,
                MemoryPropertyFlags.DeviceLocalBit, false);
        }

        var stagingSpan = stagingBuffer.MapAs<TData>();
        data.AsSpan().CopyTo(stagingSpan);
        stagingBuffer.Unmap();

        commandBuffer.CopyBuffer(stagingBuffer, gpuBuffer);
    }

    public override void Dispose()
    {
        _stagingIndexBuffer?.Dispose();
        _stagingNodeBuffer?.Dispose();
        _stagingIndexBuffer = null;
        _stagingNodeBuffer = null;

        _voxelBufferAccessor = null;
        _worldDataAccessor = null;
    }

    public override Identification Identification => RenderInputModuleIDs.World;
}