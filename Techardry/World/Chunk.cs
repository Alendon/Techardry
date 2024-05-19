using System.Numerics;
using DotNext.Diagnostics;
using DotNext.Threading;
using MintyCore;
using MintyCore.ECS;
using MintyCore.Network;
using MintyCore.Utils;
using Serilog;
using Techardry.Blocks;
using Techardry.Networking;
using Techardry.Render;
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
    private readonly IPlayerHandler _playerHandler;
    private readonly INetworkHandler _networkHandler;
    private readonly IBlockHandler _blockHandler;
    
    private HashSet<Entity> _associatedPlayerEntities = new();
    private HashSet<ushort> _associatedPlayers = new();

    public Chunk(Int3 chunkPos, TechardryWorld parentWorld, IPlayerHandler playerHandler,
        INetworkHandler networkHandler, IBlockHandler blockHandler, ITextureAtlasHandler textureAtlasHandler) : this(
        chunkPos, new VoxelOctree(textureAtlasHandler, blockHandler), parentWorld,
        playerHandler, networkHandler, blockHandler)
    {
    }

    internal Chunk(Int3 chunkPos, VoxelOctree octree, TechardryWorld parentWorld, IPlayerHandler playerHandler,
        INetworkHandler networkHandler, IBlockHandler blockHandler)
    {
        Position = chunkPos;
        Octree = octree;
        ParentWorld = parentWorld;
        _playerHandler = playerHandler;
        _networkHandler = networkHandler;
        _blockHandler = blockHandler;
    }

    public void AddPlayerEntity(Entity entity)
    {
        if(!ParentWorld.IsServerWorld)
            throw new InvalidOperationException("Entity tracking is only available on the server");
        
        _associatedPlayerEntities.Add(entity);
        UpdateAssociatedPlayers();
    }
    
    public void RemovePlayerEntity(Entity entity)
    {
        if(!ParentWorld.IsServerWorld)
            throw new InvalidOperationException("Entity tracking is only available on the server");
        
        _associatedPlayerEntities.Remove(entity);
        UpdateAssociatedPlayers();
    }
    
    private void UpdateAssociatedPlayers()
    {
        if(!ParentWorld.IsServerWorld)
            throw new InvalidOperationException("Entity tracking is only available on the server");
        
        var oldPlayers = _associatedPlayers;
        _associatedPlayers =
            _associatedPlayerEntities.Select(x => ParentWorld.EntityManager.GetEntityOwner(x)).ToHashSet();
        
        var addedPlayers = _associatedPlayers.Except(oldPlayers);
        var removedPlayers = oldPlayers.Except(_associatedPlayers);

        var createChunkMessage = _networkHandler.CreateMessage<CreateChunk>();
        createChunkMessage.ChunkPosition = Position;
        createChunkMessage.WorldId = ParentWorld.Identification;
        
        createChunkMessage.Send(addedPlayers);
        
        var removeChunkMessage = _networkHandler.CreateMessage<ReleaseChunk>();
        removeChunkMessage.ChunkPosition = Position;
        removeChunkMessage.WorldId = ParentWorld.Identification;
        
        removeChunkMessage.Send(removedPlayers);
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

        using var octreeLock = Octree.AcquireReadLock();
        if (octreeLock.IsEmpty)
        {
            Log.Error("Tried to update server chunk but failed to acquire read lock for chunk {Position}", Position);
            return;
        }

        var chunkData = _networkHandler.CreateMessage<ChunkDataMessage>();
        chunkData.ChunkPosition = Position;
        chunkData.Octree = Octree;
        chunkData.WorldId = ParentWorld.Identification;

        chunkData.Send(_associatedPlayers);

        LastSyncedVersion = Version;
        LastSyncedTime = new Timestamp();
    }

    private void UpdateClient()
    {
        if (Version == LastSyncedVersion) return;

        if (LastSyncedTime.ElapsedMilliseconds < 1000) return;

        var requestData = _networkHandler.CreateMessage<RequestChunkData>();
        requestData.Position = Position;
        requestData.WorldId = ParentWorld.Identification;

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
        if (!_blockHandler.DoesBlockExist(blockId))
        {
            throw new ArgumentException("Block to place does not exist", nameof(blockId));
        }

        if (depth < VoxelOctree.SizeOneDepth && !_blockHandler.IsBlockSplittable(blockId))
        {
            throw new ArgumentException("Invalid block placement depth", nameof(depth));
        }

        if (rotation != BlockRotation.None && !_blockHandler.IsBlockRotatable(blockId))
        {
            throw new ArgumentException("Invalid block placement rotation", nameof(rotation));
        }

        blockPos = new Vector3(blockPos.X % Size, blockPos.Y % Size, blockPos.Z % Size);
        blockPos.X = blockPos.X < 0 ? blockPos.X + Size : blockPos.X;
        blockPos.Y = blockPos.Y < 0 ? blockPos.Y + Size : blockPos.Y;
        blockPos.Z = blockPos.Z < 0 ? blockPos.Z + Size : blockPos.Z;

        using var octreeLock = Octree.AcquireWriteLock();

        Octree.Insert(new VoxelData(blockId), blockPos, depth);

        Version++;
    }

    public Identification GetBlockId(Vector3 blockPos)
    {
        blockPos = new Vector3(blockPos.X % Size, blockPos.Y % Size, blockPos.Z % Size);
        blockPos.X = blockPos.X < 0 ? blockPos.X + Size : blockPos.X;
        blockPos.Y = blockPos.Y < 0 ? blockPos.Y + Size : blockPos.Y;
        blockPos.Z = blockPos.Z < 0 ? blockPos.Z + Size : blockPos.Z;

        using var octreeLock = Octree.AcquireReadLock();

        return Octree.GetVoxelData(ref Octree.GetNode(blockPos, VoxelOctree.MaxDepth)).Id;
    }


    public void Dispose()
    {
        if(ParentWorld.IsServerWorld)
        {
            _associatedPlayerEntities.Clear();
            UpdateAssociatedPlayers();
        }
    }

    public void SetOctree(VoxelOctree octree)
    {
        using var octreeLock = Octree.AcquireWriteLock();
        Octree = octree;

        Version++;
        LastSyncedVersion = Version;
        LastSyncedTime = new Timestamp();
    }

    public VoxelCollider CreateCollider()
    {
        return new VoxelCollider(Octree);
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