using JetBrains.Annotations;
using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Modding.Implementations;
using MintyCore.Utils;
using Techardry.Render;
using RegistryIDs = Techardry.Identifications.RegistryIDs;

namespace Techardry.Registries;

[Registry("texture_atlas")]
public class TextureAtlasRegistry : IRegistry
{
    public required ITextureAtlasHandler TextureAtlasHandler { private get; [UsedImplicitly] set; }
    
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
        TextureAtlasHandler.Clear();
    }

    [RegisterMethod(ObjectRegistryPhase.Main)]
    public void RegisterTextureAtlas(Identification id, TextureAtlasInfo info)
    {
        TextureAtlasHandler.CreateTextureAtlas(id, info.Textures);
    }
    

    public ushort RegistryId => RegistryIDs.TextureAtlas;
    public IEnumerable<ushort> RequiredRegistries => Enumerable.Empty<ushort>();
}

public record struct TextureAtlasInfo(Identification[] Textures);