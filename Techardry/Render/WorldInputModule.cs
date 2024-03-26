using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
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
using Int3 = Techardry.Utils.Int3;
using MathHelper = MintyCore.Utils.Maths.MathHelper;

namespace Techardry.Render;

[RegisterInputDataModule("world")]
[UsedImplicitly]
public class WorldInputModule(IMemoryManager memoryManager, IVulkanEngine vulkanEngine) : InputModule
{
    private Func<VoxelIntermediateData>? _voxelBufferAccessor;
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
        if (_voxelBufferAccessor is null || _worldDataAccessor is null)
        {
            return;
        }

        var voxelBuffer = _voxelBufferAccessor();
        var worldData = _worldDataAccessor();
        
        if(!voxelBuffer.Buffers.Any())
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

        var nodes = bvh.Nodes;
        var indices = bvh.TreeIndices;

        ApplyBufferData(commandBuffer, nodes, ref _stagingNodeBuffer, ref worldData.BvhNodeBuffer);
        ApplyBufferData(commandBuffer, indices, ref _stagingIndexBuffer, ref worldData.BvhIndexBuffer);

        UpdateDescriptorSets(worldData, octrees);
    }

    private unsafe void UpdateDescriptorSets(WorldIntermediateData worldData,
        KeyValuePair<Int3, MemoryBuffer>[] octrees)
    {
        if (worldData.WorldDataDescriptorPool.Handle == default || worldData.DescriptorSetCapacity < octrees.Length)
        {
            RecreateDescriptor(worldData,
                octrees.Length);
        }

        var writeArray = ArrayPool<WriteDescriptorSet>.Shared.Rent((int)(worldData.DescriptorSetCapacity + 2));
        var writeGcHandle = GCHandle.Alloc(writeArray, GCHandleType.Pinned);
        var writeSpan = writeArray.AsSpan(0, (int)worldData.DescriptorSetCapacity + 2);

        var descriptorBufferArray = ArrayPool<DescriptorBufferInfo>.Shared.Rent((int)worldData.DescriptorSetCapacity);
        var descriptorBufferGcHandle = GCHandle.Alloc(descriptorBufferArray, GCHandleType.Pinned);
        var descriptorBufferSpan = descriptorBufferArray.AsSpan(0, (int)worldData.DescriptorSetCapacity);

        var descriptorIndex = 0;

        //bvh node buffer
        var nodeBufferInfo = new DescriptorBufferInfo
        {
            Buffer = worldData.BvhNodeBuffer!.Buffer,
            Range = worldData.BvhNodeBuffer.Size
        };
        writeSpan[descriptorIndex] = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = worldData.WorldDataDescriptorSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &nodeBufferInfo
        };
        descriptorIndex++;

        //bvh index buffer
        var indexBufferInfo = new DescriptorBufferInfo
        {
            Buffer = worldData.BvhIndexBuffer!.Buffer,
            Range = worldData.BvhIndexBuffer.Size
        };
        writeSpan[descriptorIndex] = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = worldData.WorldDataDescriptorSet,
            DstBinding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            PBufferInfo = &indexBufferInfo
        };
        descriptorIndex++;

        for (uint currentOctree = 0; currentOctree < octrees.Length; currentOctree++, descriptorIndex++)
        {
            descriptorBufferSpan[(int)currentOctree] = new()
            {
                Buffer = octrees[currentOctree].Value.Buffer,
                Range = octrees[currentOctree].Value.Size
            };
            writeSpan[descriptorIndex] = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = worldData.WorldDataDescriptorSet,
                DstBinding = 2,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = (DescriptorBufferInfo*)Unsafe.AsPointer(ref descriptorBufferSpan[(int)currentOctree]),
                DstArrayElement = currentOctree
            };
        }
        
        vulkanEngine.Vk.UpdateDescriptorSets(vulkanEngine.Device, writeSpan[..descriptorIndex], ReadOnlySpan<CopyDescriptorSet>.Empty);

        descriptorBufferGcHandle.Free();
        ArrayPool<DescriptorBufferInfo>.Shared.Return(descriptorBufferArray);

        writeGcHandle.Free();
        ArrayPool<WriteDescriptorSet>.Shared.Return(writeArray);
    }

    const int AdditionalDescriptorSets = 2;
    private unsafe void RecreateDescriptor(WorldIntermediateData worldData, int octreesLength)
    {
        // We need to allocate 2 additional sets for the bvh buffer (node and index)
        var alignedOctreesLength = (uint)MathHelper.CeilPower2(octreesLength);
        worldData.DescriptorSetCapacity = alignedOctreesLength;

        if (worldData.WorldDataDescriptorPool.Handle != default)
        {
            vulkanEngine.Vk.DestroyDescriptorPool(vulkanEngine.Device, worldData.WorldDataDescriptorPool, null);
        }

        var poolSize = new DescriptorPoolSize()
        {
            Type = DescriptorType.StorageBuffer,
            DescriptorCount = alignedOctreesLength + AdditionalDescriptorSets
        };

        var createInfo = new DescriptorPoolCreateInfo()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 1,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize
        };

        VulkanUtils.Assert(vulkanEngine.Vk.CreateDescriptorPool(vulkanEngine.Device, createInfo, null,
            out worldData.WorldDataDescriptorPool));

        var variableAllocateInfo = new DescriptorSetVariableDescriptorCountAllocateInfo()
        {
            SType = StructureType.DescriptorSetVariableDescriptorCountAllocateInfo,
            DescriptorSetCount = 1,
            PDescriptorCounts = &alignedOctreesLength
        };

        var layout = RenderObjects.RenderDescriptorLayout;
        var allocateInfo = new DescriptorSetAllocateInfo()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            PNext = &variableAllocateInfo,
            DescriptorPool = worldData.WorldDataDescriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout
        };

        VulkanUtils.Assert(vulkanEngine.Vk.AllocateDescriptorSets(vulkanEngine.Device, allocateInfo,
            out worldData.WorldDataDescriptorSet));
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