using System.Numerics;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Input;
using MintyCore.Modding;
using MintyCore.Registries;
using MintyCore.UI;
using MintyCore.Utils;
using Silk.NET.GLFW;
using Techardry.Components.Client;
using Techardry.Identifications;
using Techardry.UI.InGame;
using Techardry.Voxels;
using Techardry.World;

namespace Techardry.Systems.Common;

[ExecutionSide(GameType.Server)]
[RegisterSystem("test_interaction")]
public partial class TestInteractionSystem(
    IViewLocator viewLocator,
    IModManager modManager,
    IRenderManager renderManager) : ASystem
{
    [ComponentQuery] private ComponentQuery<object, (Position, Camera)> _query = new();
    static Identification currentBlock = BlockIDs.Stone;

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

            var blockId = BlockIDs.Air;
            if (hit)
            {
                blockId = world.ChunkManager.GetBlockId(blockPos - normal * 0.01f);
            }

            if (viewLocator.GetViewModel(ViewModelIDs.UiOverlay) is UiOverlayViewModel viewModel)
            {
                viewModel.LookingAt = hit ? blockPos : new Vector3(float.NaN);
                viewModel.PlayerPosition = pos;
                viewModel.BlockSize = (float)Math.Pow(2, -(depth - VoxelOctree.SizeOneDepth));
                viewModel.CurrentLookingAtBlock =
                    modManager.RegistryManager.GetObjectStringId(blockId.Mod, blockId.Category, blockId.Object);
                viewModel.CurrentHeldBlock = modManager.RegistryManager.GetObjectStringId(currentBlock.Mod,
                    currentBlock.Category, currentBlock.Object);
                viewModel.Fps = renderManager.FrameRate;
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
                world.ChunkManager.SetBlock(blockPos, currentBlock, depth);
                BlockPlaceIssued = false;
            }
        }
    }

    static bool BlockPlaceIssued = false;
    static bool BlockBreakIssued = false;
    static int depth = VoxelOctree.SizeOneDepth;


    [RegisterInputAction("place_block")]
    public static InputActionDescription PlaceBlock => new()
    {
        DefaultInput = MouseButton.Right,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
                BlockPlaceIssued = true;

            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("break_block")]
    public static InputActionDescription BreakBlock => new()
    {
        DefaultInput = MouseButton.Left,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
                BlockBreakIssued = true;

            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("increase_depth")]
    public static InputActionDescription IncreaseDepth => new()
    {
        DefaultInput = Keys.KeypadAdd,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
                depth = Math.Min(depth + 1, VoxelOctree.MaximumTotalDivision);

            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("decrease_depth")]
    public static InputActionDescription DecreaseDepth => new()
    {
        DefaultInput = Keys.KeypadSubtract,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
                depth = Math.Max(depth - 1, 0);

            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("change_build_block")]
    public static InputActionDescription ChangeBuildBlock => new()
    {
        DefaultInput = Keys.B,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                if (currentBlock == BlockIDs.Stone) currentBlock = BlockIDs.Dirt;
                else if (currentBlock == BlockIDs.Dirt) currentBlock = BlockIDs.Grass;
                else if (currentBlock == BlockIDs.Grass) currentBlock = BlockIDs.Stone;
            }

            return InputActionResult.Stop;
        }
    };

    public override Identification Identification => SystemIDs.TestInteraction;
}