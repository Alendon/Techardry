using System.Numerics;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.Components.Client;

[PlayerControlled]
[RegisterComponent("input")]
public struct InputComponent : IComponent
{
    public void PopulateWithDefaultValues()
    {
        Movement = Vector3.Zero;
    }

    public void Serialize(DataWriter writer, IWorld world, Entity entity)
    {
        writer.Put(Movement);
    }

    public bool Deserialize(DataReader reader, IWorld world, Entity entity)
    {
        return reader.TryGetVector3(out Movement);
    }

    public void IncreaseRefCount()
    {
        
    }

    public void DecreaseRefCount()
    {
        
    }

    public bool Dirty { get; set; }
    public Identification Identification => ComponentIDs.Input;
    public Vector3 Movement;
}