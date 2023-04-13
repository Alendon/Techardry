using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Utils;

namespace Techardry.Components.Common;

[RegisterComponent("last_chunk")]
public struct LastChunk : IComponent
{
    public Int3 Value;

    public void PopulateWithDefaultValues()
    {
    }

    public void Serialize(DataWriter writer, IWorld world, Entity entity)
    {
    }

    public bool Deserialize(DataReader reader, IWorld world, Entity entity)
    {
        return true;
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