using JetBrains.Annotations;
using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Modding.Implementations;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.Registries;

/// <summary>
///     <see cref="IRegistry" /> to register Fonts
/// </summary>
[Registry("font", "fonts")]
[PublicAPI]
public class FontRegistry : IRegistry
{
    /// <inheritdoc />
    public ushort RegistryId => RegistryIDs.Font;

    /// <inheritdoc />
    public IEnumerable<ushort> RequiredRegistries => Enumerable.Empty<ushort>();



    /// <inheritdoc />
    public void UnRegister(Identification objectId)
    {

    }


    /// <inheritdoc />
    public void Clear()
    {
    }

    /// <summary>
    /// Register a font family
    /// Used by the source generator
    /// </summary>
    /// <param name="id">Id of the font</param>
    [RegisterMethod(ObjectRegistryPhase.Main, RegisterMethodOptions.HasFile)]
    public void RegisterFont(Identification id)
    {

    }
}