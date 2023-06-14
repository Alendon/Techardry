using System.IO.Compression;
using System.Numerics;
using FontStashSharp;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Network;
using MintyCore.Render;
using MintyCore.Utils;
using MintyCore.Utils.Maths;
using Silk.NET.Vulkan;
using Techardry.Components.Client;
using Techardry.Entities;
using Techardry.Identifications;
using Techardry.Registries;
using Techardry.Render;
using Techardry.UI;
using Techardry.Voxels;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;
using TextureIDs = Techardry.Identifications.TextureIDs;

namespace Techardry;

[RootMod]
[UsedImplicitly]
public partial class TechardryMod : IMod
{
    public ushort ModId { get; set; }
    public string ModName => "Techardry";

    public int ServerRenderDistance { get; set; } = 8;
    
    public static UiRenderer? MainUiRenderer { get; private set; }
    public static FontSystem? FontSystem { get; private set; }

    public void Dispose()
    {
        Logger.WriteLog("Disposing TechardryMod", LogImportance.Info, "Techardry");
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
        Logger.WriteLog("Loading TechardryMod", LogImportance.Info, "Techardry");

        Voxels.RenderObjects.CreateRenderDescriptorLayout();

        InternalRegister();

        PlayerHandler.OnPlayerReady += (player, serverSide) =>
        {
            if (!serverSide) return;
            var found = WorldHandler.TryGetWorld(GameType.Server, WorldIDs.Default, out var world);
            if (!found) throw new Exception();
            world!.EntityManager.CreateEntity(ArchetypeIDs.TestCamera, player, new Archetypes.PlayerSetup());

            /*var box = world.EntityManager.CreateEntity(ArchetypeIDs.TestRender, null);
            var render = world.EntityManager.GetComponent<InstancedRenderAble>(box);
            render.MaterialMeshCombination = InstancedRenderDataIDs.DualBlock;
            world.EntityManager.SetComponent(box, render);
            var scale = world.EntityManager.GetComponent<Scale>(box);
            scale.Value = new Vector3(16, 16, 16);
            world.EntityManager.SetComponent(box, scale);*/
        };
    }

    private static void RunMainMenu()
    {
        Engine.Timer.Reset();
        Element? mainMenu = null;
        while (Engine.Window is not null && Engine.Window.Exists)
        {
            Engine.Timer.Tick();

            Engine.Window.DoEvents();

            if (mainMenu is null)
            {
                mainMenu = UiHandler.GetRootElement(UiIDs.MainMenu) as Element;
                mainMenu!.Initialize();
                mainMenu.IsActive = true;
                MainUiRenderer?.SetUiContext(mainMenu);
            }

            if (Engine.Timer.GameUpdate(out var deltaTime))
            {
                Engine.DeltaTime = deltaTime;
                UiHandler.Update();
            }

            if (!Engine.Timer.RenderUpdate(out _) || !VulkanEngine.PrepareDraw()) continue;

            MainUiRenderer?.DrawUi();

            VulkanEngine.EndDraw();
        }

        MainUiRenderer?.SetUiContext(null);
    }

    private static void RunHeadless()
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
            WorldHandler.UpdateWorlds(GameType.Server, simulationEnable, false);


            WorldHandler.SendEntityUpdates();

            NetworkHandler.Update();

            Logger.AppendLogToFile();
            if (simulationEnable)
                Engine.Tick++;
        }

        Engine.CleanupGame();
    }

    /// <summary>
    ///     The main game loop
    /// </summary>
    public static void GameLoop()
    {
        //If this is a client game (client or local) wait until the player is connected
        while (MathHelper.IsBitSet((int)Engine.GameType, (int)GameType.Client) &&
               PlayerHandler.LocalPlayerGameId == Constants.InvalidId)
            NetworkHandler.Update();

        Engine.DeltaTime = 0;
        Engine.Timer.Reset();
        while (Engine.Stop == false)
        {
            Engine.Timer.Tick();
            Engine.Window!.DoEvents();

            var drawingEnable = Engine.Timer.RenderUpdate(out var renderDeltaTime) && VulkanEngine.PrepareDraw();

            var simulationEnable = Engine.Timer.GameUpdate(out var deltaTime);


            Engine.DeltaTime = deltaTime;
            Engine.RenderDeltaTime = renderDeltaTime;

            WorldHandler.UpdateWorlds(GameType.Local, simulationEnable, drawingEnable);


            if (drawingEnable)
            {
                MainUiRenderer?.DrawUi();
                VulkanEngine.EndDraw();
            }

            WorldHandler.SendEntityUpdates();

            NetworkHandler.Update();


            Logger.AppendLogToFile();
            if (simulationEnable)
                Engine.Tick++;
        }
        
        MainUiRenderer?.SetUiContext(null);

        Engine.CleanupGame();
    }

    public void PostLoad()
    {
        if (!Engine.HeadlessModeActive)
        {
            MainUiRenderer = new UiRenderer();
        }
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
        
        InternalUnregister();
        Voxels.RenderObjects.DestroyRenderDescriptorLayout();
    }

    public static TechardryMod? Instance { get; private set; }
}