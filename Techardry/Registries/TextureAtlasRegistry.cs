using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Render;

namespace Techardry.Registries;

[Registry("texture_atlas")]
public class TextureAtlasRegistry : IRegistry
{
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
       TextureAtlasHandler.RemoveTextureAtlas(objectId);
    }

    public void PostUnRegister()
    {
        
    }

    public void Clear()
    {
        ClearRegistryEvents();
        TextureAtlasHandler.Clear();
    }

    [RegisterMethod(ObjectRegistryPhase.Main)]
    public static void RegisterTextureAtlas(Identification id, TextureAtlasInfo info)
    {
        TextureAtlasHandler.CreateTextureAtlas(id, info.Textures);
    }

    public void ClearRegistryEvents()
    {
        OnRegister = () => { };
        OnPostRegister = () => { };
        OnPreRegister = () => { };
    }

    public ushort RegistryId => RegistryIDs.TextureAtlas;
    public IEnumerable<ushort> RequiredRegistries => Enumerable.Empty<ushort>();
}

public record struct TextureAtlasInfo(Identification[] Textures);