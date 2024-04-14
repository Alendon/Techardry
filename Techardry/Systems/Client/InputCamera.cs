using System.Diagnostics;
using System.Numerics;
using BepuUtilities;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Input;
using MintyCore.Registries;
using MintyCore.SystemGroups;
using MintyCore.Utils;
using Silk.NET.GLFW;
using Techardry.Components.Client;
using Techardry.Identifications;
using Techardry.Render;

namespace Techardry.Systems.Client;

[ExecutionSide(GameType.Client)]
[RegisterSystem("input_camera")]
[ExecuteInSystemGroup<InitializationSystemGroup>]
public partial class InputCamera(
    IPlayerHandler playerHandler,
    IInputHandler inputHandler,
    IInputDataManager renderInputData) : ASystem
{
    [ComponentQuery] private readonly ComponentQuery<(Camera, InputComponent), Position> _cameraQuery = new();

    private static Vector3 _input = Vector3.Zero;
    private const float MouseSensitivity = 1f;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public override void Setup(SystemManager systemManager)
    {
        _cameraQuery.Setup(this);
    }

    protected override void Execute()
    {
        if (World is null) return;

        if (_stopwatch.Elapsed.TotalSeconds > 1)
            _stopwatch.Restart();


        foreach (var currentEntity in _cameraQuery)
        {
            if (World.EntityManager.GetEntityOwner(currentEntity.Entity) != playerHandler.LocalPlayerGameId)
                continue;

            ref var camera = ref currentEntity.GetCamera();
            ref var input = ref currentEntity.GetInputComponent();

            if (Engine.Window is { MouseLocked: true })
            {
                float deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
                _stopwatch.Restart();
                camera.Yaw += inputHandler.MouseDelta.X * MouseSensitivity * deltaTime;
                camera.Pitch = Math.Clamp(camera.Pitch + inputHandler.MouseDelta.Y * MouseSensitivity * deltaTime,
                    MathHelper.ToRadians(-85), MathHelper.ToRadians(85));
            }

            var rotation = Quaternion.CreateFromYawPitchRoll(camera.Yaw, camera.Pitch, 0f);
            camera.Forward = Vector3.Transform(Vector3.UnitZ, rotation);
            camera.Upward = Vector3.Transform(-Vector3.UnitY, rotation);

            var movement = _input;
            if (movement != Vector3.Zero)
                movement = Vector3.Normalize(movement);

            var direction = rotation;
            direction.Y = 1;
            movement = Vector3.Transform(movement, rotation);

            input.Movement = movement;

            camera.Dirty = true;
            input.Dirty = true;

            renderInputData.SetSingletonInputData(RenderInputDataIDs.Camera,
                new CameraInputData(camera, currentEntity.GetPosition()));
        }
    }

    [RegisterInputAction("Move_Forward")]
    public static InputActionDescription MoveForward => new InputActionDescription()
    {
        DefaultInput = Keys.W,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                _input.Z = Math.Clamp(_input.Z + 1, -1, 1);
            }

            if (parameters.InputAction is InputAction.Release)
            {
                _input.Z = Math.Clamp(_input.Z - 1, -1, 1);
            }

            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("Move_Backward")]
    public static InputActionDescription MoveBackwards => new InputActionDescription()
    {
        DefaultInput = Keys.S,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                _input.Z = Math.Clamp(_input.Z - 1, -1, 1);
            }

            if (parameters.InputAction is InputAction.Release)
            {
                _input.Z = Math.Clamp(_input.Z + 1, -1, 1);
            }
            
            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("Move_Left")]
    public static InputActionDescription MoveLeft => new InputActionDescription()
    {
        DefaultInput = Keys.A,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                _input.X = Math.Clamp(_input.X - 1, -1, 1);
            }

            if (parameters.InputAction is InputAction.Release)
            {
                _input.X = Math.Clamp(_input.X + 1, -1, 1);
            }
            
            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("Move_Right")]
    public static InputActionDescription MoveRight => new InputActionDescription()
    {
        DefaultInput = Keys.D,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                _input.X = Math.Clamp(_input.X + 1, -1, 1);
            }

            if (parameters.InputAction is InputAction.Release)
            {
                _input.X = Math.Clamp(_input.X - 1, -1, 1);
            }
            
            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("Move_Up")]
    public static InputActionDescription MoveUp => new InputActionDescription()
    {
        DefaultInput = Keys.Space,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                _input.Y = Math.Clamp(_input.Y + 1, -1, 1);
            }

            if (parameters.InputAction is InputAction.Release)
            {
                _input.Y = Math.Clamp(_input.Y - 1, -1, 1);
            }
            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("Move_Down")]
    public static InputActionDescription MoveDown => new InputActionDescription()
    {
        DefaultInput = Keys.ShiftLeft,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                _input.Y = Math.Clamp(_input.Y - 1, -1, 1);
            }

            if (parameters.InputAction is InputAction.Release)
            {
                _input.Y = Math.Clamp(_input.Y + 1, -1, 1);
            }
            
            return InputActionResult.Stop;
        }
    };

    [RegisterInputAction("Mouse_Lock")]
    public static InputActionDescription MouseLock => new InputActionDescription()
    {
        DefaultInput = Keys.F,
        ActionCallback = parameters =>
        {
            if (parameters.InputAction is InputAction.Press)
            {
                if (Engine.Window != null) Engine.Window.MouseLocked = !Engine.Window.MouseLocked;
            }
            
            return InputActionResult.Stop;
        }
    };

    public override Identification Identification => SystemIDs.InputCamera;
}