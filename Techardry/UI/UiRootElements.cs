using MintyCore.Registries;
using Techardry.Identifications;

namespace Techardry.UI;

internal static class UiRootElements
{
    [RegisterUiRoot("main_menu")]
    internal static RootElementInfo MainMenuRoot => new()
    {
        RootElementPrefab = UiIDs.MainMenuPrefab
    };
}