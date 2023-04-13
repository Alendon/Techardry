using MintyCore.Network;
using MintyCore.Utils;

namespace Techardry.Networking;

public class ReleaseChunk : IMessage
{
    public void Serialize(DataWriter writer)
    {
        throw new NotImplementedException();
    }

    public bool Deserialize(DataReader reader)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public void SendToServer()
    {
        throw new NotImplementedException();
    }

    public void Send(IEnumerable<ushort> receivers)
    {
        throw new NotImplementedException();
    }

    public void Send(ushort receiver)
    {
        throw new NotImplementedException();
    }

    public void Send(ushort[] receivers)
    {
        throw new NotImplementedException();
    }

    public bool IsServer { get; set; }
    public bool ReceiveMultiThreaded { get; }
    public Identification MessageId { get; }
    public DeliveryMethod DeliveryMethod { get; }
    public ushort Sender { get; set; }
}