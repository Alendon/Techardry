using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Modding.Implementations;
using MintyCore.Utils;
using Techardry.Blocks;
using Techardry.Identifications;


namespace Techardry.Registries;

[Registry("block")]
public class BlockRegistry : IRegistry
{
    public required IBlockHandler BlockHandler { private get; init; }

    [RegisterMethod(ObjectRegistryPhase.Main)]
    public void RegisterBlock(Identification blockId, IBlock block)
    {
        BlockHandler.Add(blockId, block);
    }

    public void PreUnRegister()
    {
    }

    public void UnRegister(Identification objectId)
    {
        BlockHandler.Remove(objectId);
    }

    public void PostUnRegister()
    {
    }

    public void Clear()
    {
        BlockHandler.Clear();
    }


    public ushort RegistryId => RegistryIDs.Block;
    public IEnumerable<ushort> RequiredRegistries => Array.Empty<ushort>();
}