using MintyCore.ECS;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.World;

namespace Techardry.Networking;

[RegisterMessage("release_chunk")]
public partial class ReleaseChunk : IMessage
{
    public void Serialize(DataWriter writer)
    {
        ChunkPosition.Serialize(writer);
        WorldId.Serialize(writer);
    }

    public bool Deserialize(DataReader reader)
    {
        if (!Int3.TryDeserialize(reader, out ChunkPosition)) return false;
        if (!Identification.Deserialize(reader, out WorldId)) return false;
        
        if (!WorldHandler.TryGetWorld(GameType.Client, WorldId, out var world) || world is not TechardryWorld techardryWorld)
            return false;
        
        techardryWorld.ChunkManager.RemoveChunk(ChunkPosition);
        return true;
    }

    public void Clear()
    {
    }

    public bool IsServer { get; set; }
    public bool ReceiveMultiThreaded => true;
    public Identification MessageId => MessageIDs.ReleaseChunk;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Reliable;
    public ushort Sender { get; set; }

    public Int3 ChunkPosition;
    public Identification WorldId;
}