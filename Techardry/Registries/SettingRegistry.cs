using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Modding.Implementations;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Settings;

namespace Techardry.Registries;

[Registry("setting")]
public class SettingRegistry(ISettingsManager settingsManager) : IRegistry
{
    public ushort RegistryId => RegistryIDs.Setting;
    public IEnumerable<ushort> RequiredRegistries => [RegistryIDs.SettingGroup];


    [RegisterMethod(ObjectRegistryPhase.Main)]
    public void RegisterSetting(Identification id, SettingDescription description)
    {
        settingsManager.AddSetting(id, description);
    }

    public void UnRegister(Identification objectId)
    {
        settingsManager.RemoveSetting(objectId);
    }

    public void Clear()
    {
    }
}