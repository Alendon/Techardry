using System;
using System.Diagnostics;
using System.Numerics;
using BepuUtilities;
using MintyCore;
using MintyCore.Components.Common;
using MintyCore.ECS;
using MintyCore.Graphics.Render.Managers;
using MintyCore.Registries;
using MintyCore.SystemGroups;
using MintyCore.Utils;
using Silk.NET.Input;
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

    [RegisterKeyAction("Move_Forward")]
    public static KeyActionInfo MoveForward => new KeyActionInfo()
    {
        Key = Key.W,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                _input.Z = Math.Clamp(_input.Z + 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                _input.Z = Math.Clamp(_input.Z - 1, -1, 1);
            }
        }
    };

    [RegisterKeyAction("Move_Backward")]
    public static KeyActionInfo MoveBackwards => new KeyActionInfo()
    {
        Key = Key.S,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                _input.Z = Math.Clamp(_input.Z - 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                _input.Z = Math.Clamp(_input.Z + 1, -1, 1);
            }
        }
    };

    [RegisterKeyAction("Move_Left")]
    public static KeyActionInfo MoveLeft => new KeyActionInfo()
    {
        Key = Key.A,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                _input.X = Math.Clamp(_input.X - 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                _input.X = Math.Clamp(_input.X + 1, -1, 1);
            }
        }
    };

    [RegisterKeyAction("Move_Right")]
    public static KeyActionInfo MoveRight => new KeyActionInfo()
    {
        Key = Key.D,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                _input.X = Math.Clamp(_input.X + 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                _input.X = Math.Clamp(_input.X - 1, -1, 1);
            }
        }
    };

    [RegisterKeyAction("Move_Up")]
    public static KeyActionInfo MoveUp => new KeyActionInfo()
    {
        Key = Key.Space,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                _input.Y = Math.Clamp(_input.Y + 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                _input.Y = Math.Clamp(_input.Y - 1, -1, 1);
            }
        }
    };

    [RegisterKeyAction("Move_Down")]
    public static KeyActionInfo MoveDown => new KeyActionInfo()
    {
        Key = Key.ShiftLeft,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                _input.Y = Math.Clamp(_input.Y - 1, -1, 1);
            }

            if (state is KeyStatus.KeyUp)
            {
                _input.Y = Math.Clamp(_input.Y + 1, -1, 1);
            }
        }
    };

    [RegisterKeyAction("Mouse_Lock")]
    public static KeyActionInfo MouseLock => new KeyActionInfo()
    {
        Key = Key.F,
        Action = (state, _) =>
        {
            if (state is KeyStatus.KeyDown)
            {
                if (Engine.Window != null) Engine.Window.MouseLocked = !Engine.Window.MouseLocked;
            }
        }
    };

    public override Identification Identification => SystemIDs.InputCamera;
}