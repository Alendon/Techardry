using System.Numerics;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics;
using MintyCore.Modding;
using MintyCore.Utils;
using MintyCore.Utils.Events;
using Serilog;
using Silk.NET.Vulkan;
using Techardry.Identifications;
using Techardry.Registries;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;
using TextureIDs = Techardry.Identifications.TextureIDs;

namespace Techardry;

[UsedImplicitly]
public sealed class TechardryMod : IMod
{
    private string ModName => "Techardry";
    public required IVulkanEngine VulkanEngine { [UsedImplicitly] init; private get; }
    public required IWorldHandler WorldHandler { [UsedImplicitly] init; private get; }
    public required IEngineConfiguration EngineConfiguration { [UsedImplicitly] init; private get; }
    public required IGameTimer Timer { [UsedImplicitly] init; private get; }
    public required IEventBus EventBus { [UsedImplicitly] init; private get; }

    public int ServerRenderDistance { get; set; } = 2;

    public void Dispose()
    {
        Log.Information("Disposing TechardryMod");
    }

    public void PreLoad()
    {
        Timer.SetTargetTicksPerSecond(60);
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
    }

    public void Load()
    {
        Log.Information("Loading TechardryMod");

        if (!EngineConfiguration.HeadlessModeActive)
        {
            Voxels.RenderObjects.CreateRenderDescriptorLayout(VulkanEngine);
        }
    }
    
    public void PostLoad()
    {
        EngineConfiguration.DefaultGameState = GameStateIDs.MainMenu;
        EngineConfiguration.DefaultHeadlessGameState = GameStateIDs.Headless;
        
        _playerEventBinding = new EventBinding<PlayerEvent>(EventBus, OnPlayerEvent);
    }

    private EventBinding<PlayerEvent>? _playerEventBinding;
    private EventResult OnPlayerEvent(PlayerEvent e)
    {
        if (e.Type == PlayerEvent.EventType.Ready && e.ServerSide)
            CreatePlayerEntity(e.Player);

        return EventResult.Continue;
    }

    private void CreatePlayerEntity(Player player)
    {
        var found = WorldHandler.TryGetWorld(GameType.Server, WorldIDs.TechardryWorld, out var world);
        if (!found) throw new Exception();
        var playerEntity = world!.EntityManager.CreateEntity(ArchetypeIDs.TestCamera, player);
        world.EntityManager.GetComponent<Position>(playerEntity).Value = new Vector3(0, 20, 0);
    }

    


    [RegisterTextureAtlas("block_texture")]
    public static TextureAtlasInfo BlockTextureAtlas => new(new[]
    {
        TextureIDs.Dirt, TextureIDs.Stone
    });

    public void Unload()
    {
        if (!EngineConfiguration.HeadlessModeActive)
        {
            VulkanEngine.WaitForAll();
            Voxels.RenderObjects.DestroyRenderDescriptorLayout(VulkanEngine);
        }
    }

    public static TechardryMod? Instance { get; private set; }
}