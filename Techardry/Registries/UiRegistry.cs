using JetBrains.Annotations;
using MintyCore;
using MintyCore.Identifications;
using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Modding.Implementations;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.UI;
using Techardry.UI.Interfaces;
using RegistryIDs = Techardry.Identifications.RegistryIDs;

namespace Techardry.Registries;

/// <summary>
///     Registry to handle ui root element and element prefab registration
/// </summary>
[Registry("ui")]
[PublicAPI]
public class UiRegistry : IRegistry
{
    /// <inheritdoc />
    public ushort RegistryId => RegistryIDs.Ui;
    public required IUiHandler UiHandler { [UsedImplicitly] init; private get; }

    /// <inheritdoc />
    public IEnumerable<ushort> RequiredRegistries => Enumerable.Empty<ushort>();



    /// <inheritdoc />
    public void PostRegister(ObjectRegistryPhase phase)
    {
        if(phase != ObjectRegistryPhase.Main)
            return;
        
        UiHandler.CreateRootElements();
    }

    /// <inheritdoc />
    public void UnRegister(Identification objectId)
    {
        if (Engine.HeadlessModeActive)
            return;
        UiHandler.RemoveElement(objectId);
    }

    /// <inheritdoc />
    public void Clear()
    {
        UiHandler.Clear();
    }

    /// <summary>
    ///     Register a ui root element
    /// This method is used by the source generator for the auto registry
    /// </summary>
    /// <param name="id"></param>
    [RegisterMethod(ObjectRegistryPhase.Main)]
    public void RegisterUiRoot<TRootElement>(Identification id) where TRootElement : Element, IRootElement
    {
        if (Engine.HeadlessModeActive)
            return;
        UiHandler.AddRootElement<TRootElement>(id);
    }
    
}