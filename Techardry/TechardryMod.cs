using System.Numerics;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Identifications;
using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Registries;
using MintyCore.Render;
using MintyCore.Utils;
using Silk.NET.Input;
using Silk.NET.Vulkan;
using Techardry.Entities;
using Techardry.Registries;
using Techardry.World;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;
using MaterialIDs = Techardry.Identifications.MaterialIDs;
using PipelineIDs = Techardry.Identifications.PipelineIDs;
using TextureIDs = Techardry.Identifications.TextureIDs;

namespace Techardry;

[RootMod]
[UsedImplicitly]
public partial class TechardryMod : IMod
{
    public ushort ModId { get; set; }
    public string StringIdentifier => "techardry";
    public string ModDescription => "Techardry mod";
    public string ModName => "Techardry";
    public ModVersion ModVersion => new(0, 0, 1);
    public ModDependency[] ModDependencies => Array.Empty<ModDependency>();
    public GameType ExecutionSide => GameType.Local;

    public void Dispose()
    {
        Logger.WriteLog("Disposing TechardryMod", LogImportance.Info, "Techardry");
    }

    public void PreLoad()
    {
        Engine.Timer.TargetTicksPerSecond = 60;
        Engine.Timer.TargetFps = 60;
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
        Logger.WriteLog("Loading TechardryMod", LogImportance.Info, "Techardry");
        InternalRegister();

        PlayerHandler.OnPlayerConnected += (player, serverSide) =>
        {
            if (!serverSide) return;
            var found = WorldHandler.TryGetWorld(GameType.Server, WorldIDs.Default, out var world);
            if (!found) throw new Exception();
            var entity = world!.EntityManager.CreateEntity(ArchetypeIDs.TestCamera, player);
            var camera = world.EntityManager.GetComponent<Camera>(entity);
            camera.Forward = Vector3.Normalize(new Vector3(0f, 0, 1));
            world.EntityManager.SetComponent(entity, camera);

            var position = world.EntityManager.GetComponent<Position>(entity);
            position.Value = new Vector3(0, 32, -64);
            world.EntityManager.SetComponent(entity, position);

            /*var box = world.EntityManager.CreateEntity(ArchetypeIDs.TestRender, null);
            var render = world.EntityManager.GetComponent<InstancedRenderAble>(box);
            render.MaterialMeshCombination = InstancedRenderDataIDs.DualBlock;
            world.EntityManager.SetComponent(box, render);
            var scale = world.EntityManager.GetComponent<Scale>(box);
            scale.Value = new Vector3(16, 16, 16);
            world.EntityManager.SetComponent(box, scale);*/
        };
    }

    public void PostLoad()
    {
    }

    [RegisterTextureAtlas("block_texture")]
    public static TextureAtlasInfo BlockTextureAtlas => new(new[]
    {
        TextureIDs.Dirt, TextureIDs.Stone
    });

    [OverrideWorld("default", "minty_core")]
    public static WorldInfo TechardryWorldInfo => new()
    {
        WorldCreateFunction = serverWorld => new TechardryWorld(serverWorld),
    };

    [RegisterMaterial("dual_block")]
    public static MaterialInfo DualBlock => new()
    {
        DescriptorSets = new[]
        {
            (TextureIDs.Dirt, 1u)
        },
        PipelineId = PipelineIDs.DualTexture
    };

    [RegisterInstancedRenderData("dual_block")]
    public static InstancedRenderDataInfo DualBlockRenderData => new()
    {
        MeshId = MeshIDs.Cube,
        MaterialIds = new[]
        {
            MaterialIDs.DualBlock
        }
    };

    [RegisterKeyAction("spawn_test_cube")]
    public static KeyActionInfo SpawnTestCube => new KeyActionInfo()
    {
        Action = (state, _) =>
        {
            if (state != KeyStatus.KeyDown) return;

            if (!WorldHandler.TryGetWorld(GameType.Server, WorldIDs.Default, out var world)) return;

            world.EntityManager.CreateEntity(ArchetypeIDs.PhysicBox, null, new Archetypes.PhysicBoxSetup()
            {
                Mass = 10,
                Position = new Vector3(Random.Shared.NextSingle() * 10 - 28, 32, Random.Shared.NextSingle() * 10 - 28),
                Scale = new Vector3(1, 1, 1),
            });
        },
        Key = Key.H,
        MouseButton = null
    };

    public static int RenderMode = 3;
    
    [RegisterKeyAction("switch_render_mode")]
    public static KeyActionInfo SwitchRenderMode => new()
    {
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                RenderMode %= 3;
                RenderMode++;
            }
        },
        Key = Key.K,
        MouseButton = null
    };

    public void Unload()
    {
        InternalUnregister();
    }

    public static TechardryMod? Instance { get; private set; }
}