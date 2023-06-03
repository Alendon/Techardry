using MintyCore.Registries;
using Techardry.Registries;

namespace Techardry.UI;

public class Prefabs
{
    [RegisterUiPrefab("main_menu_prefab")]
    internal static PrefabElementInfo MainMenuPrefabElement => new()
    {
        PrefabCreator = () => new MainMenu()
    };
}