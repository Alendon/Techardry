using System.Buffers;
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
    public ulong Version { get; private set; } = 0;
    private ulong _lastBufferVersion = 0;
    private (Int3 position, ulong address)[] _buffersArray = ArrayPool<(Int3, ulong)>.Shared.Rent(256);
    private int _buffersLength = 0;

    public Span<(Int3 position, ulong address)> Buffers
    {
        get
        {
            if(_lastBufferVersion == Version) return _buffersArray.AsSpan()[.._buffersLength];
            _lastBufferVersion = Version;
            
            if(_buffersArray.Length < _buffers.Count)
            {
                ArrayPool<(Int3, ulong)>.Shared.Return(_buffersArray);
                _buffersArray = ArrayPool<(Int3, ulong)>.Shared.Rent(_buffers.Count);
            }
            
            _buffersLength = 0;
            foreach (var (position, bufferInfo) in _buffers)
            {
                _buffersArray[_buffersLength++] = (position, bufferInfo.Address);
            }
            
            return _buffersArray.AsSpan()[.._buffersLength];
        }
    }

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

    public void SetNewBuffer(Int3 position, uint version, MemoryBuffer buffer, ulong address)
    {
        _buffers.Add(position, new ChunkBufferInfo
        {
            Buffer = buffer,
            Version = version,
            Address = address
        });
        
        Version++;
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

        Version = previousVoxelData.Version;
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
        public required ulong Address;

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