using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepuUtilities;
using DotNext.Threading;
using JetBrains.Annotations;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.Voxels;
using Techardry.World;
using Int3 = Techardry.Utils.Int3;

namespace Techardry.Render;

public class RenderResourcesWorker
{
    private readonly ConcurrentUniqueQueue<Int3> _chunkAddQueue = new();
    private readonly ConcurrentUniqueQueue<Int3> _chunkUpdateQueue = new();
    private readonly ConcurrentUniqueQueue<Int3> _chunkRemoveQueue = new();

    private readonly RenderData[] _frameRenderDat;

    private readonly Dictionary<Int3, int> _chunkPositionsToIndex = new();
    private readonly List<Int3> _chunkPositions = new();
    private readonly List<BufferWrapper> _chunkBuffers = new();
    private readonly List<BoundingBox> _chunkBoundingBoxes = new();

    private BufferWrapper? _masterBvhBuffer;
    private BufferWrapper? _masterBvhIndexBuffer;

    private readonly object _lock = new();
    private Thread? _workerThread;

    private volatile bool _isRunning = true;

    private readonly TechardryWorld _world;

    public unsafe RenderResourcesWorker(TechardryWorld world)
    {
        _world = world;
        _frameRenderDat = new RenderData[VulkanEngine.SwapchainImageCount];

        for (var index = 0; index < _frameRenderDat.Length; index++)
        {
            _frameRenderDat[index] = new();
        }
    }

    internal void Start()
    {
        _world.ChunkManager.ChunkAdded += ChunkManager_ChunkAdded;
        _world.ChunkManager.ChunkRemoved += ChunkManager_ChunkRemoved;
        _world.ChunkManager.ChunkUpdated += ChunkManager_ChunkUpdated;

        foreach (var chunk in _world.ChunkManager.GetLoadedChunks())
        {
            _chunkAddQueue.TryEnqueue(chunk);
        }

        _isRunning = true;
        _workerThread = new Thread(Worker);
        _workerThread.Start();
    }

    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    internal void Stop()
    {
        _world.ChunkManager.ChunkAdded -= ChunkManager_ChunkAdded;
        _world.ChunkManager.ChunkRemoved -= ChunkManager_ChunkRemoved;
        _world.ChunkManager.ChunkUpdated -= ChunkManager_ChunkUpdated;

        _isRunning = false;
        _chunkUpdateQueue.Clear();
        _chunkRemoveQueue.Clear();
        _chunkAddQueue.Clear();
        _workerThread?.Join();
        
        _masterBvhBuffer?.RemoveUse();
        _masterBvhIndexBuffer?.RemoveUse();
        
        foreach (var buffer in _chunkBuffers)
        {
            buffer.RemoveUse();
        }

        foreach (var renderData in _frameRenderDat)
        {
            foreach (var renderDataUsedBuffer in renderData.UsedBuffers)
            {
                renderDataUsedBuffer.RemoveUse();
            }
            
            renderData.MasterBvhBuffer?.RemoveUse();
            renderData.MasterBvhIndexBuffer?.RemoveUse();
            
            DescriptorSetHandler.FreeDescriptorSet(renderData.MasterBvhDescriptor);
            DescriptorSetHandler.FreeDescriptorSet(renderData.OctreeDescriptor);
        }
    }

    private unsafe void Worker()
    {
        while (_isRunning)
        {
            lock (_lock)
            {
                bool structureChanged = false;

                while (_chunkAddQueue.TryDequeue(out var position))
                {
                    structureChanged = true;
                    UpdateChunkData(position);
                }

                while (_chunkUpdateQueue.TryDequeue(out var position))
                {
                    UpdateChunkData(position);
                }

                while (_chunkRemoveQueue.TryDequeue(out var position))
                {
                    structureChanged = true;
                    DestroyChunkData(position);
                }

                if (structureChanged)
                {
                    DestroyMasterBvh();
                    UpdateMasterBvh();
                }
            }
        }
    }

    private void DestroyMasterBvh()
    {
        _masterBvhBuffer?.RemoveUse();
        _masterBvhBuffer = null;

        _masterBvhIndexBuffer?.RemoveUse();
        _masterBvhIndexBuffer = null;
    }

    private unsafe void UpdateMasterBvh()
    {
        if (_chunkBoundingBoxes.Count == 0)
        {
            return;
        }

        var masterBvhTree = new MasterBvhTree(_chunkBoundingBoxes);

        var nodes = masterBvhTree.Nodes;
        var indices = masterBvhTree.TreeIndices;

        var queueIndices = (stackalloc uint[]
        {
            VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value
        });

        var nodeStagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferSrcBit,
            (ulong)(Marshal.SizeOf<MasterBvhTree.Node>() * nodes.Length),
            SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, true, true);
        var stagingNodes =
            new Span<MasterBvhTree.Node>(MemoryManager.Map(nodeStagingBuffer.Memory).ToPointer(), nodes.Length);
        nodes.AsSpan().CopyTo(stagingNodes);

        var indicesStagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferSrcBit,
            (ulong)(sizeof(int) * indices.Length), SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, true, true);
        var stagingIndices =
            new Span<int>(MemoryManager.Map(indicesStagingBuffer.Memory).ToPointer(), indices.Length);
        indices.AsSpan().CopyTo(stagingIndices);

        var nodeBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
            (ulong)(Marshal.SizeOf<MasterBvhTree.Node>() * nodes.Length),
            SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.DeviceLocalBit, false);

        var indexBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
            (ulong)(sizeof(int) * indices.Length), SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.DeviceLocalBit, false);

        var copyNodeBufferRegion = new BufferCopy
        {
            SrcOffset = 0,
            DstOffset = 0,
            Size = nodeBuffer.Size
        };

        var copyIndexBufferRegion = new BufferCopy
        {
            SrcOffset = 0,
            DstOffset = 0,
            Size = indexBuffer.Size
        };

        var copyCommandBuffer = VulkanEngine.GetSingleTimeCommandBuffer();
        VulkanEngine.Vk.CmdCopyBuffer(
            copyCommandBuffer, nodeStagingBuffer.Buffer, nodeBuffer.Buffer,
            1, copyNodeBufferRegion);

        VulkanEngine.Vk.CmdCopyBuffer(
            copyCommandBuffer, indicesStagingBuffer.Buffer, indexBuffer.Buffer,
            1, copyIndexBufferRegion);

        VulkanEngine.ExecuteSingleTimeCommandBuffer(copyCommandBuffer);

        _masterBvhBuffer = new(nodeBuffer);
        _masterBvhIndexBuffer = new(indexBuffer);

        nodeStagingBuffer.Dispose();
        indicesStagingBuffer.Dispose();
    }

    private void UpdateChunkData(Int3 position)
    {
        Logger.AssertAndThrow(_world.ChunkManager.TryGetChunk(position, out var chunk), "Chunk not found",
            "RenderResourceWorker");

        uint bufferSize;
        using (chunk.Octree.AcquireReadLock())
            CalculateOctreeBufferSize(chunk.Octree, out bufferSize);

        bufferSize = (uint)MintyCore.Utils.Maths.MathHelper.CeilPower2(checked((int)bufferSize));

        ulong oldBufferSize = 0;

        if (_chunkPositionsToIndex.TryGetValue(position, out int index))
        {
            oldBufferSize = _chunkBuffers[index].Buffer.Size;
        }

        BufferWrapper bufferWrapper;
        if (bufferSize > oldBufferSize || bufferSize < oldBufferSize * 2)
        {
            DestroyChunkData(position);
            CreateChunkDataBuffer(position, bufferSize, out bufferWrapper);
        }
        else
        {
            bufferWrapper = _chunkBuffers[index];
        }

        using (chunk.Octree.AcquireReadLock())
            FillOctreeBuffer(chunk.Position, chunk.Octree, bufferWrapper.Buffer);
    }

    private unsafe void FillOctreeBuffer(Int3 position, VoxelOctree octree, MemoryBuffer buffer)
    {
        Span<uint> queue = stackalloc uint[] { VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value };
        var headerSize = (uint)Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint)Marshal.SizeOf<VoxelOctree.Node>();

        var stagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferSrcBit,
            buffer.Size,
            SharingMode.Exclusive,
            queue,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            true, true
        );

        var data = MemoryManager.Map(stagingBuffer.Memory);

        Unsafe.AsRef<OctreeHeader>(data.ToPointer()) = new OctreeHeader
        {
            NodeCount = octree.NodeCount,
            treeMin = position
        };

        var srcNodes = octree.Nodes.AsSpan(0, (int)octree.NodeCount);
        var dstNodes = new Span<VoxelOctree.Node>((void*)(data + headerSize), srcNodes.Length);
        srcNodes.CopyTo(dstNodes);

        var srcData = octree.Data.renderData.AsSpan(0, (int)octree.DataCount);
        var dstData =
            new Span<VoxelRenderData>((void*)(data + headerSize + nodeSize * octree.NodeCount), srcData.Length);
        srcData.CopyTo(dstData);

        MemoryManager.UnMap(stagingBuffer.Memory);

        var copy = new BufferCopy
        {
            Size = buffer.Size,
            SrcOffset = 0,
            DstOffset = 0
        };

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();
        VulkanEngine.Vk.CmdCopyBuffer(cb, stagingBuffer.Buffer, buffer.Buffer, 1, copy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

        stagingBuffer.Dispose();
    }

    private static void CalculateOctreeBufferSize(VoxelOctree octree, out uint bufferSize)
    {
        var headerSize = (uint)Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint)Marshal.SizeOf<VoxelOctree.Node>();
        var dataSize = (uint)Marshal.SizeOf<VoxelRenderData>();

        bufferSize = headerSize + nodeSize * octree.NodeCount + dataSize * octree.DataCount;
    }

    private void CreateChunkDataBuffer(Int3 position, uint bufferSize, out BufferWrapper bufferWrapper)
    {
        Span<uint> queue = stackalloc uint[] { VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value };

        var buffer = MemoryBuffer.Create(
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            bufferSize,
            SharingMode.Exclusive,
            queue,
            MemoryPropertyFlags.DeviceLocalBit,
            false
        );

        bufferWrapper = new BufferWrapper(buffer);

        _chunkPositionsToIndex.Add(position, _chunkPositions.Count);
        _chunkPositions.Add(position);

        var bMin = new Vector3(position.X, position.Y, position.Z) * Chunk.Size;
        _chunkBoundingBoxes.Add(new BoundingBox(bMin, bMin + new Vector3(Chunk.Size)));

        _chunkBuffers.Add(bufferWrapper);
    }

    private void DestroyChunkData(Int3 position)
    {
        if (!_chunkPositionsToIndex.Remove(position, out var index)) return;

        _chunkPositions.RemoveAt(index);
        for (var i = 0; i < _chunkPositions.Count; i++)
        {
            var chunkPosition = _chunkPositions[i];
            _chunkPositionsToIndex[chunkPosition] = i;
        }

        _chunkBoundingBoxes.RemoveAt(index);

        var buffer = _chunkBuffers[index];
        buffer.RemoveUse();
        _chunkBuffers.RemoveAt(index);
    }

    internal bool PrepareRenderResources(out DescriptorSet masterBvhDescriptor, out DescriptorSet octreeDescriptors)
    {
        lock (_lock)
        {
            var currentRenderData = _frameRenderDat[VulkanEngine.ImageIndex];

            masterBvhDescriptor = octreeDescriptors = default;

            DeleteOldRenderData(currentRenderData);

            if (!CreateOctreeDescriptorSet(out octreeDescriptors)) return false;
            currentRenderData.OctreeDescriptor = octreeDescriptors;
            currentRenderData.UsedBuffers.AddRange(_chunkBuffers);
            foreach (var bufferWrapper in currentRenderData.UsedBuffers)
            {
                bufferWrapper.AddUse();
            }

            if (!CreateMasterBvhDescriptorSet(out masterBvhDescriptor)) return false;

            currentRenderData.MasterBvhDescriptor = masterBvhDescriptor;

            _masterBvhBuffer?.AddUse();
            _masterBvhIndexBuffer?.AddUse();
            currentRenderData.MasterBvhBuffer = _masterBvhBuffer;
            currentRenderData.MasterBvhIndexBuffer = _masterBvhIndexBuffer;

            return true;
        }
    }

    private unsafe bool CreateMasterBvhDescriptorSet(out DescriptorSet masterBvhDescriptor)
    {
        if (_masterBvhBuffer is null || _masterBvhIndexBuffer is null)
        {
            masterBvhDescriptor = default;
            return false;
        }

        masterBvhDescriptor = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.MasterBvh);

        var bvhBufferInfo = new DescriptorBufferInfo
        {
            Buffer = _masterBvhBuffer.Buffer.Buffer,
            Offset = 0,
            Range = _masterBvhBuffer.Buffer.Size
        };

        var indexBufferInfo = new DescriptorBufferInfo
        {
            Buffer = _masterBvhIndexBuffer.Buffer.Buffer,
            Offset = 0,
            Range = _masterBvhIndexBuffer.Buffer.Size
        };

        var writeDescriptors = (stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = masterBvhDescriptor,
                PBufferInfo = &bvhBufferInfo
            },
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 1,
                DstSet = masterBvhDescriptor,
                PBufferInfo = &indexBufferInfo
            }
        });

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, writeDescriptors, 0, null);

        return true;
    }

    private unsafe bool CreateOctreeDescriptorSet(out DescriptorSet octreeDescriptor)
    {
        if (_chunkBuffers.Count == 0)
        {
            octreeDescriptor = default;
            return false;
        }

        octreeDescriptor =
            DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctree, _chunkBuffers.Count);


        DescriptorBufferInfo[] bufferInfos = new DescriptorBufferInfo[_chunkBuffers.Count];
        WriteDescriptorSet[] writeDescriptorSets = new WriteDescriptorSet[_chunkBuffers.Count];
        Span<DescriptorBufferInfo> bufferInfosSpan = bufferInfos;
        Span<WriteDescriptorSet> writeDescriptorSetsSpan = writeDescriptorSets;
        GCHandle bufferInfoHandle = GCHandle.Alloc(bufferInfos, GCHandleType.Pinned);
        GCHandle writeDescriptorSetHandle = GCHandle.Alloc(writeDescriptorSets, GCHandleType.Pinned);


        int currentChunk = 0;
        for (var i = 0; i < _chunkBuffers.Count; i++)
        {
            var chunk = _chunkBuffers[i];
            var buffer = chunk.Buffer;

            bufferInfosSpan[currentChunk] = new DescriptorBufferInfo
            {
                Buffer = buffer.Buffer,
                Offset = 0,
                Range = buffer.Size
            };

            writeDescriptorSetsSpan[currentChunk] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = octreeDescriptor,
                DstArrayElement = (uint)currentChunk,
                PBufferInfo = (DescriptorBufferInfo*)Unsafe.AsPointer(ref bufferInfos[currentChunk])
            };

            currentChunk++;
        }

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, writeDescriptorSets, 0, null);

        bufferInfoHandle.Free();
        writeDescriptorSetHandle.Free();

        return true;
    }

    private static void DeleteOldRenderData(RenderData currentRenderData)
    {
        if (currentRenderData.OctreeDescriptor.Handle != 0)
            DescriptorSetHandler.FreeDescriptorSet(currentRenderData.OctreeDescriptor);

        if (currentRenderData.MasterBvhDescriptor.Handle != 0)
            DescriptorSetHandler.FreeDescriptorSet(currentRenderData.MasterBvhDescriptor);

        foreach (var bufferWrapper in currentRenderData.UsedBuffers)
        {
            bufferWrapper.RemoveUse();
        }

        currentRenderData.UsedBuffers.Clear();

        currentRenderData.MasterBvhBuffer?.RemoveUse();
        currentRenderData.MasterBvhIndexBuffer?.RemoveUse();
    }

    private void ChunkManager_ChunkAdded(Int3 position)
    {
        _chunkAddQueue.TryEnqueue(position);
    }

    private void ChunkManager_ChunkUpdated(Int3 position)
    {
        _chunkUpdateQueue.TryEnqueue(position);
    }

    private void ChunkManager_ChunkRemoved(Int3 position)
    {
        _chunkRemoveQueue.TryEnqueue(position);
        _chunkAddQueue.TryRemove(position);
        _chunkUpdateQueue.TryRemove(position);
    }

    private class RenderData
    {
        public readonly List<BufferWrapper> UsedBuffers = new();
        public BufferWrapper? MasterBvhBuffer;
        public BufferWrapper? MasterBvhIndexBuffer;

        public DescriptorSet OctreeDescriptor;
        public DescriptorSet MasterBvhDescriptor;
    }

    private class BufferWrapper
    {
        private int _useCount;
        public readonly MemoryBuffer Buffer;

        public BufferWrapper(MemoryBuffer buffer)
        {
            Buffer = buffer;
            _useCount = 1;
        }

        public void AddUse()
        {
            Interlocked.Increment(ref _useCount);
        }

        public void RemoveUse()
        {
            if (Interlocked.Decrement(ref _useCount) == 0)
            {
                Buffer.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct OctreeHeader
    {
        [UsedImplicitly] [FieldOffset(0)] public uint NodeCount;
        [UsedImplicitly] [FieldOffset(4)] public Int3 treeMin;
    }
}