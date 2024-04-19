using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MintyCore.Registries;
using MintyCore.UI;
using Techardry.Identifications;

namespace Techardry.UI.MainMenu;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    [RegisterView("main")] internal static ViewDescription<MainView> viewDescription => new(ViewModelIDs.Main);
}