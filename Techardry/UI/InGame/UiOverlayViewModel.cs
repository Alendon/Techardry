using System.Numerics;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MintyCore.Registries;
using MintyCore.UI;

namespace Techardry.UI.InGame;

[RegisterViewModel("ui_overlay")]
public partial class UiOverlayViewModel : ViewModelNavigator
{
    [ObservableProperty] private Vector3 _playerPosition;
    [ObservableProperty] private Vector3 _lookingAt;
    [ObservableProperty] private float _blockSize;
    [ObservableProperty] private int _fps;
    [ObservableProperty] private string _currentHeldBlock = string.Empty;
    [ObservableProperty] private string _currentLookingAtBlock = string.Empty;

    public string PlayerPositionText =>
        $"Player Position: {PlayerPosition.X:F2}, {PlayerPosition.Y:F2}, {PlayerPosition.Z:F2}";

    public string LookingAtText => $"Looking At: {LookingAt.X:F2}, {LookingAt.Y:F2}, {LookingAt.Z:F2}";
    public string BlockSizeText => $"Block Size: {BlockSize:G2}m";
    public string FpsText => $"FPS: {Fps}";
    public string CurrentHeldBlockText => $"Current Held Block: {CurrentHeldBlock}";
    public string CurrentLookingAtBlockText => $"Current Looking At Block: {CurrentLookingAtBlock}";


    protected override Task LoadAsync()
    {
        return Task.CompletedTask;
    }

    public UiOverlayViewModel(IViewLocator viewLocator) : base(viewLocator)
    {
        PropertyChanged += (_, a) =>
        {
            var changedTextProperty = a.PropertyName switch
            {
                nameof(PlayerPosition) => nameof(PlayerPositionText),
                nameof(LookingAt) => nameof(LookingAtText),
                nameof(BlockSize) => nameof(BlockSizeText),
                nameof(Fps) => nameof(FpsText),
                nameof(CurrentHeldBlock) => nameof(CurrentHeldBlockText),
                nameof(CurrentLookingAtBlock) => nameof(CurrentLookingAtBlockText),
                _ => null
            };

            if (changedTextProperty is not null)
                OnPropertyChanged(changedTextProperty);
        };

        if (Design.IsDesignMode)
        {
            PlayerPosition = new Vector3(0, 0, 0);
            LookingAt = new Vector3(0, 0, 0);
            BlockSize = 1;
            Fps = 60;
            CurrentHeldBlock = "Dirt";
            CurrentLookingAtBlock = "Grass";
        }
    }


    public override void Quit()
    {
        throw new NotSupportedException();
    }
}