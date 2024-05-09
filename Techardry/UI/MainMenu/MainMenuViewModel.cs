using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using MintyCore.GameStates;
using MintyCore.Registries;
using MintyCore.UI;
using Techardry.GameStates;
using Techardry.Identifications;

namespace Techardry.UI.MainMenu;

[RegisterViewModel("main_menu")]
public partial class MainMenuViewModel(IGameStateMachine stateMachine) : ViewModel
{
    protected override Task LoadAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Singleplayer()
    {
        if (Design.IsDesignMode) return;

        stateMachine.PushGameState(GameStateIDs.LocalGame, new LocalGameState.InitializeParameters(1, "Alendon"));
    }

    [RelayCommand]
    private async Task Multiplayer()
    {
        if (Design.IsDesignMode) return;

        await Navigator.NavigateTo(ViewIDs.Multiplayer);
    }

    [RelayCommand]
    private void Options()
    {
        if (Design.IsDesignMode) return;
    }

    [RelayCommand]
    private void Exit()
    {
        if (Design.IsDesignMode) return;
        Navigator.Quit();
    }
}