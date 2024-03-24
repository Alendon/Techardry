using System;
using System.Collections.Generic;
using System.Linq;
using DotNext.Collections.Generic;
using MintyCore.Graphics.Render.Data;
using MintyCore.Graphics.VulkanObjects;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Utils;

namespace Techardry.Render;

[RegisterIntermediateRenderDataByType("voxel")]
public class VoxelIntermediateData : IntermediateData
{
    private readonly Dictionary<Int3, ChunkBufferInfo> _oldBuffers = new();
    private readonly Dictionary<Int3, ChunkBufferInfo> _buffers = new();

    public IEnumerable<KeyValuePair<Int3, MemoryBuffer>> Buffers =>
        _buffers.Select(x => new KeyValuePair<Int3, MemoryBuffer>(x.Key, x.Value.Buffer));

    public bool TryUseOldBuffer(Int3 position, uint version)
    {
        if (!_oldBuffers.Remove(position, out var bufferInfo)) return false;

        if (bufferInfo.Version != version)
        {
            bufferInfo.DecreaseRefCount();
            return false;
        }

        _buffers.Add(position, bufferInfo);
        return true;
    }

    public void SetNewBuffer(Int3 position, uint version, MemoryBuffer buffer)
    {
        _buffers.Add(position, new ChunkBufferInfo
        {
            Buffer = buffer,
            Version = version
        });
    }

    public void ReleaseOldUnusedBuffers()
    {
        foreach (var (_, bufferInfo) in _oldBuffers)
        {
            bufferInfo.DecreaseRefCount();
        }

        _oldBuffers.Clear();
    }

    public override void CopyFrom(IntermediateData? previousData)
    {
        if (previousData is not VoxelIntermediateData previousVoxelData)
            return;

        _oldBuffers.AddAll(previousVoxelData._buffers);

        foreach (var (_, bufferInfo) in previousVoxelData._buffers)
        {
            //Increase the ref count now than later, to avoid the buffer being disposed in the mean time
            bufferInfo.IncreaseRefCount();
        }
    }

    public override void Clear()
    {
        if (_oldBuffers.Count != 0)
            throw new InvalidOperationException(
                "The old buffers should have been released before clearing the intermediate data");

        foreach (var (_, bufferInfo) in _buffers)
        {
            bufferInfo.DecreaseRefCount();
        }

        _buffers.Clear();
    }

    public override void Dispose()
    {
        Clear();
    }

    public override Identification Identification => IntermediateRenderDataIDs.Voxel;

    private class ChunkBufferInfo
    {
        public required MemoryBuffer Buffer;
        public required uint Version;

        private int _refCount = 1;

        public void IncreaseRefCount()
        {
            _refCount++;
        }

        public void DecreaseRefCount()
        {
            if (_refCount <= 0)
                throw new InvalidOperationException("The ref count can not be decreased below 0");

            _refCount--;
            if (_refCount == 0)
            {
                Buffer.Dispose();
            }
        }
    }
}