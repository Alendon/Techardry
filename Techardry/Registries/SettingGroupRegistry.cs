using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Modding.Implementations;
using MintyCore.Utils;
using Techardry.Identifications;
using Techardry.Settings;

namespace Techardry.Registries;

[Registry("setting_group")]
public class SettingGroupRegistry(ISettingsManager settingsManager) : IRegistry
{
    public ushort RegistryId => RegistryIDs.SettingGroup;
    public IEnumerable<ushort> RequiredRegistries => Enumerable.Empty<ushort>();
    
    [RegisterMethod(ObjectRegistryPhase.Main)]
    public void RegisterSettingGroup(Identification groupId, SettingGroupDescription description)
    {
        settingsManager.AddSettingGroup(groupId, description);
    }

    public void PostRegister(ObjectRegistryPhase currentPhase)
    {
        if(currentPhase == ObjectRegistryPhase.Main)
            settingsManager.ApplySettingGroups();
    }

    public void PostUnRegister()
    {
        settingsManager.ApplySettingGroups();
    }

    public void UnRegister(Identification objectId)
    {
        settingsManager.RemoveSettingGroup(objectId);
    }

    public void Clear()
    {
        
    }
}