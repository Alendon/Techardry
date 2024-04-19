using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using MintyCore.Registries;
using MintyCore.UI;

namespace Techardry.UI.MainMenu;

[RegisterViewModel("main_menu")]
public partial class MainMenuViewModel : ViewModel
{
    protected override Task LoadAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    public void Options(Control control)
    {
        var popup = new Popup()
        {
            PlacementTarget = control,
            Placement = PlacementMode.Center,
            IsLightDismissEnabled = true,
            Child = new TextBlock()
            {
                Text = "Options not implemented yet.",
                Background = Brushes.White,
                Foreground = Brushes.Black,
            }
        };

        popup.Open();
    }
    
}