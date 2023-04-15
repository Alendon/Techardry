using DotNext.Threading;
using MintyCore.ECS;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.World;

namespace Techardry.Networking;

[RegisterMessage("request_chunk_data")]
public partial class RequestChunkData : IMessage
{
    public void Serialize(DataWriter writer)
    {
        Position.Serialize(writer);
        WorldId.Serialize(writer);
    }

    public bool Deserialize(DataReader reader)
    {
        if(!Int3.TryDeserialize(reader, out Position)) return false;
        if(!Identification.Deserialize(reader, out WorldId)) return false;
        
        if(!IsServer) return false;
        
        if(!WorldHandler.TryGetWorld(GameType.Server, WorldId, out var world) || world is not TechardryWorld techardryWorld)
            return false;

        if (!techardryWorld.ChunkManager.TryGetChunk(Position, out var chunk)) return false;

        chunk.Octree.AcquireReadLock();

        var chunkDataMessage = new ChunkDataMessage()
        {
            ChunkPosition = Position,
            WorldId = WorldId,
            Octree = chunk.Octree
        };
        
        chunkDataMessage.Send(Sender);

        return true;
    }

    public void Clear()
    {
        Position = default;
        WorldId = default;
    }

    public bool IsServer { get; set; }
    public bool ReceiveMultiThreaded => true;
    public Identification MessageId => MessageIDs.RequestChunkData;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Reliable;
    public ushort Sender { get; set; }
    
    public Int3 Position;
    public Identification WorldId;
}