using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MintyCore.Registries;
using MintyCore.UI;
using Techardry.Identifications;

namespace Techardry.UI.MainMenu;

public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        InitializeComponent();
    }

    [RegisterView("main_menu")]
    internal static ViewDescription<MainMenuView> viewDescription => new(ViewModelIDs.MainMenu);
}