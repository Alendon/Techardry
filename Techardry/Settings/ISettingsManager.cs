using MintyCore.Utils;

namespace Techardry.Settings;

public interface ISettingsManager
{
    void AddSetting(Identification settingId, SettingDescription description);
    void AddSettingGroup(Identification groupId, SettingGroupDescription description);
    void ApplySettingGroups();

    void SetSetting<TValue>(Identification settingId, TValue value);
    TValue GetSetting<TValue>(Identification settingId);

    void RemoveSetting(Identification settingId);
    void RemoveSettingGroup(Identification groupId);
}

public record struct SettingDescription;
public record struct SettingGroupDescription;