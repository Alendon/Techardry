using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MintyCore.Registries;
using MintyCore.UI;
using Techardry.Identifications;

namespace Techardry.UI.InGame;

public partial class UiOverlayView : UserControl
{
    public UiOverlayView()
    {
        InitializeComponent();
    }
    
    [RegisterView("ui_overlay")]
    internal static ViewDescription<UiOverlayView> View => new(ViewModelIDs.UiOverlay);
}