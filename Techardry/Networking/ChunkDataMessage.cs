using MintyCore.ECS;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
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
    
    public void Serialize(DataWriter writer)
    {
        Logger.AssertAndThrow(Octree is not null, "Octree is null", "ChunkDataMessage");
        
        ChunkPosition.Serialize(writer);
        WorldId.Serialize(writer);
        Octree.Serialize(writer);
    }

    public bool Deserialize(DataReader reader)
    {
        if (IsServer) return false;

        if (!Int3.TryDeserialize(reader, out ChunkPosition) ||
            !Identification.Deserialize(reader, out WorldId) ||
            !VoxelOctree.TryDeserialize(reader, out Octree))
            return false;
        
        if(!WorldHandler.TryGetWorld(GameType.Client, WorldId, out var world) || world is not TechardryWorld techardryWorld)
            return false;
        
        if(!techardryWorld.TryGetChunk(ChunkPosition, out var chunk))
            return false;
        
        chunk.Octree = Octree;

        return true;
    }

    public void Clear()
    {
        ChunkPosition = default;
        WorldId = default;
        Octree = default;
    }

    public bool IsServer { get; set; }
    public bool ReceiveMultiThreaded => false;
    public Identification MessageId => MessageIDs.ChunkData;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Reliable;
    public ushort Sender { get; set; }
}