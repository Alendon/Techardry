using Techardry.Registries;
using Techardry.UI.Elements;

namespace Techardry.UI;

public static class Prefabs
{
    [RegisterUiPrefab("main_menu_prefab")]
    internal static PrefabElementInfo MainMenuPrefabElement => new()
    {
        PrefabCreator = () => new MainMenu()
    };
}