using System.Numerics;
using JetBrains.Annotations;
using MintyCore;
using MintyCore.Components.Client;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Identifications;
using MintyCore.Modding;
using MintyCore.Modding.Attributes;
using MintyCore.Render;
using MintyCore.Utils;
using ArchetypeIDs = Techardry.Identifications.ArchetypeIDs;

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
            position.Value = new Vector3(0, 0, -64);
            world.EntityManager.SetComponent(entity, position);

            var box = world.EntityManager.CreateEntity(ArchetypeIDs.TestRender, null);
            var render = world.EntityManager.GetComponent<InstancedRenderAble>(box);
            render.MaterialMeshCombination = InstancedRenderDataIDs.Testing;
            world.EntityManager.SetComponent(box, render);
            var scale = world.EntityManager.GetComponent<Scale>(box);
            scale.Value = new Vector3(32, 32, 32);
            world.EntityManager.SetComponent(box, scale);
        };
    }

    public void PostLoad()
    {
    }

    public void Unload()
    {
        InternalUnregister();
    }

    public static TechardryMod? Instance { get; private set; }
    
}