using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepuUtilities;
using DotNext.Runtime;
using DotNext.Threading;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Vulkan;
using Techardry.Components.Client;
using Techardry.Identifications;
using Techardry.Render;
using Techardry.Utils;
using Techardry.Voxels;
using Techardry.World;
using DescriptorSetIDs = Techardry.Identifications.DescriptorSetIDs;
using Int3 = Techardry.Utils.Int3;
using MathHelper = MintyCore.Utils.Maths.MathHelper;
using PipelineIDs = Techardry.Identifications.PipelineIDs;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using SystemIDs = Techardry.Identifications.SystemIDs;

namespace Techardry.Systems.Client;

[RegisterSystem("voxel_render")]
[ExecuteInSystemGroup<DualRenderSystemGroup>]
[ExecuteAfter<RenderInstancedSystem>]
public sealed partial class VoxelRender : ARenderSystem
{
    [ComponentQuery] private readonly ComponentQuery<object, (Camera, Position)> _cameraQuery = new();

    private RenderData[] _renderData = Array.Empty<RenderData>();

    class RenderData
    {
        public readonly Dictionary<Int3, MemoryBuffer> ChunkOctreeBuffers = new();
        public readonly Dictionary<Int3, int> ChunkDescriptorSetIndices = new();
        public readonly Dictionary<Int3, uint> ChunkCurrentVersion = new();

        public DescriptorSet OctreeDescriptorSet;

        public DescriptorSet InputAttachmentDescriptorSet;

        public ImageView LastColorImageView;

        public MemoryBuffer CameraDataBuffer;
        public DescriptorSet CameraDataDescriptors;

        public MemoryBuffer MasterBvhBuffer;
        public MemoryBuffer MasterBvhIndexBuffer;
        public DescriptorSet MasterBvhDescriptorSet;

        public readonly HashSet<Int3> UsedChunks = new();
        public long ChunkHash;
    }


    private RenderData CurrentRenderData => _renderData[VulkanEngine.ImageIndex];

    public override void Setup(SystemManager systemManager)
    {
        _cameraQuery.Setup(this);

        _renderData = new RenderData[VulkanEngine.SwapchainImageCount];
        for (var i = 0; i < VulkanEngine.SwapchainImageCount; i++)
        {
            _renderData[i] = new RenderData();
        }

        SetRenderArguments(new RenderPassArguments
        {
            SubpassIndex = 1
        });

        CreateCameraDataBuffers();
        CreateCameraDataDescriptors();
    }

    public override void PreExecuteMainThread()
    {
        var index = VulkanEngine.ImageIndex;
        if (VulkanEngine.SwapchainImageViews[index].Handle != _renderData[index].LastColorImageView.Handle)
        {
            DescriptorSetHandler.FreeDescriptorSet(_renderData[index].InputAttachmentDescriptorSet);
            CreateInputAttachments(index);
        }

        base.PreExecuteMainThread();
    }

    protected override unsafe void Execute()
    {
        if (World is null) return;

        if ((KeyActions.RenderMode & 1) == 0) return;

        var cameraEntity = _cameraQuery.FirstOrDefault(entityWrapper =>
            World.EntityManager.GetEntityOwner(entityWrapper.Entity) == PlayerHandler.LocalPlayerGameId);

        if (cameraEntity.Entity.ArchetypeId == default) return;

        if (!UpdateVoxelData(cameraEntity.GetCamera(), cameraEntity.GetPosition()) ||
            CurrentRenderData.OctreeDescriptorSet.Handle == default ||
            CurrentRenderData.MasterBvhDescriptorSet.Handle == default)
        {
            Logger.WriteLog("Failed to update voxel data", LogImportance.Error, "VoxelRender");
            return;
        }

        var vk = VulkanEngine.Vk;


        var data = MemoryManager.Map(CurrentRenderData.CameraDataBuffer.Memory);

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
        cameraGpuData->Near = cameraData.NearPlane;
        cameraGpuData->Far = cameraData.FarPlane;

        MemoryManager.UnMap(CurrentRenderData.CameraDataBuffer.Memory);

        var pipeline = PipelineHandler.GetPipeline(PipelineIDs.Voxel);
        var pipelineLayout = PipelineHandler.GetPipelineLayout(PipelineIDs.Voxel);

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
            TextureAtlasHandler.TryGetAtlasDescriptorSet(TextureAtlasIDs.BlockTexture,
                out var atlasDescriptorSet), "Failed to get atlas descriptor set", "Techardry/Render");

        Span<DescriptorSet> descriptorSets = stackalloc DescriptorSet[]
        {
            CurrentRenderData.CameraDataDescriptors,
            atlasDescriptorSet,
            CurrentRenderData.InputAttachmentDescriptorSet,
            CurrentRenderData.MasterBvhDescriptorSet,
            CurrentRenderData.OctreeDescriptorSet,
        };


        vk.CmdBindDescriptorSets(CommandBuffer, PipelineBindPoint.Graphics, pipelineLayout, 0,
            (uint) descriptorSets.Length,
            descriptorSets, 0, (uint*) null);

        vk.CmdDraw(CommandBuffer, 6, 1, 0, 0);
    }

    private bool UpdateVoxelData(Camera camera, Position position)
    {
        if (!TryEnterLock(TimeSpan.FromMilliseconds(100))) return false;

        FreeUnusedBuffers();

        CalculateChunkRenderInfo(position.Value, camera.FarPlane, out var renderDistanceLog, out var renderDistance,
            out var centralChunk);
        var chunksToRender = GetChunksToRender(centralChunk, renderDistance).ToArray();

        var allocatedChunks = false;

        foreach (var chunk in chunksToRender)
        {
            UpdateOctree(chunk, out bool bufferAllocated);
            allocatedChunks |= bufferAllocated;
        }

        var chunkPositions = chunksToRender.Select(chunk => chunk.Position).ToArray();
        if (allocatedChunks || ChunkCombinationChanged(chunkPositions))
        {
            UpdateMasterOctree(centralChunk, chunkPositions, renderDistanceLog);
        }

        ReleaseLock();
        return true;
    }

    private void UpdateMasterOctree(Int3 centralChunk, Int3[] chunksToRender, int renderDistanceLog)
    {
        DestroyOctreeDescriptorSet();
        CreateOctreeDescriptorSet(chunksToRender);

        DestroyMasterBvhDescriptorSet();
        DestroyMasterBvhBuffer();
        CreateMasterBvhBuffers(centralChunk, renderDistanceLog);
        CreateMasterBvhDescriptorSet();
    }

    private unsafe bool ChunkCombinationChanged(Int3[] chunkList)
    {
        var newHash = chunkList.Length != 0
            ? Intrinsics.GetHashCode64(Unsafe.AsPointer(ref chunkList[0]),
                (nuint) (chunkList.Length * Marshal.SizeOf<Int3>()), false)
            : 0;
        if (CurrentRenderData.ChunkHash == newHash)
        {
            return false;
        }

        CurrentRenderData.ChunkHash = newHash;
        return true;
    }

    private unsafe void CreateMasterBvhDescriptorSet()
    {
        CurrentRenderData.MasterBvhDescriptorSet =
            DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.MasterBvh);
        
        var bvhBufferInfo = new DescriptorBufferInfo
        {
            Buffer = CurrentRenderData.MasterBvhBuffer.Buffer,
            Offset = 0,
            Range = CurrentRenderData.MasterBvhBuffer.Size
        };
        
        var indexBufferInfo = new DescriptorBufferInfo
        {
            Buffer = CurrentRenderData.MasterBvhIndexBuffer.Buffer,
            Offset = 0,
            Range = CurrentRenderData.MasterBvhIndexBuffer.Size
        };

        var writeDescriptors = (stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 0,
                DstSet = CurrentRenderData.MasterBvhDescriptorSet,
                PBufferInfo = &bvhBufferInfo
            },
            new WriteDescriptorSet()
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DstBinding = 1,
                DstSet = CurrentRenderData.MasterBvhDescriptorSet,
                PBufferInfo = &indexBufferInfo
            }
        });

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, writeDescriptors, 0, null);
    }

    private unsafe void CreateMasterBvhBuffers(Int3 centralChunk, int renderDistanceLog)
    {
        var chunkDiameter = (int) Math.Pow(2, renderDistanceLog);
        var chunkRadius = chunkDiameter / 2;
        var chunkMin = centralChunk - new Int3(chunkRadius, chunkRadius, chunkRadius);
        var chunkMax = centralChunk + new Int3(chunkRadius, chunkRadius, chunkRadius);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        bool ChunkIsInRenderDistance(Int3 chunk)
        {
            return chunk.X >= chunkMin.X && chunk.X <= chunkMax.X &&
                   chunk.Y >= chunkMin.Y && chunk.Y <= chunkMax.Y &&
                   chunk.Z >= chunkMin.Z && chunk.Z <= chunkMax.Z;
        }

        List<BoundingBox> chunkBoundingBoxes = new List<BoundingBox>();

        foreach (var chunkPosition in CurrentRenderData.ChunkDescriptorSetIndices.Keys)
        {
            if (!ChunkIsInRenderDistance(chunkPosition)) continue;

            var chunkMinPosition = new Vector3(chunkPosition.X * Chunk.Size,
                chunkPosition.Y * Chunk.Size, chunkPosition.Z * Chunk.Size);
            var chunkMaxPosition = chunkMinPosition + new Vector3(Chunk.Size);

            chunkBoundingBoxes.Add(new BoundingBox(chunkMinPosition, chunkMaxPosition));
        }

        var masterBvhTree = new MasterBvhTree(chunkBoundingBoxes);

        var nodes = masterBvhTree.Nodes;
        var indices = masterBvhTree.TreeIndices;

        var queueIndices = (stackalloc uint[]
        {
            VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value
        });

        var nodeStagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferSrcBit,
            (ulong) (Marshal.SizeOf<MasterBvhTree.Node>() * nodes.Length),
            SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, true, true);
        var stagingNodes =
            new Span<MasterBvhTree.Node>(MemoryManager.Map(nodeStagingBuffer.Memory).ToPointer(), nodes.Length);
        nodes.AsSpan().CopyTo(stagingNodes);

        var indicesStagingBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferSrcBit,
            (ulong) (sizeof(int) * indices.Length), SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, true, true);
        var stagingIndices =
            new Span<int>(MemoryManager.Map(indicesStagingBuffer.Memory).ToPointer(), indices.Length);
        indices.AsSpan().CopyTo(stagingIndices);

        var nodeBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
            (ulong) (Marshal.SizeOf<MasterBvhTree.Node>() * nodes.Length),
            SharingMode.Exclusive, queueIndices,
            MemoryPropertyFlags.DeviceLocalBit, false);

        var indexBuffer = MemoryBuffer.Create(
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
            (ulong) (sizeof(int) * indices.Length), SharingMode.Exclusive, queueIndices,
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
        
        CurrentRenderData.MasterBvhBuffer = nodeBuffer;
        CurrentRenderData.MasterBvhIndexBuffer = indexBuffer;

        nodeStagingBuffer.Dispose();
        indicesStagingBuffer.Dispose();
    }

    private unsafe void CreateOctreeDescriptorSet(Int3[] chunksToRender)
    {
        CurrentRenderData.OctreeDescriptorSet = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.VoxelOctree,
            chunksToRender.Length);

        DescriptorBufferInfo[] bufferInfos = new DescriptorBufferInfo[chunksToRender.Length];
        WriteDescriptorSet[] writeDescriptorSets = new WriteDescriptorSet[chunksToRender.Length];
        Span<DescriptorBufferInfo> bufferInfosSpan = bufferInfos;
        Span<WriteDescriptorSet> writeDescriptorSetsSpan = writeDescriptorSets;
        GCHandle bufferInfoHandle = GCHandle.Alloc(bufferInfos, GCHandleType.Pinned);
        GCHandle writeDescriptorSetHandle = GCHandle.Alloc(writeDescriptorSets, GCHandleType.Pinned);


        int currentChunk = 0;
        foreach (var chunk in chunksToRender)
        {
            var buffer = CurrentRenderData.ChunkOctreeBuffers[chunk];

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
                DstSet = CurrentRenderData.OctreeDescriptorSet,
                DstArrayElement = (uint) currentChunk,
                PBufferInfo = (DescriptorBufferInfo*) Unsafe.AsPointer(ref bufferInfos[currentChunk])
            };

            CurrentRenderData.ChunkDescriptorSetIndices[chunk] = currentChunk;
            currentChunk++;
        }

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, writeDescriptorSets, 0, null);

        bufferInfoHandle.Free();
        writeDescriptorSetHandle.Free();
    }

    private void DestroyMasterBvhDescriptorSet()
    {
        if (CurrentRenderData.MasterBvhDescriptorSet.Handle != default)
            DescriptorSetHandler.FreeDescriptorSet(CurrentRenderData.MasterBvhDescriptorSet);

        CurrentRenderData.MasterBvhDescriptorSet = default;
    }

    private void DestroyMasterBvhBuffer()
    {
        if (CurrentRenderData.MasterBvhBuffer.Buffer.Handle != default)
            CurrentRenderData.MasterBvhBuffer.Dispose();

        CurrentRenderData.MasterBvhBuffer = default;

        if (CurrentRenderData.MasterBvhIndexBuffer.Buffer.Handle != default)
            CurrentRenderData.MasterBvhIndexBuffer.Dispose();

        CurrentRenderData.MasterBvhIndexBuffer = default;
    }

    private void DestroyOctreeDescriptorSet()
    {
        if (CurrentRenderData.OctreeDescriptorSet.Handle != default)
            DescriptorSetHandler.FreeDescriptorSet(CurrentRenderData.OctreeDescriptorSet);

        CurrentRenderData.OctreeDescriptorSet = default;

        CurrentRenderData.ChunkDescriptorSetIndices.Clear();
    }

    private void UpdateOctree(Chunk chunk, out bool bufferAllocated)
    {
        bufferAllocated = false;
        CurrentRenderData.UsedChunks.Add(chunk.Position);

        //chunk data is up to date
        if (CurrentRenderData.ChunkCurrentVersion.TryGetValue(chunk.Position, out var chunkVersion)
            && chunkVersion == chunk.Version) return;


        uint bufferSize;
        using (chunk.Octree.AcquireReadLock())
            CalculateOctreeBufferSize(chunk.Octree, out bufferSize);

        bufferSize = (uint) MathHelper.CeilPower2(checked((int) bufferSize));

        var oldBufferSize = CurrentRenderData.ChunkOctreeBuffers.TryGetValue(chunk.Position, out var oldBuffer)
            ? oldBuffer.Size
            : 0;

        if ((uint) oldBufferSize < bufferSize)
        {
            bufferAllocated = true;
        }

        //shrink the buffer to save memory usage
        if ((uint) oldBufferSize > bufferSize * 4)
        {
            bufferSize /= 2;
            bufferAllocated = true;
        }

        if (bufferAllocated)
        {
            DestroyOctreeBuffer(chunk.Position);
            CreateOctreeBuffer(chunk.Position, bufferSize);
        }

        using (chunk.Octree.AcquireReadLock())
            FillOctreeBuffer(chunk.Position, chunk.Octree);

        CurrentRenderData.ChunkCurrentVersion[chunk.Position] = chunk.Version;
    }


    private void FreeUnusedBuffers()
    {
        var unusedChunks = CurrentRenderData.ChunkOctreeBuffers.Keys.Except(CurrentRenderData.UsedChunks).ToList();

        foreach (var chunk in unusedChunks)
        {
            DestroyOctreeBuffer(chunk);

            CurrentRenderData.ChunkCurrentVersion.Remove(chunk);
        }

        CurrentRenderData.UsedChunks.Clear();
    }

    private unsafe void CreateOctreeBuffer(Int3 position, ulong bufferSize)
    {
        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};

        var buffer = MemoryBuffer.Create(
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            bufferSize,
            SharingMode.Exclusive,
            queue,
            MemoryPropertyFlags.DeviceLocalBit,
            false
        );

        CurrentRenderData.ChunkOctreeBuffers[position] = buffer;
    }

    private unsafe void FillOctreeBuffer(Int3 position, VoxelOctree octree)
    {
        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};
        var headerSize = (uint) Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint) Marshal.SizeOf<VoxelOctree.Node>();

        var buffer = CurrentRenderData.ChunkOctreeBuffers[position];

        var cb = VulkanEngine.GetSingleTimeCommandBuffer();

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

        var srcNodes = octree.Nodes.AsSpan(0, (int) octree.NodeCount);
        var dstNodes = new Span<VoxelOctree.Node>((void*) (data + headerSize), srcNodes.Length);
        srcNodes.CopyTo(dstNodes);

        var srcData = octree.Data.renderData.AsSpan(0, (int) octree.DataCount);
        var dstData =
            new Span<VoxelRenderData>((void*) (data + headerSize + nodeSize * octree.NodeCount), srcData.Length);
        srcData.CopyTo(dstData);

        MemoryManager.UnMap(stagingBuffer.Memory);

        var copy = new BufferCopy
        {
            Size = buffer.Size,
            SrcOffset = 0,
            DstOffset = 0
        };

        VulkanEngine.Vk.CmdCopyBuffer(cb, stagingBuffer.Buffer, buffer.Buffer, 1, copy);
        VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

        stagingBuffer.Dispose();
    }

    private static void CalculateOctreeBufferSize(VoxelOctree octree, out uint bufferSize)
    {
        var headerSize = (uint) Marshal.SizeOf<OctreeHeader>();
        var nodeSize = (uint) Marshal.SizeOf<VoxelOctree.Node>();
        var dataSize = (uint) Marshal.SizeOf<VoxelRenderData>();

        bufferSize = headerSize + nodeSize * octree.NodeCount + dataSize * octree.DataCount;
    }

    private void DestroyOctreeBuffer(Int3 chunkPosition)
    {
        if (CurrentRenderData.ChunkOctreeBuffers.Remove(chunkPosition, out var buffer))
            buffer.Dispose();
    }

    private static void CalculateChunkRenderInfo(Vector3 cameraPosition, float cameraFarPlane,
        out int renderDistanceLog,
        out int renderDistance, out Int3 chunkPosition)
    {
        var chunkFarPlane = cameraFarPlane / Chunk.Size;
        //1290 is the theoretical maximum render distance in chunks
        renderDistance = chunkFarPlane < int.MaxValue ? (int) chunkFarPlane : 1290;
        renderDistanceLog = (int) Math.Log2(MathHelper.CeilPower2(renderDistance));
        chunkPosition = new Int3((int) (cameraPosition.X / Chunk.Size), (int) (cameraPosition.Y / Chunk.Size),
            (int) (cameraPosition.Z / Chunk.Size));
    }

    private IEnumerable<Chunk> GetChunksToRender(Int3 currentChunk, int chunkRenderDistance)
    {
        if (World is not TechardryWorld world)
        {
            yield break;
        }

        foreach (var chunkPosition in world.ChunkManager.GetLoadedChunks())
        {
            //Check for a cubical render distance instead of a spherical one
            //This is because of the technical implementation of the rendering
            bool inRenderDistance = chunkPosition.X <= currentChunk.X + chunkRenderDistance &&
                                    chunkPosition.X >= currentChunk.X - chunkRenderDistance &&
                                    chunkPosition.Y <= currentChunk.Y + chunkRenderDistance &&
                                    chunkPosition.Y >= currentChunk.Y - chunkRenderDistance &&
                                    chunkPosition.Z <= currentChunk.Z + chunkRenderDistance &&
                                    chunkPosition.Z >= currentChunk.Z - chunkRenderDistance;

            if (inRenderDistance && world.ChunkManager.TryGetChunk(chunkPosition, out var chunk))
                yield return chunk;
        }
    }

    private unsafe void CreateInputAttachments(uint index)
    {
        CurrentRenderData.InputAttachmentDescriptorSet =
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
            new WriteDescriptorSet(StructureType.WriteDescriptorSet, null,
                CurrentRenderData.InputAttachmentDescriptorSet, 0, 0,
                1, DescriptorType.InputAttachment, &depthImageInfo),

            new WriteDescriptorSet(StructureType.WriteDescriptorSet, null,
                CurrentRenderData.InputAttachmentDescriptorSet, 1, 0,
                1, DescriptorType.InputAttachment, &colorImageInfo)
        };

        VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, (uint) writeDescriptorSets.Length,
            writeDescriptorSets.GetPinnableReference(), 0, null);

        CurrentRenderData.LastColorImageView = VulkanEngine.SwapchainImageViews[index];
    }

    [StructLayout(LayoutKind.Explicit)]
    struct OctreeHeader
    {
        [UsedImplicitly] [FieldOffset(0)] public uint NodeCount;
        [UsedImplicitly] [FieldOffset(4)] public Int3 treeMin;
    }

    private unsafe void CreateCameraDataBuffers()
    {
        Span<uint> queue = stackalloc uint[] {VulkanEngine.QueueFamilyIndexes.GraphicsFamily!.Value};

        foreach (var renderData in _renderData)
        {
            renderData.CameraDataBuffer = MemoryBuffer.Create(
                BufferUsageFlags.UniformBufferBit,
                (ulong) Marshal.SizeOf<CameraData>(), SharingMode.Exclusive, queue,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, false, true);
        }
    }

    private unsafe void CreateCameraDataDescriptors()
    {
        Span<WriteDescriptorSet> cameraDataDescriptorWrites = stackalloc WriteDescriptorSet[]
        {
            new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                DstBinding = 0,
                DstArrayElement = 0,
            }
        };

        foreach (var renderData in _renderData)
        {
            ref var cameraDataDescriptor = ref renderData.CameraDataDescriptors;
            cameraDataDescriptor = DescriptorSetHandler.AllocateDescriptorSet(DescriptorSetIDs.CameraData);

            var bufferInfo = new DescriptorBufferInfo
            {
                Buffer = renderData.CameraDataBuffer.Buffer,
                Offset = 0,
                Range = (ulong) Marshal.SizeOf<CameraData>()
            };


            cameraDataDescriptorWrites[0].DstSet = cameraDataDescriptor;
            cameraDataDescriptorWrites[0].PBufferInfo = &bufferInfo;

            VulkanEngine.Vk.UpdateDescriptorSets(VulkanEngine.Device, cameraDataDescriptorWrites, 0, null);
        }
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


    public override Identification Identification => SystemIDs.VoxelRender;

    public override void Dispose()
    {
        foreach (var renderData in _renderData)
        {
            DescriptorSetHandler.FreeDescriptorSet(renderData.InputAttachmentDescriptorSet);
            DescriptorSetHandler.FreeDescriptorSet(renderData.CameraDataDescriptors);
            renderData.CameraDataBuffer.Dispose();


            DescriptorSetHandler.FreeDescriptorSet(renderData.OctreeDescriptorSet);
            foreach (var buffer in renderData.ChunkOctreeBuffers.Values)
            {
                buffer.Dispose();
            }

            renderData.ChunkOctreeBuffers.Clear();

            DescriptorSetHandler.FreeDescriptorSet(renderData.MasterBvhDescriptorSet);
            renderData.MasterBvhBuffer.Dispose();
            renderData.MasterBvhIndexBuffer.Dispose();
        }

        base.Dispose();
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct CameraData
    {
        [FieldOffset(sizeof(float) * 0)] public float HFov;
        [FieldOffset(sizeof(float) * 1)] public float AspectRatio;
        [FieldOffset(sizeof(float) * 2)] public Vector3 Forward;
        [FieldOffset(sizeof(float) * 5)] public Vector3 Upward;

        [FieldOffset(sizeof(float) * 8)] public Vector3 Position;
        [FieldOffset(sizeof(float) * 11)] public float Near;
        [FieldOffset(sizeof(float) * 12)] public float Far;
    }
}