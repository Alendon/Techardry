using MintyCore.ECS;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.World;

namespace Techardry.Networking;

[RegisterMessage("create_chunk")]
public partial class CreateChunk : IMessage
{
    public void Serialize(DataWriter writer)
    {
        if(WorldId == Identification.Invalid)
            throw new InvalidOperationException("Invalid chunk data");
        
        ChunkPosition.Serialize(writer);
        WorldId.Serialize(writer);
    }

    public bool Deserialize(DataReader reader)
    {
        if (IsServer) return false;
        
        if(!Int3.TryDeserialize(reader, out ChunkPosition)) return false;
        if(!Identification.Deserialize(reader, out WorldId)) return false;
        
        if(!WorldHandler.TryGetWorld(GameType.Client, WorldId, out var world) || world is not TechardryWorld techardryWorld)
            return false;

        techardryWorld.ChunkManager.CreateChunk(ChunkPosition);

        return true;
    }

    public void Clear()
    {
        ChunkPosition = Int3.Zero;
        WorldId = Identification.Invalid;
    }

    public bool IsServer { get; set; }
    public bool ReceiveMultiThreaded => true;
    public Identification MessageId => MessageIDs.CreateChunk;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Reliable;
    public ushort Sender { get; set; }

    /// <inheritdoc />
    public required INetworkHandler NetworkHandler { get; init; }
    public required IWorldHandler WorldHandler { private get; init; }

    public Int3 ChunkPosition;
    public Identification WorldId;
}