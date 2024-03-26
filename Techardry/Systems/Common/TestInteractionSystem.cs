using System.Numerics;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Serilog;
using Silk.NET.Input;
using Techardry.Components.Client;
using Techardry.Identifications;
using Techardry.UI;
using Techardry.Utils;
using Techardry.Voxels;
using Techardry.World;

namespace Techardry.Systems.Common;

[ExecutionSide(GameType.Server)]
[RegisterSystem("test_interaction")]
public partial class TestInteractionSystem : ASystem
{
    [ComponentQuery] private ComponentQuery<object, (Position, Camera)> _query = new();

    public override void Setup(SystemManager systemManager)
    {
        _query.Setup(this);
    }

    protected override void Execute()
    {
        foreach (var entity in _query)
        {
            if (World is not TechardryWorld world) continue;

            var pos = entity.GetPosition().Value;
            var dir = entity.GetCamera().Forward;

            var hit = world.PhysicsWorld.RayCast(pos, dir, 100,
                out var tResult, out _, out var normal);
            var blockPos = pos + dir * tResult;

            if (Engine.Desktop?.Root is IngameUi ui)
            {
                ui.SetBlockPos(hit ? blockPos : new Vector3(float.NaN));
                ui.SetPlayerPos(pos);
                ui.SetBlockSize(Math.Pow(2, -(depth - VoxelOctree.SizeOneDepth)));
            }

            if (!hit)
            {
                BlockBreakIssued = false;
                BlockPlaceIssued = false;
                continue;
            }


            if (BlockBreakIssued)
            {
                blockPos -= normal * 0.01f;
                world.ChunkManager.SetBlock(blockPos, BlockIDs.Air, depth);
                BlockBreakIssued = false;
            }

            if (BlockPlaceIssued)
            {
                blockPos += normal * 0.01f;
                world.ChunkManager.SetBlock(blockPos, BlockIDs.Stone, depth);
                BlockPlaceIssued = false;
            }
        }
    }

    static bool BlockPlaceIssued = false;
    static bool BlockBreakIssued = false;
    static int depth = VoxelOctree.SizeOneDepth;

    [RegisterKeyAction("place_block")]
    public static KeyActionInfo PlaceBlock => new()
    {
        MouseButton = MouseButton.Right,
        Action = (_, status) =>
        {
            if (status == MouseButtonStatus.MouseButtonDown)
                BlockPlaceIssued = true;
        }
    };

    [RegisterKeyAction("break_block")]
    public static KeyActionInfo BreakBlock => new()
    {
        MouseButton = MouseButton.Left,
        Action = (_, status) =>
        {
            if (status == MouseButtonStatus.MouseButtonDown)
                BlockBreakIssued = true;
        }
    };

    [RegisterKeyAction("increase_depth")]
    public static KeyActionInfo IncreaseDepth => new()
    {
        Key = Key.KeypadAdd,
        Action = (status, _) =>
        {
            if (status == KeyStatus.KeyDown)
                depth = Math.Min(depth + 1, VoxelOctree.MaximumTotalDivision);
        }
    };

    [RegisterKeyAction("decrease_depth")]
    public static KeyActionInfo DecreaseDepth => new()
    {
        Key = Key.KeypadSubtract,
        Action = (status, _) =>
        {
            if (status == KeyStatus.KeyDown)
                depth = Math.Max(depth - 1, 0);
        }
    };

    public override Identification Identification => SystemIDs.TestInteraction;
}