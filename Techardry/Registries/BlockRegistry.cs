using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Utils;
using Techardry.Blocks;
using Techardry.Identifications;

namespace Techardry.Registries;

[Registry("block")]
public class BlockRegistry : IRegistry
{
    [RegisterMethod(ObjectRegistryPhase.Main)]
    public static void RegisterBlock(Identification blockId, IBlock block)
    {
        BlockHandler.Add(blockId, block);
    }
    
    
    /// <summary />
    public static event Action OnRegister = () => { };

    /// <summary />
    public static event Action OnPostRegister = () => { };

    /// <summary />
    public static event Action OnPreRegister = () => { };
    
    public void PreRegister()
    {
        OnPreRegister();
    }

    public void Register()
    {
        OnRegister();
    }

    public void PostRegister()
    {
        OnPostRegister();
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
        ClearRegistryEvents();
        BlockHandler.Clear();
    }

    public void ClearRegistryEvents()
    {
        OnPreRegister = delegate {  };
        OnRegister = delegate {  };
        OnPostRegister = delegate {  };
    }

    public ushort RegistryId => RegistryIDs.Block;
    public IEnumerable<ushort> RequiredRegistries => Array.Empty<ushort>();
}