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
using Int3 = Techardry.Utils.Int3;
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

    private MemoryBuffer? _stagingWorldBuffer;
    private MemoryBuffer? _stagingWorldHeaderBuffer;

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

        if (voxelBuffer.Buffers.Length == 0 || worldData.LastVoxelIntermediateVersion == voxelBuffer.Version) return;


        var min = Int3.MaxValue;
        var max = Int3.MinValue;

        foreach (var (position, _) in voxelBuffer.Buffers)
        {
            min = Int3.Min(min, position);
            max = Int3.Max(max, position);
        }

        using var worldGrid = new WorldGrid(min, max);

        foreach (var (position, address) in voxelBuffer.Buffers)
        {
            worldGrid.InsertChunk(position, address);
        }

        ApplyBufferData(commandBuffer, worldGrid.Cells, ref _stagingWorldBuffer, ref worldData.WorldGridBuffer,
            BufferUsageFlags.StorageBufferBit);

        ApplyBufferData(commandBuffer, [worldGrid.Header], ref _stagingWorldHeaderBuffer,
            ref worldData.WorldGridHeaderBuffer, BufferUsageFlags.UniformBufferBit);

        UpdateDescriptorSets(worldData);

        worldData.LastVoxelIntermediateVersion = voxelBuffer.Version;
    }

    private unsafe void UpdateDescriptorSets(WorldIntermediateData worldData)
    {
        if (worldData.WorldDataDescriptorSet.Handle == default)
        {
            worldData.WorldDataDescriptorSet = descriptorSetManager.AllocateDescriptorSet(DescriptorSetIDs.Render);
        }

        var worldGridHeaderBufferInfo = new DescriptorBufferInfo
        {
            Buffer = worldData.WorldGridHeaderBuffer!.Buffer,
            Range = worldData.WorldGridHeaderBuffer.Size
        };
        var worldGridBufferInfo = new DescriptorBufferInfo
        {
            Buffer = worldData.WorldGridBuffer!.Buffer,
            Range = worldData.WorldGridBuffer.Size
        };


        ReadOnlySpan<WriteDescriptorSet> writes =
        [
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = worldData.WorldDataDescriptorSet,
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &worldGridHeaderBufferInfo
            },
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = worldData.WorldDataDescriptorSet,
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &worldGridBufferInfo
            }
        ];

        vulkanEngine.Vk.UpdateDescriptorSets(vulkanEngine.Device, writes, ReadOnlySpan<CopyDescriptorSet>.Empty);
    }

    private void ApplyBufferData<TData>(ManagedCommandBuffer commandBuffer, Span<TData> data,
        ref MemoryBuffer? stagingBuffer, ref MemoryBuffer? gpuBuffer, BufferUsageFlags bufferKind)
        where TData : unmanaged
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

            gpuBuffer = memoryManager.CreateBuffer(BufferUsageFlags.TransferDstBit | bufferKind,
                alignedSize, queueIndices,
                MemoryPropertyFlags.DeviceLocalBit, false);
        }

        var stagingSpan = stagingBuffer.MapAs<TData>();
        data.CopyTo(stagingSpan);
        stagingBuffer.Unmap();

        commandBuffer.CopyBuffer(stagingBuffer, gpuBuffer);
    }

    public override void Dispose()
    {
        _stagingWorldBuffer?.Dispose();
        _stagingWorldBuffer = null;
        
        _stagingWorldHeaderBuffer?.Dispose();
        _stagingWorldHeaderBuffer = null;

        _voxelBufferAccessor = null;
        _worldDataAccessor = null;
    }

    public override Identification Identification => RenderInputModuleIDs.World;
}