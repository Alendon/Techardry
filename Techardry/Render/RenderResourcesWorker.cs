using System.Collections.Concurrent;
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
    private readonly ConcurrentUniqueQueue<Int3> _chunkRemoveQueue = new();
    private readonly ConcurrentUniqueQueue<Int3> _chunkUpdateQueue = new();

    private readonly RenderData[] _frameRenderDat;

    /// <summary>
    /// The current render data to be used in the next frame
    /// </summary>
    /// <remarks>Always lock the access through <see cref="_lock"/></remarks>
    private RenderData _currentRenderData = new()
    {
        UsedBuffers = new List<BufferWrapper>()
    };

    private readonly Dictionary<Int3, int> _chunkPositionsToIndex = new();
    private readonly List<Int3> _chunkPositions = new();
    private readonly List<BufferWrapper> _chunkBuffers = new();
    private readonly List<BoundingBox> _chunkBoundingBoxes = new();

    private readonly object _lock = new();
    private Thread? _workerThread;

    private volatile bool _isRunning = true;

    private readonly TechardryWorld _world;

    public RenderResourcesWorker(TechardryWorld world)
    {
        _world = world;
        _frameRenderDat = new RenderData[VulkanEngine.SwapchainImageCount];

        for (var index = 0; index < _frameRenderDat.Length; index++)
        {
            _frameRenderDat[index] = new()
            {
                UsedBuffers = new List<BufferWrapper>()
            };
        }
    }

    internal void Start()
    {
        _world.ChunkManager.ChunkAdded += ChunkManager_ChunkAdded;
        _world.ChunkManager.ChunkRemoved += ChunkManager_ChunkRemoved;
        _world.ChunkManager.ChunkUpdated += ChunkManager_ChunkUpdated;

        foreach (var chunk in _world.ChunkManager.GetLoadedChunks())
        {
            _chunkUpdateQueue.TryEnqueue(chunk);
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

        _chunkRemoveQueue.Clear();
        _workerThread?.Join();

        foreach (var buffer in _chunkBuffers)
        {
            buffer.RemoveUse();
        }

        foreach (var renderData in _frameRenderDat)
        {
            renderData.RemoveUse();
        }
    }

    private void Worker()
    {
        MemoryBuffer[] stagingBuffers = new MemoryBuffer[16];

        while (_isRunning)
        {
            bool structureChanged = false;

            //The returned keys are a copy of the actual keys, so we can safely iterate over them
            //Any newly added chunks after this will be handled in the next iteration. Updates may be handled in the same iteration
            var cb = VulkanEngine.GetSingleTimeCommandBuffer();

            var stagingBufferSpan = stagingBuffers.AsSpan();

            int i = 0;
            while(_chunkUpdateQueue.TryDequeue(out var position))
            {
                if (!UpdateChunkData(position, cb, out var memoryBuffer, out var stagingBuffer))
                {
                    _chunkRemoveQueue.TryEnqueue(position);
                    continue;
                }

                if (i >= stagingBuffers.Length)
                {
                    Array.Resize(ref stagingBuffers, stagingBuffers.Length * 2);
                    stagingBufferSpan = stagingBuffers.AsSpan();
                }

                stagingBufferSpan[i] = stagingBuffer;
                i++;

                structureChanged = true;

                if (_chunkPositionsToIndex.TryGetValue(position, out var index))
                {
                    _chunkBuffers[index].RemoveUse();
                    _chunkBuffers[index] = new BufferWrapper(memoryBuffer);
                    continue;
                }

                index = _chunkPositions.Count;
                _chunkPositionsToIndex.Add(position, index);
                _chunkPositions.Add(position);
                _chunkBuffers.Add(new BufferWrapper(memoryBuffer));

                var bMin = new Vector3(position.X, position.Y, position.Z) * Chunk.Size;
                _chunkBoundingBoxes.Add(new BoundingBox(bMin, bMin + new Vector3(Chunk.Size)));
            }

            VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

            stagingBufferSpan = stagingBufferSpan[..i];

            DisposeBuffers(stagingBufferSpan);

            ValidateChunkPositions();

            while(_chunkRemoveQueue.TryDequeue(out var position))
            {
                DestroyChunkData(position);

                structureChanged = true;
            }


            if (!structureChanged) continue;

            if (!UpdateMasterBvh(out var masterBvhBuffer, out var bvhIndexBuffer))
            {
                //Clear resources as no chunks exists atm
                lock (_lock)
                {
                    //the current render data is already empty
                    if (_currentRenderData.UsedBuffers.Count == 0) continue;

                    RenderData empty = new RenderData
                    {
                        UsedBuffers = new List<BufferWrapper>()
                    };

                    //maybe a bit unconventional, but this just swaps the current render data with the empty one
                    (_currentRenderData, empty) = (empty, _currentRenderData);

                    //empty now points to the old render data so we can remove the use
                    empty.RemoveUse();
                }

                continue;
            }

            CreateDescriptorSets(masterBvhBuffer, bvhIndexBuffer, out DescriptorSet octreeDescriptorSet,
                out DescriptorSet masterBvhDescriptorSet);

            var renderData = new RenderData()
            {
                UsedBuffers = new List<BufferWrapper>(_chunkBuffers),
                OctreeDescriptor = new DescriptorWrapper(octreeDescriptorSet),
                MasterBvhDescriptor = new DescriptorWrapper(masterBvhDescriptorSet),
                MasterBvhBuffer = new BufferWrapper(masterBvhBuffer),
                MasterBvhIndexBuffer = new BufferWrapper(bvhIndexBuffer)
            };

            renderData.AddUse();

            lock (_lock)
            {
                var oldRenderData = _currentRenderData;
                _currentRenderData = renderData;
                oldRenderData.RemoveUse();
            }
        }
    }


    private unsafe bool UpdateMasterBvh(out MemoryBuffer nodeBuffer, out MemoryBuffer indexBuffer)
    {
        if (_chunkBoundingBoxes.Count == 0)
        {
            nodeBuffer = default;
            indexBuffer = default;
            return false;
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

        nodeBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
            (ulong)(Marshal.SizeOf<MasterBvhTree.Node>() * nodes.Length),
            SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.DeviceLocalBit, false);

        indexBuffer = MemoryBuffer.Create(
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

        nodeStagingBuffer.Dispose();
        indicesStagingBuffer.Dispose();

        return true;
    }

    private bool UpdateChunkData(Int3 position, CommandBuffer commandBuffer, out MemoryBuffer buffer,
        out MemoryBuffer stagingBuffer)
    {
        Logger.AssertAndThrow(_world.ChunkManager.TryGetChunk(position, out var chunk), "Chunk not found",
            "RenderResourceWorker");


        uint bufferSize;
        using (chunk.Octree.AcquireReadLock())
        {
            //the chunk is empty, so we dont want to create render resources as this would be a waste of memory and compute time
            if (chunk.Octree.DataCount == 0)
            {
                //add the current chunk to the remove queue. This makes sure that the chunk is no longer present
                //if it wasn't added in the first place it will be ignored
                _chunkRemoveQueue.TryEnqueue(position);

                buffer = default;
                stagingBuffer = default;
                return false;
            }

            CalculateOctreeBufferSize(chunk.Octree, out bufferSize);
        }

        bufferSize = (uint)MintyCore.Utils.Maths.MathHelper.CeilPower2(checked((int)bufferSize));

        buffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
            bufferSize,
            SharingMode.Exclusive,
            new[] { VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value },
            MemoryPropertyFlags.DeviceLocalBit,
            false
        );

        using (chunk.Octree.AcquireReadLock())
            FillOctreeBuffer(chunk.Position, chunk.Octree, buffer, out stagingBuffer);

        BufferCopy copy = new()
        {
            Size = buffer.Size,
            DstOffset = 0,
            SrcOffset = 0
        };
        VulkanEngine.Vk.CmdCopyBuffer(commandBuffer, stagingBuffer.Buffer, buffer.Buffer, 1, copy);

        return true;
    }

    private unsafe void FillOctreeBuffer(Int3 position, VoxelOctree octree, MemoryBuffer buffer,
        out MemoryBuffer stagingBuffer)
    {
        Span<uint> queue = stackalloc uint[] { VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value };
        var headerSize = (uint)Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint)Marshal.SizeOf<VoxelOctree.Node>();

        stagingBuffer = MemoryBuffer.Create(
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
    }

    private static void CalculateOctreeBufferSize(VoxelOctree octree, out uint bufferSize)
    {
        var headerSize = (uint)Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint)Marshal.SizeOf<VoxelOctree.Node>();
        var dataSize = (uint)Marshal.SizeOf<VoxelRenderData>();

        bufferSize = headerSize + nodeSize * octree.NodeCount + dataSize * octree.DataCount;
    }

    private void DestroyChunkData(Int3 position)
    {
        if (!_chunkPositionsToIndex.Remove(position, out var index))
        {
            //this may happen if the chunk was added empty
            //in this case we can just ignore it
            //The reason for this is to not need locking to check if a chunk is present if a empty one gets added
            return;
        }

        _chunkPositions.RemoveAt(index);
        _chunkBoundingBoxes.RemoveAt(index);

        //could be optimized but works for now
        for (int i = index; i < _chunkPositions.Count; i++)
        {
            _chunkPositionsToIndex[_chunkPositions[i]] = i;
        }

        var buffer = _chunkBuffers[index];
        buffer.RemoveUse();
        _chunkBuffers.RemoveAt(index);


        ValidateChunkPositions();
    }

    private void ValidateChunkPositions()
    {
        foreach (var (pos, index) in _chunkPositionsToIndex)
        {
            if (_chunkPositions.Count <= index || _chunkPositions[index] != pos)
                throw new Exception("Chunk positions are not valid");
        }

        if (_chunkPositions.Any(pos => !_chunkPositionsToIndex.ContainsKey(pos)))
            throw new Exception("Chunk positions are not valid");
    }

    internal bool TryGetRenderResources(out DescriptorSet masterBvhDescriptor, out DescriptorSet octreeDescriptors)
    {
        _frameRenderDat[VulkanEngine.ImageIndex].RemoveUse();

        RenderData current;
        lock (_lock)
        {
            _currentRenderData.AddUse();
            current = _currentRenderData;
        }

        _frameRenderDat[VulkanEngine.ImageIndex] = current;

        masterBvhDescriptor = default;
        octreeDescriptors = default;

        if (current.OctreeDescriptor is null || current.MasterBvhDescriptor is null) return false;


        masterBvhDescriptor = current.MasterBvhDescriptor.Set;
        octreeDescriptors = current.OctreeDescriptor.Set;
        return true;
    }

    internal void CreateDescriptorSets(MemoryBuffer masterBvhBuffer, MemoryBuffer masterBvhIndexBuffer,
        out DescriptorSet octreeDescriptors, out DescriptorSet masterBvhDescriptor)
    {
        CreateOctreeDescriptorSet(out octreeDescriptors);
        CreateMasterBvhDescriptorSet(masterBvhBuffer, masterBvhIndexBuffer, out masterBvhDescriptor);
    }

    private unsafe void CreateMasterBvhDescriptorSet(MemoryBuffer masterBvhBuffer,
        MemoryBuffer masterBvhIndexBuffer,
        out DescriptorSet masterBvhDescriptor)
    {
        masterBvhDescriptor = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.MasterBvh);

        var bvhBufferInfo = new DescriptorBufferInfo
        {
            Buffer = masterBvhBuffer.Buffer,
            Offset = 0,
            Range = masterBvhBuffer.Size
        };

        var indexBufferInfo = new DescriptorBufferInfo
        {
            Buffer = masterBvhIndexBuffer.Buffer,
            Offset = 0,
            Range = masterBvhIndexBuffer.Size
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
    }

    private unsafe void CreateOctreeDescriptorSet(out DescriptorSet octreeDescriptor)
    {
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
    }

    private void DisposeBuffers(Span<MemoryBuffer> stagingBufferSpan)
    {
        foreach (var staging in stagingBufferSpan)
        {
            staging.Dispose();
        }
    }


    private void ChunkManager_ChunkAdded(Int3 position)
    {
        _chunkUpdateQueue.TryEnqueue(position);
    }

    private void ChunkManager_ChunkUpdated(Int3 position)
    {
        _chunkUpdateQueue.TryEnqueue(position);
    }

    private void ChunkManager_ChunkRemoved(Int3 position)
    {
        _chunkRemoveQueue.TryEnqueue(position);
    }

    private class RenderData
    {
        public required List<BufferWrapper> UsedBuffers { get; init; }
        public BufferWrapper? MasterBvhBuffer { get; init; }
        public BufferWrapper? MasterBvhIndexBuffer { get; init; }

        public DescriptorWrapper? OctreeDescriptor { get; init; }
        public DescriptorWrapper? MasterBvhDescriptor { get; init; }


        public void AddUse()
        {
            foreach (var buffer in UsedBuffers)
            {
                buffer.AddUse();
            }

            MasterBvhBuffer?.AddUse();
            MasterBvhIndexBuffer?.AddUse();
            OctreeDescriptor?.AddUse();
            MasterBvhDescriptor?.AddUse();
        }

        public void RemoveUse()
        {
            foreach (var buffer in UsedBuffers)
            {
                buffer.RemoveUse();
            }

            MasterBvhBuffer?.RemoveUse();
            MasterBvhIndexBuffer?.RemoveUse();
            OctreeDescriptor?.RemoveUse();
            MasterBvhDescriptor?.RemoveUse();
        }
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

    private class DescriptorWrapper
    {
        private int _useCount;
        public readonly DescriptorSet Set;

        public DescriptorWrapper(DescriptorSet set)
        {
            Set = set;
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
                DescriptorSetHandler.FreeDescriptorSet(Set);
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