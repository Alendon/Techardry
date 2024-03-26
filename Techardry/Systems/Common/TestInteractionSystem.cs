using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Registries;
using MintyCore.Utils;
using Serilog;
using Silk.NET.Input;
using Techardry.Components.Client;
using Techardry.Identifications;
using Techardry.Utils;
using Techardry.Voxels;
using Techardry.World;

namespace Techardry.Systems.Common;

[ExecutionSide(GameType.Server)]
[RegisterSystem("test_interaction")]
public partial class TestInteractionSystem(IInputHandler inputHandler) : ASystem
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
            if (!BlockPlaceIssued && !BlockBreakIssued)
                continue;

            if (World is not TechardryWorld world) continue;

            var pos = entity.GetPosition().Value;
            var dir = entity.GetCamera().Forward;

            if (!world.PhysicsWorld.RayCast(pos, dir, 100,
                    out var tResult,
                    out _, out var normal))
            {
                BlockBreakIssued = false;
                BlockPlaceIssued = false;
                continue;
            }


            if (BlockBreakIssued)
            {
                var blockPos = pos + dir * tResult;
                blockPos -= normal * 0.01f;

                var chunkPos = new Int3((int)blockPos.X, (int)blockPos.Y, (int)blockPos.Z) / Chunk.Size;
                world.ChunkManager.SetBlock(chunkPos, blockPos, BlockIDs.Air, VoxelOctree.SizeOneDepth + depth);
                BlockBreakIssued = false;
            }

            if (BlockPlaceIssued)
            {
                var blockPos = pos + dir * tResult;
                blockPos += normal * 0.01f;

                var chunkPos = new Int3((int)blockPos.X, (int)blockPos.Y, (int)blockPos.Z) / Chunk.Size;
                world.ChunkManager.SetBlock(chunkPos, blockPos, BlockIDs.Stone, VoxelOctree.SizeOneDepth + depth);
                BlockPlaceIssued = false;
            }
        }
    }

    static bool BlockPlaceIssued = false;
    static bool BlockBreakIssued = false;
    static int depth = 0;

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
                depth = Math.Min(depth + 1, VoxelOctree.MaxSplitCount);
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