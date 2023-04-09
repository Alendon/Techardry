using System.Numerics;
using MintyCore;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Silk.NET.Input;
using Techardry.Entities;
using Techardry.Identifications;

namespace Techardry.Utils;

internal static class KeyActions
{
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
    
    [RegisterKeyAction("spawn_test_cube")]
    public static KeyActionInfo SpawnTestCube => new()
    {
        Action = (state, _) =>
        {
            if (state != KeyStatus.KeyDown) return;

            if (!WorldHandler.TryGetWorld(GameType.Server, WorldIDs.Default, out var world)) return;

            world.EntityManager.CreateEntity(ArchetypeIDs.PhysicBox, null, new Archetypes.PhysicBoxSetup()
            {
                Mass = 10,
                Position = new Vector3(Random.Shared.NextSingle() * 16, 32, Random.Shared.NextSingle() * 16),
                Scale = new Vector3(1, 1, 1),
            });
        },
        Key = Key.H,
        MouseButton = null
    };
    
    [RegisterKeyAction("back_to_main_menu")]
    internal static KeyActionInfo BackToMainMenu => new()
    {
        Key = Key.Escape,
        Action = (state, _) =>
        {
            if (state == KeyStatus.KeyDown)
                Engine.ShouldStop = true;
        }
    };
}