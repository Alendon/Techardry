using ENet;
using MintyCore;
using MintyCore.ECS;
using MintyCore.GameStates;
using MintyCore.Graphics;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Modding;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;

namespace Techardry.GameStates;

public class ClientGameState(
    IGameTimer gameTimer,
    IRenderManager renderManager,
    IEngineConfiguration engineConfiguration,
    IPlayerHandler playerHandler,
    IModManager modManager,
    IWorldHandler worldHandler,
    INetworkHandler networkHandler,
    IWindowHandler windowHandler,
    IVulkanEngine vulkanEngine) : GameState<ClientGameState.InitializeParameters>
{
    private InitializeParameters? _initializeParameters;
    
    public override void Initialize(InitializeParameters parameters)
    {
        _initializeParameters = parameters;
        
        engineConfiguration.SetGameType(GameType.Client);

        playerHandler.LocalPlayerId = parameters.PlayerId;
        playerHandler.LocalPlayerName = parameters.PlayerName;
        
        renderManager.StartRendering();
        renderManager.MaxFrameRate = 60;
        
        var address = new Address() { Port = parameters.TargetPort };
        
        if(!address.SetHost(parameters.TargetHost) &&
           !address.SetIP(parameters.TargetHost))
            throw new ArgumentException("Invalid host or IP address");

        networkHandler.ConnectToServer(address);
        
        while (playerHandler.LocalPlayerGameId == Constants.InvalidId)
        {
            networkHandler.Update();
            Thread.Sleep(TimeSpan.FromMilliseconds(10));
        }
        
        gameTimer.Reset();
    }

    public override void Update()
    {
        gameTimer.Update();
        
        windowHandler.GetMainWindow().DoEvents(gameTimer.DeltaTime);
        
        worldHandler.UpdateWorlds(GameType.Client, gameTimer.IsSimulationTick);
        worldHandler.SendEntityUpdates();
        
        networkHandler.Update();
    }

    public override void Cleanup(bool restorable)
    {
        if (!restorable) _initializeParameters = null;
        
        worldHandler.DestroyWorlds(GameType.Client);

        networkHandler.StopClient();

        renderManager.StopRendering();
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

    public record InitializeParameters(ulong PlayerId, string PlayerName, string TargetHost, ushort TargetPort);
    
    [RegisterGameState("client_game")] public static GameStateDescription<ClientGameState> Description => new();

}