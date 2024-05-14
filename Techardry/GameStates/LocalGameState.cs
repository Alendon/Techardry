using ENet;
using MintyCore;
using MintyCore.ECS;
using MintyCore.GameStates;
using MintyCore.Graphics;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Input;
using MintyCore.Modding;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.UI;
using MintyCore.Utils;
using Techardry.Identifications;
using RenderModuleIDs = MintyCore.Identifications.RenderModuleIDs;

namespace Techardry.GameStates;

public class LocalGameState(
    IGameTimer gameTimer,
    IRenderManager renderManager,
    IEngineConfiguration engineConfiguration,
    IPlayerHandler playerHandler,
    IModManager modManager,
    IWorldHandler worldHandler,
    INetworkHandler networkHandler,
    IWindowHandler windowHandler,
    IVulkanEngine vulkanEngine,
    IRenderModuleManager renderModuleManager,
    IInputHandler inputHandler,
    IViewLocator viewLocator) : GameState<LocalGameState.InitializeParameters>
{
    private InitializeParameters? _initializeParameters;
    
    public override void Initialize(InitializeParameters parameters)
    {
        _initializeParameters = parameters;
        
        engineConfiguration.SetGameType(GameType.Local);

        playerHandler.LocalPlayerId = parameters.PlayerId;
        playerHandler.LocalPlayerName = parameters.PlayerName;

        modManager.LoadGameMods(modManager.GetAvailableMods(true).Where(x => !x.IsRootMod));

        networkHandler.StartServer(Constants.DefaultPort, 16);

        var address = new Address() { Port = Constants.DefaultPort };
        address.SetHost("localhost");

        networkHandler.ConnectToServer(address);

        worldHandler.CreateWorlds(GameType.Local);

        while (playerHandler.LocalPlayerGameId == Constants.InvalidId)
        {
            networkHandler.Update();
            Thread.Sleep(TimeSpan.FromMilliseconds(10));
        }

        viewLocator.SetRootView(ViewIDs.UiOverlay);
        
        renderModuleManager.SetModuleActive(RenderModuleIDs.AvaloniaUi, true);
        renderModuleManager.SetModuleActive(Identifications.RenderModuleIDs.World, true);
       
        renderManager.MaxFrameRate = int.MaxValue;
        renderManager.StartRendering();
        
        gameTimer.SetTargetTicksPerSecond(60);
        gameTimer.Reset();

        inputHandler.InputConsumer = InputConsumer.InputActions;
    }

    public override void Update()
    {
        gameTimer.Update();
        
        windowHandler.GetMainWindow().DoEvents(gameTimer.DeltaTime);
        
        worldHandler.UpdateWorlds(GameType.Local, gameTimer.IsSimulationTick);
        worldHandler.SendEntityUpdates();
        
        networkHandler.Update();
    }

    public override void Cleanup(bool restorable)
    {
        if (!restorable) _initializeParameters = null;
        
        windowHandler.GetMainWindow().MouseLocked = false;
        
        renderManager.StopRendering();
        renderModuleManager.SetModuleActive(Identifications.RenderModuleIDs.World, false);
        
        worldHandler.DestroyWorlds(GameType.Local);

        networkHandler.StopClient();
        networkHandler.StopServer();
        
        vulkanEngine.WaitForAll();

        modManager.UnloadMods(false);

        playerHandler.LocalPlayerId = Constants.InvalidId;
        playerHandler.LocalPlayerName = string.Empty;

        engineConfiguration.SetGameType(GameType.None);
    }

    public override void Restore()
    {
        Initialize(_initializeParameters ?? throw new InvalidOperationException());
    }

    public record InitializeParameters(ulong PlayerId, string PlayerName);

    [RegisterGameState("local_game")] public static GameStateDescription<LocalGameState> Description => new();
}