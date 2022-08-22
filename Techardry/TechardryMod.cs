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
using Silk.NET.Vulkan;
using Techardry.Registries;
using Techardry.World;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;
using InstancedRenderDataIDs = Techardry.Identifications.InstancedRenderDataIDs;
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
    public ModVersion ModVersion => new(0,0,1);
    public ModDependency[] ModDependencies => Array.Empty<ModDependency>();
    public GameType ExecutionSide => GameType.Local;
    
    public void Dispose()
    {
        Logger.WriteLog("Disposing TechardryMod", LogImportance.Info, "Techardry");
    }

    public void PreLoad()
    {
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
    public static TextureAtlasInfo BlockTextureAtlas => new (new[]
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

    public void Unload()
    {
        InternalUnregister();
    }

    public static TechardryMod? Instance { get; private set; }
    
}