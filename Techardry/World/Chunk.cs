using System.Numerics;
using DotNext.Diagnostics;
using MintyCore;
using MintyCore.Utils;
using Techardry.Blocks;
using Techardry.Networking;
using Techardry.Utils;
using Techardry.Voxels;

namespace Techardry.World;

public class Chunk : IDisposable
{
    public const int Size = VoxelOctree.Dimensions;

    public Int3 Position { get; }
    internal VoxelOctree Octree { get; set; }
    public uint Version { get; set; }
    public uint LastSyncedVersion { get; set; }
    public Timestamp LastSyncedTime { get; set; }

    public TechardryWorld ParentWorld { get; }
    

    public Chunk(Int3 chunkPos, TechardryWorld parentWorld)
    {
        Position = chunkPos;
        Octree = new VoxelOctree();
        ParentWorld = parentWorld;
        
    }

   

    public void Update()
    {
        if (ParentWorld.IsServerWorld)
            UpdateServer();
        else
            UpdateClient();
    }

    private void UpdateServer()
    {
        if (Version == LastSyncedVersion) return;

        var chunkData = new ChunkDataMessage
        {
            ChunkPosition = Position,
            Octree = Octree,
            WorldId = ParentWorld.Identification
        };

        chunkData.Send(PlayerHandler.GetConnectedPlayers());

        LastSyncedVersion = Version;
        LastSyncedTime = new Timestamp();
    }

    private void UpdateClient()
    {
        if (Version == LastSyncedVersion) return;

        if (LastSyncedTime.ElapsedMilliseconds < 1000) return;

        var requestData = new RequestChunkData()
        {
            Position = Position
        };

        requestData.SendToServer();

        LastSyncedTime = new Timestamp();
    }

    public void SetBlock(Vector3 blockPos, Identification blockId, BlockRotation rotation = BlockRotation.None)
    {
        SetBlock(blockPos, blockId, VoxelOctree.SizeOneDepth, rotation);
    }

    public void SetBlock(Vector3 blockPos, Identification blockId, int depth,
        BlockRotation rotation = BlockRotation.None)
    {
        Logger.AssertAndThrow(BlockHandler.DoesBlockExist(blockId), "Block to place does not exist", "World");
        Logger.AssertAndThrow(depth >= VoxelOctree.SizeOneDepth || BlockHandler.IsBlockSplittable(blockId),
            "Invalid block placement depth", "World");
        Logger.AssertAndThrow(rotation == BlockRotation.None || BlockHandler.IsBlockRotatable(blockId),
            "Invalid block placement rotation", "World");

        Octree.Insert(new VoxelData(blockId), blockPos, depth);

        Version++;
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Clockwise block rotation in 90 degree increments.
/// </summary>
[Flags]
public enum BlockRotation
{
    None = 0b00_00_00,
    Z90 = 0b00_00_01,
    Z180 = 0b00_00_10,
    Z270 = 0b00_00_11,
    Y90 = 0b00_01_00,
    Y180 = 0b00_10_00,
    Y270 = 0b00_11_00,
    X90 = 0b01_00_00,
    X180 = 0b10_00_00,
    X270 = 0b11_00_00,
}