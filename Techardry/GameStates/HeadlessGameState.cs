using MintyCore;
using MintyCore.ECS;
using MintyCore.GameStates;
using MintyCore.Modding;
using MintyCore.Network;
using MintyCore.Registries;
using MintyCore.Utils;

namespace Techardry.GameStates;

public class HeadlessGameState(
    IGameTimer gameTimer,
    IEngineConfiguration engineConfiguration,
    IModManager modManager,
    IWorldHandler worldHandler,
    INetworkHandler networkHandler) : GameState
{
    public override void Initialize()
    {
        engineConfiguration.SetGameType(GameType.Server);
        
        modManager.LoadGameMods(modManager.GetAvailableMods(true).Where(x => !x.IsRootMod));

        networkHandler.StartServer(Constants.DefaultPort, 16);

        worldHandler.CreateWorlds(GameType.Server);
        
        gameTimer.Reset();
    }

    public override void Update()
    {
        gameTimer.Update();
        
        worldHandler.UpdateWorlds(GameType.Server, gameTimer.IsSimulationTick);
        worldHandler.SendEntityUpdates();
        
        networkHandler.Update();
    }

    public override void Cleanup(bool restorable)
    {
        worldHandler.DestroyWorlds(GameType.Server);

        networkHandler.StopServer();

        modManager.UnloadMods(false);

        engineConfiguration.SetGameType(GameType.None);
    }

    public override void Restore()
    {
        Initialize();
    }
    
    [RegisterGameState("headless")] public static GameStateDescription<HeadlessGameState> Description => new();

}