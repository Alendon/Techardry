using MintyCore.Registries;
using MintyCore.UI;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.UI.MainMenu;

[RegisterViewModel("main")]
public class MainViewModel : ViewModelNavigator
{
    public MainViewModel(IViewLocator viewLocator) : base(viewLocator)
    {
    }


    protected override async Task LoadAsync()
    {
        await NavigateTo(ViewIDs.MainMenu);
    }

    public override void Quit()
    {
    }
}