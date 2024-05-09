using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MintyCore.Registries;
using MintyCore.UI;

namespace Techardry.UI.MainMenu;

[RegisterViewModel("multiplayer")]
public partial class MultiplayerViewModel : ViewModel
{
    [ObservableProperty] private string _hostPort = string.Empty;

    [ObservableProperty] private string _hostAddress = string.Empty;

    [ObservableProperty] private string _playerName = string.Empty;
    [ObservableProperty] private string _playerId = string.Empty;

    [ObservableProperty] private string _createServerPort = string.Empty;

    [RelayCommand]
    private void ConnectToServer()
    {
        if (Design.IsDesignMode) return;

        if (string.IsNullOrWhiteSpace(HostPort) ||
            string.IsNullOrWhiteSpace(HostAddress) ||
            string.IsNullOrWhiteSpace(PlayerName) ||
            string.IsNullOrWhiteSpace(PlayerId)) return;
        
        
    }
    
    [RelayCommand]
    private void CreateServer()
    {
        if (Design.IsDesignMode) return;
    }

    partial void OnHostPortChanged(string? oldValue, string newValue)
    {
        if (ushort.TryParse(newValue, out _) || newValue.Length == 0) return;
        HostPort = oldValue ?? string.Empty;
    }

    partial void OnCreateServerPortChanged(string? oldValue, string newValue)
    {
        if (ushort.TryParse(newValue, out _) || newValue.Length == 0) return;
        CreateServerPort = oldValue ?? string.Empty;
    }

    partial void OnPlayerIdChanged(string? oldValue, string newValue)
    {
        if (ulong.TryParse(newValue, out _) || newValue.Length == 0) return;
        PlayerId = oldValue ?? string.Empty;
    }

    protected override Task LoadAsync()
    {
        return Task.CompletedTask;
    }
}