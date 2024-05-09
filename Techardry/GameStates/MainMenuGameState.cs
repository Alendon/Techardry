using MintyCore;
using MintyCore.GameStates;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Input;
using MintyCore.Registries;
using MintyCore.UI;
using MintyCore.Utils;
using Techardry.Identifications;
using RenderModuleIDs = MintyCore.Identifications.RenderModuleIDs;

namespace Techardry.GameStates;

public class MainMenuGameState(
    IGameTimer gameTimer,
    IWindowHandler windowHandler,
    IRenderManager renderManager,
    IViewLocator viewLocator,
    IInputHandler inputHandler,
    IRenderModuleManager renderModuleManager) : GameState
{
    public override void Initialize()
    {
        gameTimer.Reset();

        renderModuleManager.SetModuleActive(RenderModuleIDs.AvaloniaUi, true);

        viewLocator.SetRootView(ViewIDs.Main);
        
        renderManager.StartRendering();
        renderManager.MaxFrameRate = 60;


        inputHandler.InputConsumer = InputConsumer.Avalonia;
    }

    public override void Update()
    {
        gameTimer.Update();
        windowHandler.GetMainWindow().DoEvents(gameTimer.DeltaTime);
    }

    public override void Cleanup(bool restorable)
    {
        renderModuleManager.SetModuleActive(RenderModuleIDs.AvaloniaUi, false);
        renderManager.StopRendering();
        
        viewLocator.ClearRootView();
        
        gameTimer.Reset();
    }

    public override void Restore()
    {
        Initialize();
    }

    [RegisterGameState("main_menu")] public static GameStateDescription<MainMenuGameState> Description => new();
}