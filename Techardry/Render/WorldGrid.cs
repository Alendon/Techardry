using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Techardry.Utils;

namespace Techardry.Render;

//TODO implement support for dynamic entities

public class WorldGrid : IDisposable
{
    private readonly Cell[] _gridCells;
    private readonly int _cellCount;

    //we need to slice the span as the array returned by the pool might be larger than the cell count
    public Span<Cell> Cells => _gridCells.AsSpan(0, _cellCount);
    public WorldGridHeader Header => new()
    {
        minX = _min.X,
        minY = _min.Y,
        minZ = _min.Z,
        sizeX = _size.X,
        sizeY = _size.Y,
        sizeZ = _size.Z
    };

    private readonly Int3 _min;
    private readonly Int3 _size;

    /// <summary>
    /// Creates a new world grid
    /// </summary>
    /// <param name="min">The minimum chunk position inclusive</param>
    /// <param name="max">The maximum chunk position inclusive</param>
    public WorldGrid(Int3 min, Int3 max)
    {
        _min = min;

        _size = max - _min + Int3.One;
        _cellCount = _size.X * _size.Y * _size.Z;
        _gridCells = ArrayPool<Cell>.Shared.Rent(_cellCount);
    }

    public void InsertChunk(Int3 position, ulong chunkPointer)
    {
        var normalizedX = position.X - _min.X;
        var normalizedY = position.Y - _min.Y;
        var normalizedZ = position.Z - _min.Z;
        
        Debug.Assert(normalizedX >= 0 && normalizedX < _size.X);
        Debug.Assert(normalizedY >= 0 && normalizedY < _size.Y);
        Debug.Assert(normalizedZ >= 0 && normalizedZ < _size.Z);
        
        var index = normalizedX + normalizedY * _size.X + normalizedZ * _size.X * _size.Y;
        
        _gridCells[index] = new Cell { ChunkPointer = chunkPointer };
    }


    public void Dispose()
    {
        ArrayPool<Cell>.Shared.Return(_gridCells);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Cell
    {
        public ulong ChunkPointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldGridHeader
    {
        public int minX;
        public int minY;
        public int minZ;
        
        public int sizeX;
        public int sizeY;
        public int sizeZ;
    }
}