﻿using System.Numerics;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Modding;
using MintyCore.Network;
using MintyCore.Render;
using MintyCore.Utils;
using MintyCore.Utils.Maths;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Registries;
using Techardry.Render;
using Techardry.UI;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;
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
    public required IUiHandler UiHandler { [UsedImplicitly] init; private get; }

    public int ServerRenderDistance { get; set; } = 2;
    
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

        Voxels.RenderObjects.CreateRenderDescriptorLayout(VulkanEngine);
        
        PlayerHandler.OnPlayerReady += (player, serverSide) =>
        {
            if (!serverSide) return;
            var found = WorldHandler.TryGetWorld(GameType.Server, WorldIDs.Default, out var world);
            if (!found) throw new Exception();
            var playerEntity = world!.EntityManager.CreateEntity(ArchetypeIDs.TestCamera, player);
            world.EntityManager.GetComponent<Position>(playerEntity).Value = new Vector3(0, 20, 0);
        };
    }

    private void RunMainMenu()
    {
        Engine.Timer.Reset();
        Element? mainMenu = null;
        while (Engine.Window is not null && Engine.Window.Exists)
        {
            Engine.Timer.Tick();
            
            if (mainMenu is null)
            {
                mainMenu = UiHandler.GetRootElement(UiIDs.MainMenu) as Element;
                mainMenu!.Initialize();
                mainMenu.IsActive = true;
            }

            if (Engine.Timer.GameUpdate(out var deltaTime))
            {
                Engine.DeltaTime = deltaTime;
                UiHandler.Update();
            }

            if (!Engine.Timer.RenderUpdate(out _) || !VulkanEngine.PrepareDraw()) continue;
            
            VulkanEngine.EndDraw();
            Engine.Window.DoEvents(Engine.DeltaTime);

        }
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
    public void GameLoop()
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
            var simulationEnable = Engine.Timer.GameUpdate(out var deltaTime);

            Engine.Window!.DoEvents(deltaTime);

            Engine.DeltaTime = deltaTime;

            WorldHandler.UpdateWorlds(GameType.Local, simulationEnable);
            

            WorldHandler.SendEntityUpdates();

            NetworkHandler.Update();


            Logger.AppendLogToFile();
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