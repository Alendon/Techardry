using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Utils;

namespace Techardry.Components.Common;

[RegisterComponent("last_chunk")]
public struct LastChunk : IComponent
{
    public Int2 Value;
    public bool HasValue;

    public void PopulateWithDefaultValues()
    {
    }

    public void Serialize(DataWriter writer, IWorld world, Entity entity)
    {
        writer.Put(HasValue);
        Value.Serialize(writer);
    }

    public bool Deserialize(DataReader reader, IWorld world, Entity entity)
    {
        return reader.TryGetBool(out HasValue) && Int2.TryDeserialize(reader, out Value);
    }

    public void IncreaseRefCount()
    {
    }

    public void DecreaseRefCount()
    {
    }

    public bool Dirty { get; set; }
    public Identification Identification => ComponentIDs.LastChunk;
}