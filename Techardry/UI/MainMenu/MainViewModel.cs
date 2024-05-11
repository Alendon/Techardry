using Avalonia.Controls;
using MintyCore;
using MintyCore.GameStates;
using MintyCore.Registries;
using MintyCore.UI;
using MintyCore.Utils;
using Techardry.Identifications;

namespace Techardry.UI.MainMenu;

[RegisterViewModel("main")]
public class MainViewModel : ViewModelNavigator
{
    public required IGameStateMachine GameStateMachine { init; private get; }
    
    public MainViewModel(IViewLocator viewLocator) : base(viewLocator)
    {
    }


    protected override async Task LoadAsync()
    {
        await NavigateTo(ViewIDs.MainMenu);
    }

    public override void Quit()
    {
        GameStateMachine.Stop();
    }
}