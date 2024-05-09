using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MintyCore.Registries;
using MintyCore.UI;
using Techardry.Identifications;

namespace Techardry.UI.MainMenu;

public partial class MultiplayerView : UserControl
{
    public MultiplayerView()
    {
        InitializeComponent();
    }

    [RegisterView("multiplayer")]
    internal static ViewDescription<MultiplayerView> View => new(ViewModelIDs.Multiplayer);
}