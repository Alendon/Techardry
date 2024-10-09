using JetBrains.Annotations;
using MintyCore.ECS;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;
using Serilog;
using Techardry.Blocks;
using Techardry.Identifications;
using Techardry.Render;
using Techardry.Utils;
using Techardry.Voxels;
using Techardry.World;

namespace Techardry.Networking;

[RegisterMessage("chunk_data")]
public partial class ChunkDataMessage : IMessage
{
    public Int3 ChunkPosition;
    public Identification WorldId;
    public VoxelOctree? Octree;
    public required ITextureAtlasHandler TextureAtlasHandler { private get; [UsedImplicitly] init; }
    public required IBlockHandler BlockHandler { private get; [UsedImplicitly] init; }

    public void Serialize(DataWriter writer)
    {
        if (Octree is null)
            throw new NullReferenceException("Octree is null");

        ChunkPosition.Serialize(writer);
        WorldId.Serialize(writer);
        Octree.Serialize(writer);
        
        Log.Debug("Send chunk data for {ChunkPosition}", ChunkPosition);
    }

    public bool Deserialize(DataReader reader)
    {
        Log.Debug("Start receiving chunk data");
        if (IsServer)
        {
            return false;
        }

        if (!Int3.TryDeserialize(reader, out ChunkPosition) ||
            !Identification.Deserialize(reader, out WorldId) ||
            !VoxelOctree.TryDeserialize(reader, out Octree, TextureAtlasHandler, BlockHandler))
        {
            return false;
        }

        if (!WorldHandler.TryGetWorld(GameType.Client, WorldId, out var world) ||
            world is not TechardryWorld techardryWorld)
        {
            return false;
        }

        techardryWorld.ChunkManager.UpdateChunk(ChunkPosition, Octree);
        
        Log.Debug("Received chunk data for {ChunkPosition}", ChunkPosition);
        
        return true;
    }

    public void Clear()
    {
        ChunkPosition = default;
        WorldId = default;
        Octree = default;
    }

    public bool IsServer { get; set; }
    public bool ReceiveMultiThreaded => true;
    public Identification MessageId => MessageIDs.ChunkData;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Reliable;
    public ushort Sender { get; set; }

    /// <inheritdoc />
    public required INetworkHandler NetworkHandler { get; init; }

    public required IWorldHandler WorldHandler { private get; init; }
}