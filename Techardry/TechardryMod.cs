using System.Diagnostics;
using System.Numerics;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Modding;
using MintyCore.Network;
using MintyCore.UI;
using MintyCore.Utils;
using MintyCore.Utils.Maths;
using Serilog;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Registries;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;
using Constants = MintyCore.Utils.Constants;
using TextureIDs = Techardry.Identifications.TextureIDs;

namespace Techardry;

[UsedImplicitly]
public sealed class TechardryMod : IMod
{
    private string ModName => "Techardry";
    public required IVulkanEngine VulkanEngine { [UsedImplicitly] init; private get; }
    public required IPlayerHandler PlayerHandler { [UsedImplicitly] init; private get; }
    public required IWorldHandler WorldHandler { [UsedImplicitly] init; private get; }
    public required INetworkHandler NetworkHandler { [UsedImplicitly] init; private get; }
    public required IModManager ModManager { [UsedImplicitly] init; private get; }
    public required IRenderManager RenderManager { [UsedImplicitly] init; private get; }
    public required IViewLocator ViewLocator { [UsedImplicitly] init; private get; }

    public int ServerRenderDistance { get; set; } = 2;

    public void Dispose()
    {
        Log.Information("Disposing TechardryMod");
    }

    public void PreLoad()
    {
        Engine.Timer.TargetTicksPerSecond = 60;
        Instance = this;
        VulkanEngine.AddDeviceExtension(ModName, "VK_KHR_shader_non_semantic_info", true);
        VulkanEngine.AddDeviceFeatureExension(new PhysicalDeviceVulkan12Features()
        {
            SType = StructureType.PhysicalDeviceVulkan12Features,
            RuntimeDescriptorArray = true,
            DescriptorBindingPartiallyBound = true,
            ShaderStorageBufferArrayNonUniformIndexing = true,
            DescriptorBindingVariableDescriptorCount = true,
        });

        Engine.RunMainMenu = RunMainMenu;
        Engine.RunHeadless = RunHeadless;
    }

    public void Load()
    {
        Log.Information("Loading TechardryMod");

        Voxels.RenderObjects.CreateRenderDescriptorLayout(VulkanEngine);

        PlayerHandler.OnPlayerReady += (player, serverSide) =>
        {
            if (!serverSide) return;
            var found = WorldHandler.TryGetWorld(GameType.Server, WorldIDs.TechardryWorld, out var world);
            if (!found) throw new Exception();
            var playerEntity = world!.EntityManager.CreateEntity(ArchetypeIDs.TestCamera, player);
            world.EntityManager.GetComponent<Position>(playerEntity).Value = new Vector3(0, 20, 0);
        };
    }

    private void RunMainMenu()
    {
        Engine.Timer.Reset();

        RenderManager.StartRendering();
        RenderManager.MaxFrameRate = 100;

        ViewLocator.SetRootView(ViewIDs.Main);
        
        while (!Engine.Stop)
        {
            Engine.Timer.Tick();


            if (Engine.Timer.GameUpdate(out var deltaTime))
            {
                Engine.DeltaTime = deltaTime;
            }
            
            if (/*mainMenu?.Quit is true*/ false)
            {
                Engine.ShouldStop = true;
                break;
            }

            Engine.Window!.DoEvents(Engine.DeltaTime);

            if (false) // mainMenu?.PlayLocal is true)
            {
                Engine.SetGameType(GameType.Local);

                PlayerHandler.LocalPlayerId = 1;
                PlayerHandler.LocalPlayerName = "Alendon";

                Engine.LoadMods(ModManager.GetAvailableMods(true));

                WorldHandler.CreateWorlds(GameType.Server);

                Engine.CreateServer(Constants.DefaultPort);
                Engine.ConnectToServer("localhost", Constants.DefaultPort);

                GameLoop();
            }
        }

        RenderManager.StopRendering();
    }

    private void RunHeadless()
    {
        Engine.SetGameType(GameType.Server);
        Engine.LoadMods(ModManager.GetAvailableMods(true));
        WorldHandler.CreateWorlds(GameType.Server);
        Engine.CreateServer(Engine.HeadlessPort);

        Engine.Timer.Reset();
        while (Engine.Stop == false)
        {
            Engine.Timer.Tick();

            var simulationEnable = Engine.Timer.GameUpdate(out var deltaTime);

            Engine.DeltaTime = deltaTime;
            WorldHandler.UpdateWorlds(GameType.Server, simulationEnable);


            WorldHandler.SendEntityUpdates();

            NetworkHandler.Update();

            if (simulationEnable)
                Engine.Tick++;
        }

        Engine.CleanupGame();
    }

    /// <summary>
    ///     The main game loop
    /// </summary>
    public void GameLoop()
    {
        //Engine.Desktop!.Root = new IngameUi(ModManager);
        
        //If this is a client game (client or local) wait until the player is connected
        while (MathHelper.IsBitSet((int)Engine.GameType, (int)GameType.Client) &&
               PlayerHandler.LocalPlayerGameId == Constants.InvalidId)
        {
            NetworkHandler.Update();
            Thread.Sleep(10);
        }

        Engine.DeltaTime = 0;
        Engine.Timer.Reset();
        
        Stopwatch sw = Stopwatch.StartNew();
        
        while (Engine.Stop == false)
        {
            Engine.Timer.Tick();
            var simulationEnable = Engine.Timer.GameUpdate(out var deltaTime);

            Engine.Window!.DoEvents(deltaTime);

            Engine.DeltaTime = deltaTime;

            WorldHandler.UpdateWorlds(GameType.Local, simulationEnable);
            WorldHandler.SendEntityUpdates();
            
            NetworkHandler.Update();

            if (false) //sw.Elapsed.TotalSeconds > 1 && Engine.Desktop?.Root is IngameUi ui)
            {
                //ui.SetFps(RenderManager.FrameRate);
                sw.Restart();
            }

            if (simulationEnable)
                Engine.Tick++;
        }

        Engine.CleanupGame();
    }

    public void PostLoad()
    {
    }

    [RegisterTextureAtlas("block_texture")]
    public static TextureAtlasInfo BlockTextureAtlas => new(new[]
    {
        TextureIDs.Dirt, TextureIDs.Stone
    });

    public void Unload()
    {
        if (!Engine.HeadlessModeActive)
        {
            VulkanEngine.WaitForAll();
        }

        Voxels.RenderObjects.DestroyRenderDescriptorLayout(VulkanEngine);
    }

    public static TechardryMod? Instance { get; private set; }
}