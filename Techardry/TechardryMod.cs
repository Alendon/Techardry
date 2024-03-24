using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics;
using MintyCore.Graphics.Managers;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Modding;
using MintyCore.Network;
using MintyCore.UI;
using MintyCore.Utils;
using MintyCore.Utils.Maths;
using Myra;
using Serilog;
using Serilog.Core;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Registries;
using Techardry.UI;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;
using Constants = MintyCore.Utils.Constants;
using TextureIDs = Techardry.Identifications.TextureIDs;

namespace Techardry;

[UsedImplicitly]
public sealed class TechardryMod : IMod
{
    public ushort ModId { get; set; }
    private string ModName => "Techardry";
    public required IVulkanEngine VulkanEngine { [UsedImplicitly] init; private get; }
    public required IPlayerHandler PlayerHandler { [UsedImplicitly] init; private get; }
    public required IWorldHandler WorldHandler { [UsedImplicitly] init; private get; }
    public required INetworkHandler NetworkHandler { [UsedImplicitly] init; private get; }
    public required IModManager ModManager { [UsedImplicitly] init; private get; }
    public required IRenderManager RenderManager { [UsedImplicitly] init; private get; }
    public required ITextureManager TextureManager { [UsedImplicitly] init; private get; }

    public int ServerRenderDistance { get; set; } = 2;

    public void Dispose()
    {
        Log.Information("Disposing TechardryMod");
    }

    public void PreLoad()
    {
        Engine.Timer.TargetTicksPerSecond = 60;
        Engine.Timer.TargetFps = int.MaxValue;
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
        MainMenu? mainMenu = null;
        while (!Engine.Stop)
        {
            if (Engine.Desktop?.Root is null)
            {
                mainMenu = new MainMenu();
                Engine.Desktop!.Root = mainMenu;
            }

            Engine.Timer.Tick();


            if (Engine.Timer.GameUpdate(out var deltaTime))
            {
                Engine.DeltaTime = deltaTime;
            }

            Engine.Desktop.Render();

            if (mainMenu?.Quit is true)
            {
                Engine.ShouldStop = true;
                break;
            }

            var cb = VulkanEngine.GetSingleTimeCommandBuffer();
            TextureManager.ApplyChanges(cb);
            VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

            var renderer = (IUiRenderer)MyraEnvironment.Platform.Renderer;
            renderer.ApplyRenderData();


            Engine.Window!.DoEvents(Engine.DeltaTime);

            if (mainMenu?.PlayLocal is true)
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
        Engine.Desktop!.Root = null;
        
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

            if (sw.Elapsed.TotalSeconds > 1)
            {
                Log.Debug("Current FPS: {Fps}", RenderManager.FrameRate);
                sw.Restart();
            }
            
            Engine.Desktop?.Render();
            
            var cb = VulkanEngine.GetSingleTimeCommandBuffer();
            TextureManager.ApplyChanges(cb);
            VulkanEngine.ExecuteSingleTimeCommandBuffer(cb);

            var renderer = (IUiRenderer)MyraEnvironment.Platform.Renderer;
            renderer.ApplyRenderData();

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