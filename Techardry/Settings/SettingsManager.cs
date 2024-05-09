using MintyCore.Utils;

namespace Techardry.Settings;

[Singleton<ISettingsManager>]
public class SettingsManager : ISettingsManager
{
    public void AddSetting(Identification settingId, SettingDescription description)
    {
        throw new NotImplementedException();
    }

    public void AddSettingGroup(Identification groupId, SettingGroupDescription description)
    {
        throw new NotImplementedException();
    }

    public void ApplySettingGroups()
    {
        //TODO Implement
    }

    public void SetSetting<TValue>(Identification settingId, TValue value)
    {
        throw new NotImplementedException();
    }

    public TValue GetSetting<TValue>(Identification settingId)
    {
        throw new NotImplementedException();
    }

    public void RemoveSetting(Identification settingId)
    {
        throw new NotImplementedException();
    }

    public void RemoveSettingGroup(Identification groupId)
    {
        throw new NotImplementedException();
    }
}